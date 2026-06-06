# NTE-iniEditor

Small Windows console tool for editing HT/NTE encrypted `GameUserSettings.ini`
files. It is designed to be published as a Native AOT executable.

## What it does

On launch, the tool checks both config files under the current user's
`%LOCALAPPDATA%` folder:

```text
HT\Saved\Config\Windows\GameUserSettings.ini
HT\Saved_GAT\Config\Windows\GameUserSettings.ini
```

The newer file is used as the edit source. The tool decrypts it, writes a
temporary plain-text `.ini` file, opens that file with the default associated
application, and waits for you to save your edits and press Enter.

After that, it encrypts the edited content again, creates backups, and
overwrites both encrypted config files with the edited version.

If encryption or writing fails after editing, the decrypted temporary file is
kept and its path is printed in the console.

## Key

The default key is:

```text
UVbP6pjjw5KZhvddie3tfhg1pVkkveY8
```

You can override it from the command line:

```powershell
htini.exe --key "UVbP6pjjw5KZhvddie3tfhg1pVkkveY8"
```

The key can be either a 32-byte string or a 64-character hex value, with or
without a `0x` prefix.

## Build

Regular build:

```powershell
dotnet build .\NTE-iniEditor.sln -c Release
```

Run tests:

```powershell
dotnet run --project .\tests\NTE-iniEditor.Tests\NTE-iniEditor.Tests.csproj -c Release
```

Publish a Native AOT executable:

```powershell
dotnet publish .\src\NTE-iniEditor\NTE-iniEditor.csproj -c Release -r win-x64 --self-contained true
```

The executable will be under:

```text
src\NTE-iniEditor\bin\Release\net8.0\win-x64\publish\NTE-iniEditor.exe
```

## Usage

Close the game first, then run:

```powershell
NTE-iniEditor.exe
```

Edit the opened `.ini` file, save it, return to the console, and press Enter.

Backups are written next to the original files with a `.bak-YYYYMMDD-HHMMSS`
suffix.
