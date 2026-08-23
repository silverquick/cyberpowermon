# 修正内容の確認 (Walkthrough): 実用性・UX向上機能

実用性・UXの向上として、以下の4つの主要機能を実装しました：
1. **タスクトレイ常駐（Minimize to Tray / Close to Tray）**
2. **Windows ネイティブ通知（停電・復電・バッテリー低下・過負荷等）**
3. **Windows 起動時の自動起動（Run on Startup / Start Minimized）**
4. **テレメトリ・イベント履歴のデータエクスポート（CSV / JSON）**

---

## 1. 変更・新規作成ファイル一覧

| レイヤー / プロジェクト | ファイル | 変更内容 |
| :--- | :--- | :--- |
| **UpsMonitor.Core** | [UpsEvents.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core/UpsEvents.cs) | `UpsEventSeverity` enum の定義と `UpsEvent` への重大度プロパティ追加 |
| **UpsMonitor.Infrastructure** | [AppConfiguration.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/AppConfiguration.cs) | `UiConfiguration` にトレイ常駐・通知・自動起動設定を追加 |
| | [StartupManager.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/StartupManager.cs) (新規) | Windows レジストリ（`HKCU\...\Run`）を用いた自動起動の登録・解除 |
| | [TelemetryExporter.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/TelemetryExporter.cs) (新規) | SQLite データベースからテレメトリ・イベントを CSV / JSON で出力 |
| **UpsMonitor.App** | [Resources/app.ico](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/app.ico) (新規) | マルチサイズ（16px〜256px）の Windows アプリアイコン |
| | [Resources/app.png](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/app.png) (新規) | 高解像度アプリアイコン画像 |
| | [UpsMonitor.App.csproj](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/UpsMonitor.App.csproj) | `<ApplicationIcon>` と埋め込みリソース設定 |
| | [TrayIconManager.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/TrayIconManager.cs) (新規) | Win32 `Shell_NotifyIcon`、トレイ右クリックメニュー、イベントバルーン/トースト通知 |
| | [App.xaml.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/App.xaml.cs) | `--tray` / `--minimized` コマンドライン引数によるバックグラウンド起動処理 |
| | [MainWindow.xaml.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml.cs) | `OnClosing` でのトレイ格納、スリープ復帰（`WM_POWERBROADCAST`）検知 |
| | [MainViewModel.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs) | 設定バインディング、エクスポートコマンド、通知・ツールチップ連動 |
| | [MainWindow.xaml](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml) | Settings タブにトレイ・自動起動カード、History/Logs にエクスポートボタン追加 |
| | [App.xaml](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/App.xaml) | Fluent 風 CheckBox スタイルの追加 |
| | [Strings.ja-JP.xaml](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.ja-JP.xaml) / [Strings.en-US.xaml](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.en-US.xaml) | 日本語・英語ローカライズリソースの追加 |
| **UpsMonitor.Core.Tests** | [Program.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core.Tests/Program.cs) | イベント重大度分類および CSV/JSON エクスポートのテスト追加 |

---

## 2. 実装詳細

### A. タスクトレイ常駐
- Win32 `Shell_NotifyIcon` API を使用し、外部 NuGet パッケージ不要で軽量に常駐。
- **トレイ操作**:
  - 左クリック / ダブルクリック: メインウィンドウの表示・最前面化 / 最小化切り替え
  - 右クリック: 「開く」「最小化 / 非表示」「終了」のコンテキストメニュー表示
  - マウスホバー: UPS製品名、電源状態、バッテリー残量、残り時間のツールチップ表示
  - 「終了」選択時に `Application.Current.Shutdown()` を明示実行し、バックグラウンド非同期タスクのタイムアウト付き解放によりデッドロックなく安全に終了。
- **ウィンドウの挙動**:
  - ウィンドウを閉じる（`[X]` ボタン）際、`CloseToTray` が有効なら終了せずトレイに非表示化。
  - ウィンドウ最小化時、`MinimizeToTray` が有効ならタスクバーからトレイに格納。

### B. Windows ネイティブイベント通知
- 商用電源断（`PowerLost`）、復電（`PowerRestored`）、残量低下（`BatteryLow`）、臨界停止切迫（`BatteryCritical`）、残り時間低下（`RuntimeLow`）、過負荷（`OverloadDetected`）、UPS切断・再接続（`UpsDisconnected` / `UpsReconnected`）を検知した際に、Windows のアクションセンター / トースト通知で通知。
- 設定画面で「Windows 通知を有効にする」のオン/オフが可能。

### C. 自動起動 (Run on Startup)
- 設定画面のチェックボックスからワンクリックで Windows 起動時の自動起動（`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`）を設定可能。
- 「起動時にタスクトレイに最小化する」を有効にすると、`--tray` 引数付きで登録され、PC 起動時にウィンドウを表示せずトレイに格納された状態で静かに監視を開始。

### D. データエクスポート
- **History タブ**: 「テレメトリ CSV エクスポート」「テレメトリ JSON エクスポート」ボタンを配置。
- **Logs タブ**: 「イベントログ CSV エクスポート」ボタンを配置。
- 期間に応じた生データ・イベントを標準的なフォーマットで保存可能。
