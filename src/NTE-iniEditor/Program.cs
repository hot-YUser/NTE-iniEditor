using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace NTE_iniEditor;

internal static class Program
{
    private const string DefaultKey = "UVbP6pjjw5KZhvddie3tfhg1pVkkveY8";

    public static int Main(string[] args)
    {
        string? tempPathForError = null;

        try
        {
            Console.OutputEncoding = Encoding.UTF8;

            CliOptions options = CliOptions.Parse(args);
            if (options.ShowHelp)
            {
                CliOptions.PrintUsage();
                return 0;
            }

            byte[] key = KeyParser.ParseKey(options.Key ?? DefaultKey);
            TargetFile[] targets =
            [
                new("Saved", DefaultPaths.SavedGameUserSettings),
                new("Saved_GAT", DefaultPaths.SavedGatGameUserSettings)
            ];

            TargetFile workingTarget = TargetSelector.SelectNewestExisting(targets);

            Console.WriteLine("HT encrypted GameUserSettings editor");
            Console.WriteLine();
            Console.WriteLine("Candidate files:");
            foreach (TargetFile target in targets)
            {
                string state = File.Exists(target.Path)
                    ? File.GetLastWriteTime(target.Path).ToString("yyyy-MM-dd HH:mm:ss")
                    : "missing";
                Console.WriteLine($"  {target.Name}: {target.Path}");
                Console.WriteLine($"    Last write: {state}");
            }

            Console.WriteLine();
            Console.WriteLine($"Working source: {workingTarget.Name}");
            Console.WriteLine($"Reading: {workingTarget.Path}");

            string[] encryptedLines = File.ReadAllLines(workingTarget.Path, Encoding.ASCII);
            string[] plainLines = IniCrypto.DecryptLines(encryptedLines, key);

            TempIniFile tempIni = TempIniFile.Create(plainLines);
            tempPathForError = tempIni.Path;

            Console.WriteLine();
            Console.WriteLine($"Decrypted temporary file: {tempIni.Path}");
            Console.WriteLine("Opening it with the default associated application...");
            EditorLauncher.OpenWithDefaultApplication(tempIni.Path);
            Console.WriteLine();
            Console.WriteLine("Save your edits in the opened file, close the editor if needed, then return here.");
            Console.Write("Press Enter to encrypt and write both GameUserSettings.ini files...");
            Console.ReadLine();

            string[] editedLines = File.ReadAllLines(tempIni.Path, Encoding.UTF8);
            string[] newEncryptedLines = IniCrypto.EncryptLines(editedLines, key);
            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

            Console.WriteLine();
            Console.WriteLine("Writing encrypted files:");
            foreach (TargetFile target in targets)
            {
                string? backupPath = FileWriter.BackupIfExists(target.Path, timestamp);
                if (backupPath is not null)
                {
                    Console.WriteLine($"  Backup: {backupPath}");
                }

                FileWriter.WriteAllLinesAtomic(target.Path, newEncryptedLines);
                string[] verifyEncryptedLines = File.ReadAllLines(target.Path, Encoding.ASCII);
                string[] verifyPlainLines = IniCrypto.DecryptLines(verifyEncryptedLines, key);
                if (!LineComparer.Equals(editedLines, verifyPlainLines))
                {
                    throw new InvalidOperationException($"Verification failed after writing: {target.Path}");
                }

                Console.WriteLine($"  Updated: {target.Path}");
            }

            Console.WriteLine();
            Console.WriteLine("Done. Both files now contain the edited settings.");
            tempIni.Delete();
            tempPathForError = null;
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Error:");
            Console.Error.WriteLine(ex.Message);
            if (tempPathForError is not null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"The decrypted temporary file was kept here: {tempPathForError}");
            }

            return 1;
        }
    }
}

public sealed record TargetFile(string Name, string Path);

public static class DefaultPaths
{
    public static string SavedGameUserSettings { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HT",
        "Saved",
        "Config",
        "Windows",
        "GameUserSettings.ini");

    public static string SavedGatGameUserSettings { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HT",
        "Saved_GAT",
        "Config",
        "Windows",
        "GameUserSettings.ini");
}

public sealed class CliOptions
{
    public string? Key { get; private init; }
    public bool ShowHelp { get; private init; }

    public static CliOptions Parse(string[] args)
    {
        string? key = null;
        bool showHelp = false;

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "--help" or "-h" or "/?")
            {
                showHelp = true;
                continue;
            }

            if (arg is "--key" or "-k")
            {
                if (i + 1 >= args.Length)
                {
                    throw new ArgumentException($"{arg} requires a value.");
                }

                key = args[++i];
                continue;
            }

            const string keyPrefix = "--key=";
            if (arg.StartsWith(keyPrefix, StringComparison.Ordinal))
            {
                key = arg[keyPrefix.Length..];
                continue;
            }

            throw new ArgumentException($"Unknown argument: {arg}");
        }

        return new CliOptions
        {
            Key = key,
            ShowHelp = showHelp
        };
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  htini [--key <32-byte-string-or-64-hex-key>]");
        Console.WriteLine();
        Console.WriteLine("Default key:");
        Console.WriteLine("  UVbP6pjjw5KZhvddie3tfhg1pVkkveY8");
        Console.WriteLine();
        Console.WriteLine("Behavior:");
        Console.WriteLine("  1. Reads these files under the current user's LOCALAPPDATA:");
        Console.WriteLine("     HT\\Saved\\Config\\Windows\\GameUserSettings.ini");
        Console.WriteLine("     HT\\Saved_GAT\\Config\\Windows\\GameUserSettings.ini");
        Console.WriteLine("  2. Uses the newer file as the edit source.");
        Console.WriteLine("  3. Decrypts it, opens a temporary .ini with the default app,");
        Console.WriteLine("     then re-encrypts your edits.");
        Console.WriteLine("  4. Backs up and overwrites both encrypted files.");
    }
}

public static class KeyParser
{
    public static byte[] ParseKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Key cannot be empty.");
        }

        string trimmed = value.Trim();
        string hex = trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
            ? trimmed[2..]
            : trimmed;

        if (hex.Length == 64 && IsHex(hex))
        {
            byte[] bytes = new byte[32];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }

            return bytes;
        }

        byte[] asciiBytes = Encoding.ASCII.GetBytes(trimmed);
        if (asciiBytes.Length != 32)
        {
            throw new ArgumentException("Key must be a 32-byte string or a 64-character hex value.");
        }

        return asciiBytes;
    }

    private static bool IsHex(string value)
    {
        foreach (char c in value)
        {
            bool valid =
                c is >= '0' and <= '9' ||
                c is >= 'a' and <= 'f' ||
                c is >= 'A' and <= 'F';

            if (!valid)
            {
                return false;
            }
        }

        return true;
    }
}

public static class TargetSelector
{
    public static TargetFile SelectNewestExisting(IReadOnlyList<TargetFile> targets)
    {
        TargetFile? newest = null;
        DateTime newestWriteTime = DateTime.MinValue;

        foreach (TargetFile target in targets)
        {
            if (!File.Exists(target.Path))
            {
                continue;
            }

            DateTime writeTime = File.GetLastWriteTimeUtc(target.Path);
            if (newest is null || writeTime > newestWriteTime)
            {
                newest = target;
                newestWriteTime = writeTime;
            }
        }

        return newest ?? throw new FileNotFoundException(
            "Neither GameUserSettings.ini file exists under LOCALAPPDATA\\HT.");
    }
}

public static class IniCrypto
{
    private const string SplitMarker = "|SPLIT|";

    public static string[] DecryptLines(IReadOnlyList<string> encryptedLines, byte[] key)
    {
        string[] plainLines = new string[encryptedLines.Count];
        for (int i = 0; i < encryptedLines.Count; i++)
        {
            plainLines[i] = DecryptLine(encryptedLines[i], key);
        }

        return plainLines;
    }

    public static string[] EncryptLines(IReadOnlyList<string> plainLines, byte[] key)
    {
        string[] encryptedLines = new string[plainLines.Count];
        for (int i = 0; i < plainLines.Count; i++)
        {
            encryptedLines[i] = EncryptLine(plainLines[i], key);
        }

        return encryptedLines;
    }

    public static string DecryptLine(string encryptedLine, byte[] key)
    {
        byte[] cipherBytes = Convert.FromBase64String(encryptedLine.Trim());
        using Aes aes = CreateAes(key);
        using ICryptoTransform decryptor = aes.CreateDecryptor();
        byte[] plainBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
        string plain = Encoding.UTF8.GetString(plainBytes);

        return plain.EndsWith(SplitMarker, StringComparison.Ordinal)
            ? plain[..^SplitMarker.Length]
            : plain;
    }

    public static string EncryptLine(string plainLine, byte[] key)
    {
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainLine + SplitMarker);
        using Aes aes = CreateAes(key);
        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] cipherBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return Convert.ToBase64String(cipherBytes);
    }

    private static Aes CreateAes(byte[] key)
    {
        if (key.Length != 32)
        {
            throw new ArgumentException("AES-256 requires a 32-byte key.", nameof(key));
        }

        Aes aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        return aes;
    }
}

public sealed class TempIniFile
{
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private TempIniFile(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public static TempIniFile Create(IReadOnlyList<string> plainLines)
    {
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"htini-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.ini");

        File.WriteAllLines(path, plainLines, Utf8NoBom);
        return new TempIniFile(path);
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
        catch
        {
            // Leaving a temp file behind is less harmful than hiding the real result.
        }
    }
}

public static class EditorLauncher
{
    public static void OpenWithDefaultApplication(string path)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = path,
            UseShellExecute = true
        };

        Process.Start(startInfo);
    }
}

public static class FileWriter
{
    private static readonly Encoding Ascii = Encoding.ASCII;

    public static string? BackupIfExists(string path, string timestamp)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        string backupPath = $"{path}.bak-{timestamp}";
        int suffix = 2;
        while (File.Exists(backupPath))
        {
            backupPath = $"{path}.bak-{timestamp}-{suffix}";
            suffix++;
        }

        File.Copy(path, backupPath);
        return backupPath;
    }

    public static void WriteAllLinesAtomic(string path, IReadOnlyList<string> lines)
    {
        string? directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = $"{path}.tmp-{Guid.NewGuid():N}";
        File.WriteAllLines(tempPath, lines, Ascii);
        File.Move(tempPath, path, overwrite: true);
    }
}

public static class LineComparer
{
    public static bool Equals(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (int i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
