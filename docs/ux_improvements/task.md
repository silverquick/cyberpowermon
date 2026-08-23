# タスクリスト: 実用性・UX向上機能

- [x] **ステップ 1: インフラ・設定基盤の拡張**
  - [x] `UpsMonitor.Infrastructure/AppConfiguration.cs` に UI / システム設定（トレイ常駐・通知・自動起動）を追加
  - [x] `UpsMonitor.Infrastructure/StartupManager.cs` を新規作成（Windows レジストリ自動起動制御）
  - [x] `UpsMonitor.Infrastructure/TelemetryExporter.cs` を新規作成（CSV / JSON エクスポートロジック）
- [x] **ステップ 2: トレイ常駐と Windows 通知の実装**
  - [x] `UpsMonitor.App/TrayIconManager.cs` を新規作成（Win32 `Shell_NotifyIcon`、トレイメニュー、イベントバルーン/トースト通知）
  - [x] `UpsMonitor.App/App.xaml.cs` にトレイライフサイクルと起動引数（`--tray` / `--minimized`）処理を追加
  - [x] `UpsMonitor.App/MainWindow.xaml.cs` に閉じるボタン（`OnClosing`）時のトレイ格納処理を追加
- [x] **ステップ 3: ViewModel と UI（XAML / 多言語リソース）の拡張**
  - [x] `UpsMonitor.App/LocalizationManager.cs` およびリソースファイル（`Strings.ja-JP.xaml`, `Strings.en-US.xaml`）に文言を追加
  - [x] `UpsMonitor.App/MainViewModel.cs` に設定項目、エクスポートコマンド、通知連動を追加
  - [x] `UpsMonitor.App/MainWindow.xaml` にトレイ・スタートアップ設定トグルとエクスポートボタンを配置
- [x] **ステップ 4: ビルドとテストの確認**
  - [x] コードの静的検証および単体テスト（`UpsMonitor.Core.Tests`）の拡張
- [x] **ステップ 5: 修正内容の確認 (Walkthrough) ドキュメントの作成**
  - [x] `docs/ux_improvements/walkthrough.md` を作成
