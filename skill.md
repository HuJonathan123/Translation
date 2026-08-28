# 專案需求與規格標準 (Skill Criteria Specification)

## 1. 專案概述 (Project Overview)
本專案旨在開發一個 C# 桌面端即時翻譯應用程式 (Frontend)，配合部署於 Render 的後端翻譯服務 (Backend API)。

使用者在系統任何角落選取文字並按下全局熱鍵 `Ctrl + Alt + T` 時，應用程式會在背景自動模擬複製 (`Ctrl + C`)、讀取剪貼簿文字內容、透過 REST API 發送至 Render 後端進行翻譯，並於游標處或螢幕適當位置彈出輕量化小視窗展示翻譯結果。

---

## 2. 核心功能與軟體標準 (Functional Criteria)

### FC-1: 全局熱鍵監聽 (Global Hotkey Listener)
- **需求細節**：使用 Win32 API (`RegisterHotKey` / `UnregisterHotKey`) 註冊全域快捷鍵 `Ctrl + Alt + T`。
- **標準**：當應用程式常駐於系統托盤 (System Tray) 或背景時，無論當前焦點在任何第三方軟體 (如 Chrome, Word, VS Code, PDF Reader)，快捷鍵均可正常觸發。

### FC-2: 模擬剪貼簿自動擷取 (Clipboard Text Capture)
- **需求細節**：
  1. 觸發快捷鍵後，前端自動模擬 `Ctrl + C` 鍵盤輸入事件 (`keybd_event` 或 `SendInput`)。
  2. 暫停 50~100ms 確保系統剪貼簿資料更新完成。
  3. 讀取 `Clipboard.GetText()` 取得被選取的字串。
  4. (選用) 在翻譯完成後恢復使用者原先剪貼簿之內容，保護使用者剪貼簿資料不被意料外覆蓋。
- **標準**：能處理空白字串、過長字串及剪貼簿非純文字 (如圖片、檔案) 之異常情況防錯。

### FC-3: API 溝通與後端翻譯 (Backend API Integration)
- **需求細節**：
  1. 使用非同步 `HttpClient` 呼叫部署於 Render 的 REST API 端點 (如 `/api/translate`)。
  2. 傳遞 Payload：
     ```json
     {
       "text": "Selected text here",
       "source_lang": "auto",
       "target_lang": "zh-TW"
     }
     ```
  3. 支援處理 Render 免費版冷啟動 (Cold Start Delay, ~50s) 期間的等待狀態提示。
- **標準**：前端必須實作 Timeout 超時處理 (如 10s/60s)、錯誤重試與友善錯誤訊息 (如網路連線失敗、API 500 錯誤等)。

### FC-4: 浮動翻譯結果 UI (Popup Result Window)
- **需求細節**：
  1. 輕量無邊框 / 圓角現代化視窗 (WPF / Avalonia)。
  2. 顯示內容：
     - 原始選取文字
     - 翻譯結果
     - 一鍵複製翻譯結果按鈕 (Copy Result)
     - 關閉按鈕 (Close)
  3. 視窗彈出位置：滑鼠游標附近或螢幕右下角。
  4. 快速關閉機制：按 `Esc` 鍵或點擊視窗外部區域時自動隱藏。
- **標準**：視窗彈出時不得導致當前編輯區失焦（可選 `WS_EX_NOACTIVATE` 屬性），確保使用者流暢的工作體驗。

### FC-5: 後臺語言設定與常駐 UI (Settings & System Tray UI)
- **需求細節**：
  1. 應用程式預設縮小至 Windows 右下角系統托盤 (System Tray Icon)。
  2. 點擊托盤選單可開啟「設定選單」：
     - **目標翻譯語言選擇** (例如：繁體中文 `zh-TW`、英文 `en`、日文 `ja`、韓文 `ko` 等)。
     - **Backend API URL 設定** (允許輸入 Render API 網址，例如 `https://your-translator-backend.onrender.com`)。
     - **開機自動啟動設定** (選用)。
- **標準**：設定檔 (如 `appsettings.json` 或 `user.config`) 需持久化保存，任何裝置下載前端單一執行檔後，輸入 Backend API 網址即可即連即用。

---

## 3. 架構設計與技術選型 (Technical Stack & Architecture)

```
+-------------------------------------------------------------+
|                     Windows Desktop Client                  |
|  +------------------+  +------------------+  +-----------+  |
|  | Global Hotkey    |  | Clipboard Manager|  | Settings  |  |
|  | (Ctrl+Alt+T)     |  | (Ctrl+C Simulation|  | UI (Tray) |  |
|  +--------+---------+  +--------+---------+  +-----+-----+  |
|           |                     |                  |        |
|           +----------+----------+                  |        |
|                      v                             v        |
|               +--------------+             +---------------+|
|               | Popup View   |             | App Config    ||
|               +--------------+             +---------------+|
+----------------------|--------------------------------------+
                       | HTTP POST (JSON)
                       v
+-------------------------------------------------------------+
|                     Render Cloud Backend                    |
|             (FastAPI / Express / ASP.NET Core)              |
|                      |                                      |
|                      v                                      |
|             Translation Provider Engine                     |
|         (Google Translate / DeepL / OpenAI)                 |
+-------------------------------------------------------------+
```

### 技術選型
- **前端 Framework**: C# (.NET 8.0 WPF) - 原生 Windows API 支援度高、UI 彈性強。
- **後端 Framework**: Python (FastAPI) 或 Node.js / ASP.NET Core，部署於 Render.com 免費/付費服務。
- **翻譯 API**: 整合 Google Translate (Free/Official API)、DeepL 或 OpenAI API。

---

## 4. 驗收標準 (Acceptance Criteria - AC)

| 編號 | 驗收項目 | 驗收測試步驟與標準 |
|---|---|---|
| **AC-1** | 熱鍵響應測試 | 於記事本、瀏覽器或 PDF 中選取文字後按 `Ctrl+Alt+T`，應在 500ms 內觸發彈窗並顯示翻譯中狀態。 |
| **AC-2** | 剪貼簿穩定性 | 快速連續按熱鍵，系統不應崩潰；選用非文字內容時，彈窗提示「未偵測到文字」。 |
| **AC-3** | 後端可連線與切換 | 變更設定頁面中的 Render API URL 後，測試按鈕可確認後端連線狀態與翻譯正常。 |
| **AC-4** | UI 互動體驗 | 翻譯視窗能正確顯示源語言與目標語言翻譯結果，按 `Esc` 鍵能順利關閉視窗。 |
| **AC-5** | 可攜性與免安裝 | 編譯產出獨立 EXE 檔案，放置於乾淨 Windows 環境即可直接開啟運行並運作。 |

---

## 5. 後續開發步驟建議 (Implementation Plan)

1. **Step 1: Backend 建立與部署** (FastAPI / Express API，部署至 Render，確認 `/api/translate` 可通)。
2. **Step 2: C# 前端熱鍵與剪貼簿原型** (驗證 `RegisterHotKey` 及 `SendInput` 模擬 `Ctrl+C` 擷取文字)。
3. **Step 3: C# 前端 Popup 視窗與 HttpClient 整合** (串接 Render API 取得結果並呈現)。
4. **Step 4: 系統托盤與設定頁面** (支援語言切換與網址設定)。
