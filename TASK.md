# タスク: 起動・初期化パフォーマンス分析と改良

【対象】cyberpowermon (WPF/.NET UPS監視アプリ) の起動・初期化パフォーマンス分析と改良。

【調査対象ファイル】
- UpsMonitor.App/App.xaml.cs
- UpsMonitor.Infrastructure/AppConfiguration.cs
- 単一インスタンスチェック処理
- DI/サービス初期化
- SQLite初期化処理(TelemetryHistory系)

【観点】
- アプリ起動からメインウィンドウ表示までの所要時間
- 同期I/O待ち
- 不要な初期化処理の直列実行
- 設定ファイル読み込みコスト
- SQLiteスキーマ初期化・マイグレーションのコスト

【成果物】
1. `docs/performance/startup_analysis.md` に分析結果(計測方法・ボトルネック箇所・改善案)を記載
2. 実際にコードを改良(非同期化・遅延初期化・不要処理削減など)し、`UpsMonitor.sln` のビルドと `UpsMonitor.Core.Tests` の全テストがパスすることを確認
   - ビルド: `dotnet build UpsMonitor.sln`
   - テスト: `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`
3. 変更は `git commit` しておくこと(コミットメッセージは日本語で分かりやすく)

【注意】
- UI/描画・SQLiteクエリ・バックグラウンドポーリング処理そのものの詳細改良は別タスクが担当するため深入りしないこと。
- 作業が完了したら、この TASK.md の末尾に完了報告を追記すること。

---

## 完了報告

- **分析ドキュメント作成**: `docs/performance/startup_analysis.md` にて起動・初期化シーケンス、ボトルネック箇所（SQLite直列初期化待機、毎回全DDL実行、設定ファイル初回保存待機）、改善案を整理・記述しました。
- **コード改良の実施**:
  - `SqliteTelemetryStore.cs`: `PRAGMA user_version` によるスキーマDDL短絡スキップ（既存DB時の初期化コスト激減）、`_initTask` による非同期・遅延初期化管理、キューイングの非ブロッキング化を実装。
  - `SqliteTelemetryQueries.cs`: 各クエリ実行時に `await EnsureInitializedAsync()` で安全に遅延初期化を待機するよう更新。
  - `JsonConfigurationStore.cs`: 初回設定読み込み時のデフォルトファイル保存を非同期バックグラウンド実行にし、起動待機を排除。
  - `App.xaml.cs`: `OnStartup` 内で `_historyStore.InitializeAsync()` の同期await待機を解除し、メインウィンドウ表示（`window.Show()`）を最優先化。
- **検証結果**:
  - ビルド (`dotnet build UpsMonitor.sln`): 警告0・エラー0でビルド成功。
  - テスト (`dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`): 全22件のテストすべてパス (22/22 tests passed)。
- **関連ドキュメント**: `docs/startup_performance_optimization/` 配下に `task.md`, `implementation_plan.md`, `walkthrough.md` を作成・保存。

