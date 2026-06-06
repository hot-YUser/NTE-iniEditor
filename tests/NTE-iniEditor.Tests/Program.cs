using NTE_iniEditor;
using System.Text;

int failures = 0;

Run("預設金鑰可解析為 32 位元組", () =>
{
    byte[] key = KeyParser.ParseKey(KnownKeys.Windows);
    AssertEqual(32, key.Length);
});

Run("Android 金鑰可解析為 32 位元組", () =>
{
    byte[] key = KeyParser.ParseKey(KnownKeys.Android);
    AssertEqual(32, key.Length);
});

Run("十六進位金鑰支援 0x 前綴", () =>
{
    byte[] key = KeyParser.ParseKey("0x390B40DA3E0805AE7397DFA707E7227DBA06C35E95262E7FFF8F8E60CBC7A69C");
    AssertEqual(32, key.Length);
    AssertEqual(0x39, key[0]);
    AssertEqual(0x9C, key[31]);
});

Run("加密與解密可完整往返", () =>
{
    byte[] key = KeyParser.ParseKey("UVbP6pjjw5KZhvddie3tfhg1pVkkveY8");
    string[] source =
    [
        ";METADATA=(Diff=true, UseCommands=true)",
        "[/Script/HTGame.HTGameUserSettings]",
        "FrameRateLimit=41.000000",
        "",
        "Language=zh-Hant"
    ];

    string[] encrypted = IniCrypto.EncryptLines(source, key);
    string[] decrypted = IniCrypto.DecryptLines(encrypted, key);

    AssertTrue(LineComparer.Equals(source, decrypted), "往返後的內容應該一致");
});

Run("已知密文可解出中繼資料列", () =>
{
    byte[] key = KeyParser.ParseKey(KnownKeys.Windows);
    string decrypted = IniCrypto.DecryptLine(
        "PgeXONQlonNTPXQbvRbO5wFnY4uHRNNRKU4EthKhbLC8acDrzpPNnKj8NojJfD9D",
        key);

    AssertEqual(";METADATA=(Diff=true, UseCommands=true)", decrypted);
});

Run("Android 已知密文可解出中繼資料列", () =>
{
    byte[] key = KeyParser.ParseKey(KnownKeys.Android);
    string decrypted = IniCrypto.DecryptLine(
        "5S/9+KXxvY+jIJkSh1fX/8rmoFVzUUDcCsgK2Sc8ZTOHrY/OxhrXodzfpBTJ3ovt",
        key);

    AssertEqual(";METADATA=(Diff=true, UseCommands=true)", decrypted);
});

Run("CLI 可解析英文區服與 Engine 設定檔", () =>
{
    CliOptions options = CliOptions.Parse(["--server", "Saved_GAT", "--ini", "Engine"]);

    AssertEqual(1, options.ServerCodes.Count);
    AssertEqual("Saved_GAT", options.ServerCodes[0]);
    AssertEqual(1, options.IniNames.Count);
    AssertEqual("Engine.ini", options.IniNames[0]);

    ServerProfile server = KnownServers.Resolve(options.ServerCodes[0]);
    AssertEqual("Saved_GAT", server.Code);
});

Run("CLI 可解析 Android 模式與完整 ini 路徑", () =>
{
    string iniPath = @"C:\Temp\Android\GameUserSettings.ini";
    CliOptions options = CliOptions.Parse(["--android", "--ini", iniPath]);

    AssertTrue(options.Android, "應該啟用 Android 模式");
    AssertEqual(1, options.IniNames.Count);
    AssertEqual(iniPath, options.IniNames[0]);
    AssertTrue(CliOptions.IsDirectIniPath(options.IniNames[0]), "完整路徑應視為直接檔案");
});

Run("CLI 可用 -a 啟用 Android 模式", () =>
{
    CliOptions options = CliOptions.Parse(["-a", "--ini", @"Android\Engine.ini"]);

    AssertTrue(options.Android, "應該啟用 Android 模式");
    AssertEqual(@"Android\Engine.ini", options.IniNames[0]);
});

Run("CLI 預設不強制指定區服與設定檔", () =>
{
    CliOptions options = CliOptions.Parse([]);

    AssertEqual(0, options.ServerCodes.Count);
    AssertEqual(0, options.IniNames.Count);
});

Run("EditSession 可直接讀取指定 ini 路徑", () =>
{
    byte[] key = KeyParser.ParseKey(KnownKeys.Android);
    string[] source =
    [
        ";METADATA=(Diff=true, UseCommands=true)",
        "[Internationalization]",
        "Language=zh-Hant"
    ];

    string tempDirectory = Path.Combine(Path.GetTempPath(), "NTE-iniEditor.Tests", Guid.NewGuid().ToString("N"));
    string iniPath = Path.Combine(tempDirectory, "GameUserSettings.ini");
    EditSession? session = null;

    try
    {
        Directory.CreateDirectory(tempDirectory);
        File.WriteAllLines(iniPath, IniCrypto.EncryptLines(source, key), Encoding.ASCII);

        session = EditSession.Create(KnownServers.All, iniPath, explicitServer: false, key);
        string[] decrypted = File.ReadAllLines(session.TempFile.Path, Encoding.UTF8);

        AssertEqual("GameUserSettings.ini", session.IniName);
        AssertTrue(session.Source.Server is null, "直接路徑來源不應綁定區服");
        AssertEqual(iniPath, session.Source.Path);
        AssertEqual(1, session.Targets.Count);
        AssertEqual(iniPath, session.Targets[0].Path);
        AssertTrue(LineComparer.Equals(source, decrypted), "直接路徑解密內容應該一致");
    }
    finally
    {
        session?.TempFile.Delete();
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }
});

Console.WriteLine();
if (failures == 0)
{
    Console.WriteLine("全部測試通過。");
    return 0;
}

Console.WriteLine($"{failures} 個測試失敗。");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"通過：{name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"失敗：{name}");
        Console.WriteLine($"      {ex.Message}");
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"預期「{expected}」，實際「{actual}」。");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
