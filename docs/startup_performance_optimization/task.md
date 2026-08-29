# タスクリスト: 起動・初期化パフォーマンス分析と改良

- [x] 起動・初期化シーケンスのパフォーマンス分析とボトルネック特定 <!-- id: 0 -->
  - [x] `App.xaml.cs` (起動シーケンス、単一インスタンスチェック、初期化フロー) の確認 <!-- id: 1 -->
  - [x] `AppConfiguration.cs` / `JsonConfigurationStore.cs` (設定読み込み・デフォルト生成コスト) の確認 <!-- id: 2 -->
  - [x] `SqliteTelemetryStore.cs` / `SqliteTelemetryQueries.cs` (DB接続オープン・DDLスキーマ実行コスト) の確認 <!-- id: 3 -->
  - [x] `MainWindow.xaml.cs` / `MainViewModel.cs` (UI構築・データバインディング・初期化) の確認 <!-- id: 4 -->
- [x] パフォーマンス分析ドキュメント `docs/performance/startup_analysis.md` の作成 <!-- id: 5 -->
- [x] 起動・初期化パフォーマンスの改善コード実装 <!-- id: 6 -->
  - [x] `SqliteTelemetryStore` の初期化の非同期・遅延化と `PRAGMA user_version` によるスキーマDDL短絡 <!-- id: 7 -->
  - [x] `JsonConfigurationStore` の初回デフォルト保存の非同期・非ブロッキング化 <!-- id: 8 -->
  - [x] `App.xaml.cs` の起動フロー最適化（ウィンドウの即時表示とバックグラウンド初期化の連携） <!-- id: 9 -->
- [x] ビルドおよびテストの実行確認 <!-- id: 10 -->
  - [x] `dotnet build UpsMonitor.sln` <!-- id: 11 -->
  - [x] `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj` <!-- id: 12 -->
- [x] ドキュメント（`implementation_plan.md`, `walkthrough.md`）の作成 <!-- id: 13 -->
- [x] `git commit` による変更記録 <!-- id: 14 -->
- [x] `TASK.md` 末尾への完了報告追記 <!-- id: 15 -->
