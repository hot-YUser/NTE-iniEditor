using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace NTE_iniEditor;

internal static class Program
{
    private const string 預設金鑰 = "UVbP6pjjw5KZhvddie3tfhg1pVkkveY8";

    public static int Main(string[] args)
    {
        List<string> 保留的暫存檔 = [];

        try
        {
            Console.OutputEncoding = Encoding.UTF8;

            CliOptions options = CliOptions.Parse(args);
            if (options.ShowHelp)
            {
                CliOptions.PrintUsage();
                return 0;
            }

            byte[] key = KeyParser.ParseKey(options.Key ?? 預設金鑰);
            IReadOnlyList<ServerProfile> servers = options.ServerCodes.Count > 0
                ? KnownServers.ResolveAll(options.ServerCodes)
                : KnownServers.All;

            IReadOnlyList<string> iniNames = options.IniNames.Count > 0
                ? options.IniNames
                : CliOptions.DefaultIniNames;

            bool explicitServer = options.ServerCodes.Count > 0;
            List<EditSession> sessions = [];

            foreach (string iniName in iniNames)
            {
                EditSession session = EditSession.Create(servers, iniName, explicitServer, key);
                sessions.Add(session);
                保留的暫存檔.Add(session.TempFile.Path);
            }

            Console.WriteLine("NTE-iniEditor");
            Console.WriteLine();
            Console.WriteLine("本次開啟的設定檔：");
            foreach (EditSession session in sessions)
            {
                Console.WriteLine($"  {session.IniName}");
                Console.WriteLine($"    來源：{session.Source.Server.DisplayName} ({session.Source.Server.Code})");
                Console.WriteLine($"    路徑：{session.Source.Path}");
                Console.WriteLine($"    暫存：{session.TempFile.Path}");
                Console.WriteLine("    將寫入：");
                foreach (WritableTarget target in session.Targets)
                {
                    Console.WriteLine($"      {target.Server.DisplayName} ({target.Server.Code})：{target.Path}");
                }
            }

            Console.WriteLine();
            Console.WriteLine("正在使用系統預設程式開啟暫存 ini。");
            foreach (EditSession session in sessions)
            {
                EditorLauncher.OpenWithDefaultApplication(session.TempFile.Path);
            }

            Console.WriteLine();
            Console.WriteLine("請在已開啟的暫存 ini 中完成編輯並儲存。");
            Console.Write("全部儲存完成後，回到這裡按 Enter 重新加密並寫回原檔：");
            Console.ReadLine();

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Console.WriteLine();
            Console.WriteLine("正在寫回加密設定檔：");

            foreach (EditSession session in sessions)
            {
                string[] editedLines = File.ReadAllLines(session.TempFile.Path, Encoding.UTF8);
                string[] encryptedLines = IniCrypto.EncryptLines(editedLines, key);

                foreach (WritableTarget target in session.Targets)
                {
                    string? backupPath = FileWriter.BackupIfExists(target.Path, timestamp);
                    if (backupPath is not null)
                    {
                        Console.WriteLine($"  備份：{backupPath}");
                    }

                    FileWriter.WriteAllLinesAtomic(target.Path, encryptedLines);
                    string[] verifyEncryptedLines = File.ReadAllLines(target.Path, Encoding.ASCII);
                    string[] verifyPlainLines = IniCrypto.DecryptLines(verifyEncryptedLines, key);
                    if (!LineComparer.Equals(editedLines, verifyPlainLines))
                    {
                        throw new InvalidOperationException($"寫入後驗證失敗：{target.Path}");
                    }

                    Console.WriteLine($"  更新：{target.Path}");
                }
            }

            foreach (EditSession session in sessions)
            {
                session.TempFile.Delete();
                保留的暫存檔.Remove(session.TempFile.Path);
            }

            Console.WriteLine();
            Console.WriteLine("完成。");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("錯誤：");
            Console.Error.WriteLine(ex.Message);
            if (保留的暫存檔.Count > 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("以下暫存明文檔已保留，方便你取回剛才的編輯內容：");
                foreach (string path in 保留的暫存檔)
                {
                    Console.Error.WriteLine($"  {path}");
                }
            }

            return 1;
        }
    }
}

public sealed record ServerProfile(string Code, string DisplayName, string FolderName)
{
    public string ConfigDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HT",
        FolderName,
        "Config",
        "Windows");

    public string GetIniPath(string iniName) => Path.Combine(ConfigDirectory, iniName);
}

public static class KnownServers
{
    public static IReadOnlyList<ServerProfile> All { get; } =
    [
        new("Saved", "陸服", "Saved"),
        new("Saved_GAT", "台港澳服", "Saved_GAT"),
        new("Saved_Global", "國際服", "Saved_Global")
    ];

    public static IReadOnlyList<ServerProfile> ResolveAll(IReadOnlyList<string> values)
    {
        List<ServerProfile> servers = [];
        foreach (string value in values)
        {
            ServerProfile server = Resolve(value);
            if (!servers.Any(existing => string.Equals(existing.Code, server.Code, StringComparison.OrdinalIgnoreCase)))
            {
                servers.Add(server);
            }
        }

        return servers;
    }

    public static ServerProfile Resolve(string value)
    {
        string normalized = value.Trim();
        foreach (ServerProfile server in All)
        {
            if (string.Equals(normalized, server.Code, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, server.DisplayName, StringComparison.OrdinalIgnoreCase) ||
                IsAlias(normalized, server.Code))
            {
                return server;
            }
        }

        string available = string.Join("、", All.Select(server => $"{server.Code} ({server.DisplayName})"));
        throw new ArgumentException($"未知區服：{value}。可用區服：{available}");
    }

    private static bool IsAlias(string value, string code)
    {
        return code switch
        {
            "Saved" => value is "陸服" or "中國服" or "CN",
            "Saved_GAT" => value is "台港澳" or "台港澳服" or "港澳台" or "港澳台服" or "GAT",
            "Saved_Global" => value is "國際" or "國際服" or "Global",
            _ => false
        };
    }
}

public sealed record CandidateFile(ServerProfile Server, string IniName, string Path, DateTime LastWriteTimeUtc);

public sealed record WritableTarget(ServerProfile Server, string IniName, string Path);

public sealed record EditSession(
    string IniName,
    CandidateFile Source,
    IReadOnlyList<WritableTarget> Targets,
    TempIniFile TempFile)
{
    public static EditSession Create(
        IReadOnlyList<ServerProfile> servers,
        string iniName,
        bool explicitServer,
        byte[] key)
    {
        List<CandidateFile> existingFiles = [];
        foreach (ServerProfile server in servers)
        {
            string path = server.GetIniPath(iniName);
            if (File.Exists(path))
            {
                existingFiles.Add(new CandidateFile(server, iniName, path, File.GetLastWriteTimeUtc(path)));
            }
        }

        if (existingFiles.Count == 0)
        {
            string serverNames = string.Join("、", servers.Select(server => $"{server.Code} ({server.DisplayName})"));
            throw new FileNotFoundException($"在指定區服中找不到 {iniName}：{serverNames}");
        }

        if (explicitServer)
        {
            List<string> missing = [];
            foreach (ServerProfile server in servers)
            {
                string path = server.GetIniPath(iniName);
                if (!File.Exists(path))
                {
                    missing.Add($"{server.Code} ({server.DisplayName})：{path}");
                }
            }

            if (missing.Count > 0)
            {
                throw new FileNotFoundException($"指定區服缺少 {iniName}：" + Environment.NewLine + string.Join(Environment.NewLine, missing));
            }
        }

        CandidateFile source = existingFiles
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .First();

        List<WritableTarget> targets = explicitServer
            ? servers.Select(server => new WritableTarget(server, iniName, server.GetIniPath(iniName))).ToList()
            : existingFiles.Select(file => new WritableTarget(file.Server, iniName, file.Path)).ToList();

        string[] encryptedLines = File.ReadAllLines(source.Path, Encoding.ASCII);
        string[] plainLines = IniCrypto.DecryptLines(encryptedLines, key);
        TempIniFile tempFile = TempIniFile.Create(iniName, plainLines);

        return new EditSession(iniName, source, targets, tempFile);
    }
}

public sealed class CliOptions
{
    public static IReadOnlyList<string> DefaultIniNames { get; } =
    [
        "GameUserSettings.ini",
        "Engine.ini"
    ];

    public string? Key { get; private init; }
    public bool ShowHelp { get; private init; }
    public IReadOnlyList<string> ServerCodes { get; private init; } = [];
    public IReadOnlyList<string> IniNames { get; private init; } = [];

    public static CliOptions Parse(string[] args)
    {
        string? key = null;
        bool showHelp = false;
        List<string> servers = [];
        List<string> iniNames = [];

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg is "--help" or "-h" or "/?" or "--說明")
            {
                showHelp = true;
                continue;
            }

            if (IsValueOption(arg, "--key", "-k", "--金鑰", out string? inlineKey))
            {
                key = inlineKey ?? ReadNextValue(args, ref i, arg);
                continue;
            }

            if (IsValueOption(arg, "--server", "-s", "--伺服器", "--區服", out string? inlineServer))
            {
                AddValues(servers, inlineServer ?? ReadNextValue(args, ref i, arg));
                continue;
            }

            if (IsValueOption(arg, "--ini", "-i", "--設定檔", out string? inlineIni))
            {
                AddValues(iniNames, inlineIni ?? ReadNextValue(args, ref i, arg), normalizeIni: true);
                continue;
            }

            throw new ArgumentException($"未知參數：{arg}");
        }

        return new CliOptions
        {
            Key = key,
            ShowHelp = showHelp,
            ServerCodes = servers,
            IniNames = iniNames
        };
    }

    public static void PrintUsage()
    {
        Console.WriteLine("用法：");
        Console.WriteLine("  NTE-iniEditor.exe [--key <key>] [--server <Saved|Saved_GAT|Saved_Global>] [--ini <filename.ini>]");
        Console.WriteLine();
        Console.WriteLine("預設金鑰：");
        Console.WriteLine("  UVbP6pjjw5KZhvddie3tfhg1pVkkveY8");
        Console.WriteLine();
        Console.WriteLine("區服：");
        Console.WriteLine("  Saved：陸服");
        Console.WriteLine("  Saved_GAT：台港澳服");
        Console.WriteLine("  Saved_Global：國際服");
        Console.WriteLine();
        Console.WriteLine("預設設定檔：");
        Console.WriteLine("  GameUserSettings.ini");
        Console.WriteLine("  Engine.ini");
        Console.WriteLine();
        Console.WriteLine("行為：");
        Console.WriteLine("  未指定區服時，每個 ini 會使用現有副本中修改時間最新者作為編輯來源，");
        Console.WriteLine("  並在儲存後覆蓋該 ini 已存在的所有區服副本。");
        Console.WriteLine("  指定區服時，只讀寫指定區服。");
        Console.WriteLine();
        Console.WriteLine("範例：");
        Console.WriteLine("  NTE-iniEditor.exe");
        Console.WriteLine("  NTE-iniEditor.exe --server Saved_GAT");
        Console.WriteLine("  NTE-iniEditor.exe --ini Engine.ini");
        Console.WriteLine("  NTE-iniEditor.exe --server Saved_Global --ini GameUserSettings.ini");
    }

    private static bool IsValueOption(
        string arg,
        string longName,
        string shortName,
        string chineseName,
        out string? inlineValue)
    {
        return IsValueOption(arg, [longName, shortName, chineseName], out inlineValue);
    }

    private static bool IsValueOption(
        string arg,
        string longName,
        string shortName,
        string chineseName,
        string alternativeChineseName,
        out string? inlineValue)
    {
        return IsValueOption(arg, [longName, shortName, chineseName, alternativeChineseName], out inlineValue);
    }

    private static bool IsValueOption(string arg, IReadOnlyList<string> names, out string? inlineValue)
    {
        foreach (string name in names)
        {
            if (arg == name)
            {
                inlineValue = null;
                return true;
            }

            string prefix = name + "=";
            if (arg.StartsWith(prefix, StringComparison.Ordinal))
            {
                inlineValue = arg[prefix.Length..];
                return true;
            }
        }

        inlineValue = null;
        return false;
    }

    private static string ReadNextValue(string[] args, ref int index, string optionName)
    {
        if (index + 1 >= args.Length)
        {
            throw new ArgumentException($"{optionName} 需要指定值。");
        }

        return args[++index];
    }

    private static void AddValues(List<string> values, string rawValue, bool normalizeIni = false)
    {
        foreach (string item in rawValue.Split([',', ';', '、'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string value = normalizeIni ? NormalizeIniName(item) : item;
            if (!values.Contains(value, StringComparer.OrdinalIgnoreCase))
            {
                values.Add(value);
            }
        }
    }

    public static string NormalizeIniName(string value)
    {
        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            throw new ArgumentException("設定檔名稱不可為空。");
        }

        if (Path.IsPathRooted(trimmed) ||
            trimmed.Contains(Path.DirectorySeparatorChar) ||
            trimmed.Contains(Path.AltDirectorySeparatorChar))
        {
            throw new ArgumentException($"設定檔名稱只接受檔名，不接受路徑：{value}");
        }

        return trimmed.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : trimmed + ".ini";
    }
}

public static class KeyParser
{
    public static byte[] ParseKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("金鑰不可為空。");
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
            throw new ArgumentException("金鑰必須是 32 位元組字串，或 64 字元十六進位值。");
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
            throw new ArgumentException("AES-256 需要 32 位元組金鑰。", nameof(key));
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

    public static TempIniFile Create(string iniName, IReadOnlyList<string> plainLines)
    {
        string safeName = SanitizeFileName(iniName);
        string path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"NTE-iniEditor-{safeName}-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.ini");

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
        }
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalid = System.IO.Path.GetInvalidFileNameChars();
        StringBuilder builder = new(value.Length);
        foreach (char c in value)
        {
            builder.Append(invalid.Contains(c) ? '_' : c);
        }

        return builder.ToString();
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
