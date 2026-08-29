# 修正内容の確認 (Walkthrough): 起動・初期化パフォーマンスの最適化

## 1. 概要
cyberpowermon（PowerGuard）におけるアプリ起動からメインウィンドウ表示までの所要時間を短縮するため、以下の最適化を実施しました。

1. **SQLite 初期化の非ブロッキング・遅延実行化**: `SqliteTelemetryStore` の初期化タスクをバックグラウンドで開始し、`OnStartup` での待機を解除。
2. **`PRAGMA user_version` によるスキーマDDL短絡**: 既存DBでは12個以上のテーブル/インデックス作成DDLをスキップ。
3. **設定ファイル初回保存の非同期・非ブロッキング化**: `JsonConfigurationStore.LoadAsync` でデフォルト設定保存をバックグラウンド化。
4. **UI即時表示とバックグラウンド初期化の連携**: `App.xaml.cs` でウィンドウ表示を最優先化。

---

## 2. 変更内容の詳細

### 2.1 `UpsMonitor.Infrastructure/SqliteTelemetryStore.cs` & `SqliteTelemetryQueries.cs`
- `_initTask` によるスレッドセーフかつべき等な非同期初期化保証メカニズム（`EnsureInitializedAsync`）を導入。
- `ExecuteSchemaAsync` において `PRAGMA user_version;` を照会し、すでにバージョン1以上であれば `CREATE TABLE/INDEX IF NOT EXISTS` DDLのパース・実行を完全にスキップ。
- `Queue` メソッドでの `EnsureInitialized` 依存を解除し、初期化中であってもスナップショットやイベントを安全にキューイングできるように変更。
- 各種クエリメソッド（`QueryWeeklyPatternAsync`, `QueryHistoryAsync`, `GetStatisticsAsync`, `QueryDailyEnergyReportsAsync`, `QueryPowerTroubleSummaryAsync`）で `await EnsureInitializedAsync()` を使用し、安全な遅延初期化を実現。

### 2.2 `UpsMonitor.Infrastructure/JsonConfigurationStore.cs`
- `LoadAsync` において設定ファイルが存在しない場合、デフォルトの `AppConfiguration` を即座に返し、ディスクへの `SaveAsync` を `Task.Run` で非同期実行。

### 2.3 `UpsMonitor.App/App.xaml.cs`
- `OnStartup` 内で `_historyStore.InitializeAsync()` の直列ブロッキング待機（`await`）を解除。
- メインウィンドウ（`MainWindow`）の作成と `window.Show()` を直ちに実行し、第一描画時間を最小化。
- 初期化タスクの例外は `ContinueWith` により UI スレッドへ安全にエラー通知。

---

## 3. テストと動作確認結果

### 3.1 ビルド確認
- コマンド: `dotnet build UpsMonitor.sln`
- 結果: **成功 (0 警告, 0 エラー)**

### 3.2 テスト実行
- コマンド: `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`
- 結果: **全22テスト パス (22/22 tests passed)**
  - `PASS Power state priority`
  - `PASS Power loss and restore events`
  - `PASS Alarm edge events`
  - `PASS Disconnect and reconnect events`
  - `PASS Invalid charge is rejected, not clamped`
  - `PASS Percentage capacities are not physical SOH`
  - `PASS Physical capacity ratio calculates SOH`
  - `PASS Runtime baseline calculates comparable-load SOH`
  - `PASS Current baseline reports relative trend only`
  - `PASS Relative runtime decline requests a battery check`
  - `PASS Known BHI anchors the runtime estimate`
  - `PASS Missing baseline leaves health unknown`
  - `PASS Hard battery failures override score`
  - `PASS Self-test failure requests a battery check`
  - `PASS SQLite history stores samples, rollups, events, and health`
  - `PASS Event severity classification`
  - `PASS Telemetry and event export to CSV/JSON`
  - `PASS Dynamic runtime-low threshold update`
  - `PASS Weekly heatmap pattern aggregation`
  - `PASS Runtime estimator load calculation`
  - `PASS Configuration theme, alerts, webhook, and command settings`
  - `PASS Daily energy reports and trouble summary queries`

---

## 4. 成果物一覧
- `docs/performance/startup_analysis.md`: 起動・初期化パフォーマンス分析と改善設計書
- `docs/startup_performance_optimization/task.md`: タスクリスト
- `docs/startup_performance_optimization/implementation_plan.md`: 実装計画
- `docs/startup_performance_optimization/walkthrough.md`: 修正内容確認（本ドキュメント）
