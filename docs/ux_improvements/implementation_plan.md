# 実装計画: 実用性・UX向上機能 (タスクトレイ常駐・通知・自動起動・データエクスポート)

## 1. 概要
`cyberpowermon` (PowerGuard) に以下の実用性・UX向上機能を追加します：
1. **タスクトレイ常駐（Minimize to Tray / Close to Tray）**
2. **Windows 通知（OS イベント通知）**
3. **Windows 起動時の自動起動（Run on Startup & Start Minimized）**
4. **テレメトリ・イベント履歴のエクスポート（CSV / JSON Export）**

---

## 2. 変更対象コンポーネント・ファイル

### A. 設定・インフラ層 (`UpsMonitor.Infrastructure`)
- [AppConfiguration.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/AppConfiguration.cs): トレイ常駐・通知・スタートアップ起動の設定項目を追加
- [TelemetryExporter.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/TelemetryExporter.cs) (新規作成): テレメトリおよびイベントログの CSV / JSON エクスポート処理
- [StartupManager.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/StartupManager.cs) (新規作成): Windows レジストリ（`HKCU\...\Run`）を用いた自動起動の登録・解除

### B. アプリケーション・UI層 (`UpsMonitor.App`)
- [TrayIconManager.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/TrayIconManager.cs) (新規作成): Win32 `Shell_NotifyIcon` / 通知 / コンテキストメニューの管理
- [App.xaml.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/App.xaml.cs): コマンドライン引数（`--tray` / `--minimized`）の処理、トレイアイコンの初期化と破棄
- [MainWindow.xaml.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml.cs): ウィンドウ最小化・閉じる操作時のトレイ格納処理
- [MainViewModel.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs): エクスポートコマンド、新設定プロパティのバインディング、イベント通知連携
- [MainWindow.xaml](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml): Settings タブに新設定トグルを追加、History/Logs タブにエクスポートボタンを追加
- [Resources/Strings.ja-JP.xaml](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.ja-JP.xaml) & [Resources/Strings.en-US.xaml](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.en-US.xaml): 多言語リソースの更新

---

## 3. 実装ステップ

### ステップ 1: 設定とインフラ基盤の拡張
1. `AppConfiguration.cs` に `MinimizeToTray`, `CloseToTray`, `EnableNotifications`, `StartMinimized`, `RunOnStartup` を追加。
2. `StartupManager.cs` を実装し、レジストリによる自動起動設定の読み書きを提供。
3. `TelemetryExporter.cs` を実装し、SQLite ストアからのデータ抽出と CSV / JSON 形式でのファイル出力を提供。

### ステップ 2: トレイ常駐と Windows 通知の実装
1. `TrayIconManager.cs` を実装。
   - Win32 `Shell_NotifyIcon` API を用いてタスクトレイにアイコンを表示。
   - 右クリックメニュー（表示、最小化、終了）のサポート。
   - ダブルクリックでメインウィンドウの表示/非表示トグル。
   - `NIF_INFO` による Windows ネイティブのイベント通知（停電、復電、バッテリー低下、過負荷等）の配信。
2. `App.xaml.cs` と `MainWindow.xaml.cs` でトレイアイコンを統合。
   - ウィンドウを閉じる際に、設定が有効なら終了せずトレイに非表示化。
   - `--tray` 引数による起動時の最小化。

### ステップ 3: ViewModel・UI・多言語リソースの更新
1. `MainViewModel.cs` に以下を追加：
   - 新設定項目のプロパティ・コマンド
   - テレメトリ/イベントのエクスポート用非同期コマンド（`ExportTelemetryCommand`, `ExportEventsCommand`）
   - イベント検知時の通知トリガー
2. `MainWindow.xaml` の Settings タブにトレイ・通知・スタートアップ設定 UI を配置。
3. `MainWindow.xaml` の History / Logs タブにエクスポートボタンを配置。
4. `Strings.ja-JP.xaml` および `Strings.en-US.xaml` にローカライズ文字列を追加。

### ステップ 4: ビルド・動作確認・テスト
1. `dotnet build` でビルドが通ることを確認。
2. テストの実行と動作検証。
