using NTE_iniEditor;

int failures = 0;

Run("預設金鑰可解析為 32 位元組", () =>
{
    byte[] key = KeyParser.ParseKey("UVbP6pjjw5KZhvddie3tfhg1pVkkveY8");
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
    byte[] key = KeyParser.ParseKey("UVbP6pjjw5KZhvddie3tfhg1pVkkveY8");
    string decrypted = IniCrypto.DecryptLine(
        "PgeXONQlonNTPXQbvRbO5wFnY4uHRNNRKU4EthKhbLC8acDrzpPNnKj8NojJfD9D",
        key);

    AssertEqual(";METADATA=(Diff=true, UseCommands=true)", decrypted);
});

Run("CLI 可解析台港澳服與 Engine 設定檔", () =>
{
    CliOptions options = CliOptions.Parse(["--伺服器", "台港澳服", "--設定檔", "Engine"]);

    AssertEqual(1, options.ServerCodes.Count);
    AssertEqual("台港澳服", options.ServerCodes[0]);
    AssertEqual(1, options.IniNames.Count);
    AssertEqual("Engine.ini", options.IniNames[0]);

    ServerProfile server = KnownServers.Resolve(options.ServerCodes[0]);
    AssertEqual("Saved_GAT", server.Code);
});

Run("CLI 預設不強制指定區服與設定檔", () =>
{
    CliOptions options = CliOptions.Parse([]);

    AssertEqual(0, options.ServerCodes.Count);
    AssertEqual(0, options.IniNames.Count);
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
