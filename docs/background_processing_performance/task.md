# タスク: バックグラウンド処理・リアルタイムテレメトリ収集パフォーマンス分析と改良

- [x] 1. パフォーマンス分析・課題抽出と分析レポート作成 <!-- id: 1 -->
  - [x] 1.1 UPSデバイスポーリングループおよびスレッド/タスク使用効率の分析 <!-- id: 1-1 -->
  - [x] 1.2 Webhook送信および外部コマンド実行の非同期化・リトライ・タイムアウト・プロセス管理の分析 <!-- id: 1-2 -->
  - [x] 1.3 イベント発火時のリソース確保/解放・メモリ割り当て・GCプレッシャー・メモリリーク調査 <!-- id: 1-3 -->
  - [x] 1.4 `docs/performance/background_processing_analysis.md` の作成 <!-- id: 1-4 -->
- [x] 2. コアポーリング・イベント処理の最適化実装 <!-- id: 2 -->
  - [x] 2.1 `UpsMonitorEngine.cs`: `WaitForNextPollAsync` の `SemaphoreSlim.WaitAsync` 直接待機化による Task/CTS/例外アロケーション撲滅 <!-- id: 2-1 -->
  - [x] 2.2 `UpsEvents.cs`: `UpsEventDetector.Observe` で定常時（イベントなし時）のゼロアロケーション化、`CompositeUpsEventSink` 最適化 <!-- id: 2-2 -->
  - [x] 2.3 `WindowsHidUpsProvider.cs`, `HidDeviceSession.cs`, `HidReportParser.cs`, `UpsHidMapper.cs`: ポーリング毎の Task.Run・LINQ・バッファ再割り当て最適化 <!-- id: 2-3 -->
- [x] 3. Webhook通知および外部コマンド実行の耐障害性・非同期処理改良 <!-- id: 3 -->
  - [x] 3.1 `WebhookNotifier.cs`: 一時的ネットワーク障害・HTTP 5xx/429 に対する指数バックオフ付きリトライおよびタイムアウト制御の実装 <!-- id: 3-1 -->
  - [x] 3.2 `CommandRunner.cs`: パイプバッファ枯渇によるデッドロック防止（標準出力/標準エラーの非同期ドレイン）、タイムアウト/キャンセル時のプロセスツリー強制終了（ゾンビプロセス防止）、引数エスケープ処理の実装 <!-- id: 3-2 -->
- [x] 4. テスト追加・ビルド検証・ドキュメント作成 <!-- id: 4 -->
  - [x] 4.1 `UpsMonitor.Core.Tests` への単体テスト追加（リトライ、コマンド実行、ゼロアロケーション、ポーリングキャンセル等） <!-- id: 4-1 -->
  - [x] 4.2 ビルドおよびテスト実行確認 (`dotnet build`, `dotnet run --project UpsMonitor.Core.Tests`) <!-- id: 4-2 -->
  - [x] 4.3 実装計画ドキュメント (`implementation_plan.md`) およびウォークスルー (`walkthrough.md`) の作成 <!-- id: 4-3 -->
  - [x] 4.4 `TASK.md` への完了報告追記および git コミット <!-- id: 4-4 -->
