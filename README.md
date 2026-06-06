# NTE-iniEditor

![平台](https://img.shields.io/badge/%E5%B9%B3%E5%8F%B0-Windows%20x64-0078D6)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![建置](https://img.shields.io/badge/%E5%BB%BA%E7%BD%AE-Native%20AOT-success)

NTE-iniEditor 是一支 Windows 命令列小工具，用來編輯《NTE》存在本機、且經過加密的 `ini` 設定檔（`GameUserSettings.ini`、`Engine.ini`）。

遊戲會把這些設定**逐行加密**後存檔，沒辦法直接用記事本開啟修改。這支工具負責：解密 → 用系統預設編輯器開啟暫存明文檔 → 等你改完並存檔 → 重新加密寫回，並在過程中自動備份與驗證。

## 功能特色

- **自動解密 / 加密**：你只需要編輯一般的純文字 `ini`，加解密交給工具處理。
- **多區服**：內建陸服、台港澳服、國際服三種安裝路徑，可自動偵測或手動指定。
- **多設定檔**：預設同時開啟 `GameUserSettings.ini` 與 `Engine.ini`，也可自選其他檔案。
- **安全機制**：寫回前自動備份原檔、原子寫入、寫回後再解密驗證內容一致；任何步驟失敗都會保留暫存明文檔，避免編輯成果遺失。
- **免安裝執行檔**：以 .NET 8 Native AOT 發佈為單一 `exe`，目標機器不需另外安裝 .NET 執行環境。

## 系統需求

- **執行**：Windows x64。使用自含式發佈版 `exe` 不需安裝 .NET。
- **建置**：[.NET 8 SDK](https://dotnet.microsoft.com/download)，以及 Native AOT 所需的 C++ 建置工具。

## 快速開始

> [!IMPORTANT]
> 使用前請先**完全關閉遊戲**，避免讀寫衝突，或被遊戲在結束時覆寫掉你的修改。

1. 關閉遊戲。
2. 執行 `NTE-iniEditor.exe`。
3. 工具會用系統預設程式開啟一份暫存的明文 `ini`，直接編輯並存檔。
4. 回到命令列視窗，按 <kbd>Enter</kbd>。
5. 工具會重新加密、備份原檔、寫回並驗證，看到「完成。」即結束。

未加任何參數時的預設行為：同時處理 `GameUserSettings.ini` 與 `Engine.ini`，來源取所有區服中「修改時間最新」的副本，寫回時覆蓋所有已存在該檔的區服。

## 使用方式

```powershell
# 自動偵測區服、預設兩個設定檔
.\NTE-iniEditor.exe

# 只編輯指定區服
.\NTE-iniEditor.exe --server Saved_GAT

# 只編輯指定設定檔
.\NTE-iniEditor.exe --ini Engine.ini

# 指定區服 + 單一設定檔
.\NTE-iniEditor.exe --server Saved_Global --ini GameUserSettings.ini

# 使用自訂金鑰
.\NTE-iniEditor.exe --key "UVbP6pjjw5KZhvddie3tfhg1pVkkveY8"

# 顯示說明
.\NTE-iniEditor.exe --help
```

### 命令列參數

| 參數 | 別名 | 說明 |
| --- | --- | --- |
| `--key <金鑰>` | `-k`、`--金鑰` | 覆蓋預設金鑰。可為 32 位元組字串，或 64 字元十六進位值（可含 `0x` 前綴）。 |
| `--server <區服>` | `-s`、`--伺服器`、`--區服` | 指定區服。可用 `Saved` / `Saved_GAT` / `Saved_Global`，或其別名（含中文）。 |
| `--ini <檔名>` | `-i`、`--設定檔` | 指定設定檔。只接受檔名（不接受路徑），未加 `.ini` 時自動補上。 |
| `--help` | `-h`、`/?`、`--說明` | 顯示用法說明。 |

補充說明：

- `--server` 與 `--ini` 可一次給多個值，以 `,` `;` `、` 分隔，例如 `--server Saved,Saved_GAT`。
- 支援 `--參數=值` 的寫法，例如 `--ini=Engine.ini`。
- 區服別名：
  - **陸服**：`Saved` / `陸服` / `中國服` / `CN`
  - **台港澳服**：`Saved_GAT` / `台港澳` / `台港澳服` / `港澳台` / `港澳台服` / `GAT`
  - **國際服**：`Saved_Global` / `國際` / `國際服` / `Global`

## 預設路徑與金鑰

工具會在目前 Windows 使用者的 `%LOCALAPPDATA%` 下尋找各區服設定：

```text
%LOCALAPPDATA%\HT\Saved\Config\Windows         # 陸服（未求證）
%LOCALAPPDATA%\HT\Saved_GAT\Config\Windows     # 台港澳服
%LOCALAPPDATA%\HT\Saved_Global\Config\Windows  # 國際服
```

預設處理的設定檔：

```text
GameUserSettings.ini
Engine.ini
```

預設金鑰：

```text
UVbP6pjjw5KZhvddie3tfhg1pVkkveY8
```

這把金鑰是遊戲用來**混淆**設定檔的固定金鑰，並不是保護你帳號的安全密鑰，因此可以公開放在原始碼與說明文件中。只有當遊戲改用不同金鑰時，才需要用 `--key` 覆蓋。

## 運作方式

### 加密方案

設定檔是**逐行加密**而非整檔加密。每一行的處理流程為：

1. 在明文行尾接上 `|SPLIT|` 標記。
2. 以 AES-256（ECB 模式、PKCS7 填充）加密。
3. 將密文做 Base64 編碼，成為磁碟上的一行。

解密時反向操作：Base64 解碼 → AES 解密 → 去掉行尾的 `|SPLIT|`。因此磁碟上的加密檔是純 ASCII（Base64），工具開給你編輯的暫存明文檔則是 UTF-8（無 BOM）。

金鑰固定為 32 位元組。`--key` 接受 32 位元組的 ASCII 字串，或 64 字元的十六進位值（可加 `0x` 前綴）。

### 來源與寫入對象的判斷

- **未指定 `--server`**：來源取所有區服中已存在、且修改時間最新的副本；寫回時覆蓋每個已存在該檔的區服（讓多份副本保持一致）。若所有區服都沒有該檔，視為錯誤。
- **有指定 `--server`**：只讀寫指定的區服；若指定的區服缺少該檔，視為錯誤（不會略過）。

### 安全機制

- **備份**：寫回前先把原檔複製成 `原檔名.bak-yyyyMMdd-HHmmss`（同秒衝突會再附加 `-2`、`-3`…）。
- **原子寫入**：先寫到暫存檔，再以覆蓋方式搬移到目標，避免寫到一半損毀原檔。
- **寫回驗證**：寫入後重新讀回並解密，與你的編輯內容逐行比對，不一致就中止並報錯。
- **失敗保留**：加密或寫入過程若發生錯誤，暫存明文檔不會被刪除，並在畫面上列出路徑，方便你取回剛才的編輯。

備份與暫存檔（`*.bak-*`、`*.tmp-*`）已列入 `.gitignore`。

## 常用設定速查

以下只列最常調整的項目。**完整清單**（含拍照模式、載具、各 `sg.*` 細項，以及「不建議手改」的內部 / 登入 / 公告類設定與查證來源）請見 **[設定選項速查](docs/設定選項速查.md)**。

開始前：`True` / `False` 代表開 / 關；多數畫質數值越高越吃效能；修改前讓工具自動備份（預設就會），真的出問題可還原 `.bak` 檔。

| 選項 | 說明 | 建議值 / 備註 |
| --- | --- | --- |
| `FrameRateLimit` | 幀率上限，`0` 為無效設定 | 可填遊戲選單沒有的值，如 `45` / `144` / `240` |
| `Language`、`Locale`、`AudioCulture` | 介面語言、地區格式、語音 | 例：`zh-Hant`、`ja`；不支援時可能回退 |
| `bUseVSync` | 垂直同步 | `True` / `False` |
| `ResolutionSizeX`、`ResolutionSizeY` | 解析度寬高 | 全螢幕下可能受遊戲套用流程影響 |
| `FullscreenMode` | 顯示模式（全螢幕 / 無邊框 / 視窗） | 進不去遊戲時改回原本可用的值 |
| `ScreenPercentage`、`sg.ResolutionQuality` | 內部渲染比例 | 降低可增幀；可能與 DLSS/FSR 互相覆蓋 |
| `sg.ShadowQuality` | 陰影品質 | 降低通常是最有效的增幀方式之一 |
| `sg.TextureQuality` | 貼圖品質 | 主要吃顯存 |
| `sg.EffectsQuality`、`sg.PostProcessQuality` | 特效 / 後處理品質 | 影響景深、泛光、色調等 |
| `MotionBlur` | 動態模糊 | `0` 通常為關閉 |
| `Sound_EffectVolumn`、`Sound_VoiceVolumn`、`Sound_MusicVolumn` | 音效 / 語音 / 音樂音量 | 原檔拼字為 `Volumn` |
| `bCameraShake` | 鏡頭震動 | 會暈 3D 可關 |
| `FPCameraFOV` | 第一人稱 FOV | 太高會變形、太低易暈 |
| `bInverseMouseX`、`bInverseMouseY` | 滑鼠 X / Y 反轉 | `True` / `False` |

> [!WARNING]
> `Param3`、`LastLoginUsername`、`LastLoginPassword` 與公告紀錄等可能含帳號或登入狀態資料，請勿手動修改，也不要貼到公開的 issue、截圖或發行說明。

## 建置與測試

```powershell
# 建置
dotnet build .\NTE-iniEditor.sln -c Release

# 執行測試（採用專案內建的簡易測試執行器）
dotnet run --project .\tests\NTE-iniEditor.Tests\NTE-iniEditor.Tests.csproj -c Release

# 發佈 Windows Native AOT 單一執行檔
dotnet publish .\src\NTE-iniEditor\NTE-iniEditor.csproj -c Release -r win-x64 --self-contained true
```

發佈產物位置：

```text
src\NTE-iniEditor\bin\Release\net8.0\win-x64\publish\NTE-iniEditor.exe
```

## 注意事項

- 本工具為**非官方工具**，修改遊戲設定有風險，請自行斟酌；出問題時可用自動產生的 `.bak` 檔還原。
- 使用前請關閉遊戲。
- 修改 `Version`、`GameVersion`，或解析度 / 顯示模式相關的「上次確認值」，可能導致遊戲重建設定或無法正常啟動，非必要請勿更動。
- 不要外流 `Param3`、登入帳密、公告等個人或登入相關資料。
