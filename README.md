# NTE-iniEditor

`NTE-iniEditor` 是用來編輯 NTE 本機加密 `ini` 設定檔的小工具，目標是發佈成 Windows Native AOT 執行檔。

## 功能

- 自動處理 NTE 加密 `ini` 的逐行解密與重新加密。
- 預設同時開啟 `GameUserSettings.ini` 與 `Engine.ini`。
- 支援三個可能的本機資料目錄：
  - `Saved`：陸服
  - `Saved_GAT`：台港澳服
  - `Saved_Global`：國際服
- 未指定區服時，每個 `ini` 會使用現有副本中修改時間最新者作為編輯來源。
- 未指定區服時，儲存後會覆蓋該 `ini` 已存在的所有區服副本。
- 指定區服時，只讀寫指定區服。
- 寫入前會自動備份原檔。
- 寫入後會重新解密驗證，確認內容一致。
- 如果加密或寫入失敗，暫存明文檔會保留，避免剛才的編輯內容遺失。

## 預設路徑

工具會在目前 Windows 使用者的 `%LOCALAPPDATA%` 下尋找：

```text
HT\Saved\Config\Windows
HT\Saved_GAT\Config\Windows
HT\Saved_Global\Config\Windows
```

預設開啟：

```text
GameUserSettings.ini
Engine.ini
```

## 金鑰

預設金鑰：

```text
UVbP6pjjw5KZhvddie3tfhg1pVkkveY8
```

可用 CLI 覆蓋：

```powershell
.\NTE-iniEditor.exe --金鑰 "UVbP6pjjw5KZhvddie3tfhg1pVkkveY8"
```

金鑰可以是 32 位元組字串，也可以是 64 字元十六進位值；十六進位值可包含 `0x` 前綴。

## 使用方式

關閉遊戲後執行：

```powershell
.\NTE-iniEditor.exe
```

工具會用系統預設程式開啟暫存的明文 `ini`。完成編輯並儲存後，回到命令列按 Enter，工具就會重新加密並寫回。

指定區服：

```powershell
.\NTE-iniEditor.exe --伺服器 Saved_GAT
```

指定設定檔：

```powershell
.\NTE-iniEditor.exe --設定檔 Engine.ini
```

指定國際服與單一設定檔：

```powershell
.\NTE-iniEditor.exe --伺服器 Saved_Global --設定檔 GameUserSettings.ini
```

## 建置

一般建置：

```powershell
dotnet build .\NTE-iniEditor.sln -c Release
```

執行測試：

```powershell
dotnet run --project .\tests\NTE-iniEditor.Tests\NTE-iniEditor.Tests.csproj -c Release
```

發佈 Windows Native AOT 執行檔：

```powershell
dotnet publish .\src\NTE-iniEditor\NTE-iniEditor.csproj -c Release -r win-x64 --self-contained true
```

產物位置：

```text
src\NTE-iniEditor\bin\Release\net8.0\win-x64\publish\NTE-iniEditor.exe
```

## 備份

每次寫回前，工具會在原檔旁建立備份，例如：

```text
GameUserSettings.ini.bak-20260606-191500
Engine.ini.bak-20260606-191500
```

## 專案簡介

NTE-iniEditor：用於編輯 NTE 加密本機 ini 設定檔的 Windows Native AOT 小工具。

## 發行說明草稿

初版功能：

- 支援 `Saved`、`Saved_GAT`、`Saved_Global` 三種本機資料目錄。
- 預設同時編輯 `GameUserSettings.ini` 與 `Engine.ini`。
- 支援 CLI 指定區服、設定檔與金鑰。
- 編輯前解密，儲存後重新加密並備份原檔。
