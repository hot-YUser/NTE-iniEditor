using NTE_iniEditor;

int failures = 0;

Run("default key parses to 32 bytes", () =>
{
    byte[] key = KeyParser.ParseKey("UVbP6pjjw5KZhvddie3tfhg1pVkkveY8");
    AssertEqual(32, key.Length);
});

Run("hex key parser accepts 0x-prefixed 64-character values", () =>
{
    byte[] key = KeyParser.ParseKey("0x390B40DA3E0805AE7397DFA707E7227DBA06C35E95262E7FFF8F8E60CBC7A69C");
    AssertEqual(32, key.Length);
    AssertEqual(0x39, key[0]);
    AssertEqual(0x9C, key[31]);
});

Run("encrypt and decrypt round trip preserves lines", () =>
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

    AssertTrue(LineComparer.Equals(source, decrypted), "round trip lines should match");
});

Run("known ciphertext decrypts to metadata line", () =>
{
    byte[] key = KeyParser.ParseKey("UVbP6pjjw5KZhvddie3tfhg1pVkkveY8");
    string decrypted = IniCrypto.DecryptLine(
        "PgeXONQlonNTPXQbvRbO5wFnY4uHRNNRKU4EthKhbLC8acDrzpPNnKj8NojJfD9D",
        key);

    AssertEqual(";METADATA=(Diff=true, UseCommands=true)", decrypted);
});

Console.WriteLine();
if (failures == 0)
{
    Console.WriteLine("All tests passed.");
    return 0;
}

Console.WriteLine($"{failures} test(s) failed.");
return 1;

void Run(string name, Action test)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine($"     {ex.Message}");
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
