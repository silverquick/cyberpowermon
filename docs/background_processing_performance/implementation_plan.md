# 実装計画: バックグラウンド処理・リアルタイムテレメトリ収集パフォーマンス分析と改良

## 1. 概要と目標
cyberpowermon (WPF/.NET 10 UPS監視アプリケーション) におけるバックグラウンド処理、UPS HIDデバイスポーリング、テレメトリ収集、イベント検知、Webhook通知、外部コマンド実行のパフォーマンス分析と最適化を行う。

### 主な目標
1. **ポーリングループの効率化とゼロアロケーション化**: 毎秒（または高頻度設定時）に発生する Task / CancellationTokenSource / 例外 / LINQ のメモリ割り当てとコンテキストスイッチを最小化する。
2. **外部通知・スクリプト実行の堅牢化**:
   - Webhook送信における一時的通信障害に対する指数バックオフ付きリトライ機構・タイムアウト制御の導入。
   - 外部コマンド実行における標準出力/標準エラーの非同期ドレイン（パイプ満杯によるデッドロック防止）、タイムアウト/キャンセル時のプロセスツリー強制終了（ゾンビプロセス/リソースリーク防止）、引数エスケープの安全性向上。
3. **イベント検知とリソース管理の最適化**: 定常時（イベント非発生時）のメモリ割り当て削減、HIDレポート解析処理のバッファ再利用・LINQ排除。
4. **包括的な分析レポートの作成**: `docs/performance/background_processing_analysis.md` に分析結果と改善策を詳細に記録。

---

## 2. 変更対象コンポーネントと設計

### A. `UpsMonitorEngine.cs` (`UpsMonitor.Core/Monitoring.cs`)
- **課題**: `WaitForNextPollAsync` がポーリング毎に `CancellationTokenSource.CreateLinkedTokenSource`、`Task.Delay`、`_wakeSignal.WaitAsync`、`Task.WhenAny` を生成し、通常タイムアウト時に内部で `OperationCanceledException` を発生させていた。
- **改善**: `await _wakeSignal.WaitAsync(_pollIntervalMs, cancellationToken).ConfigureAwait(false);` に置き換え。
  - `SemaphoreSlim.WaitAsync(int millisecondsTimeout, CancellationToken cancellationToken)` は .NET 標準でタイムアウトとシグナルとキャンセルの待機を単一の非同期操作として処理するため、CTS・Delay・WhenAny・例外アロケーションをゼロにする。

### B. `UpsEvents.cs` (`UpsMonitor.Core/UpsEvents.cs`)
- **課題**: `UpsEventDetector.Observe` が毎ポーリング呼び出し時に `new List<UpsEvent>()` を新規生成しており、イベントが発生しない定常時（99.9%以上の呼び出し）にもヒープ確保が発生していた。
- **改善**: イベントが検知されなかった場合は静的な空配列 `Array.Empty<UpsEvent>()` を返却し、定常時アロケーションをゼロにする。`CompositeUpsEventSink` でも LINQ `Select` アロケーションを排除。

### C. `WindowsHidUpsProvider.cs` & `UpsMonitor.Hid`
- **課題**:
  1. `WindowsHidUpsProvider.ReadSnapshotAsync` が `Task.Run` を呼び出していたが、呼び出し元の `UpsMonitorEngine` は既にバックグラウンドタスクで稼働しているため、不要な ThreadPool ディスパッチが発生していた。
  2. `HidDeviceSession.ReadInputLoopAsync` 内で割り込みレポート受信毎に `new byte[...]` をアロケーションしていた。
  3. `HidReportParser.Parse` がレポート解析毎に全ケーパビリティに対して LINQ `.Where(...).ToArray()`、ボタン群に対して `new ushort[4096]` と `.ToHashSet()` をアロケーションしていた。
  4. `UpsHidMapper.Map` が各スナップショット生成時に約30回 LINQ `.Where(...).OrderBy(...).ThenBy(...).FirstOrDefault()` を実行し、大量のイテレータとデリゲートを生成していた。
- **改善**:
  - `ReadSnapshotAsync` で直接 `session.ReadValues()` を呼び出し不要な ThreadPool キューイングを抑制。
  - `HidDeviceSession` の入力受信用バッファを再利用。
  - `HidReportParser` での配列確保に `ArrayPool<ushort>` を利用し、`HashSet` の代わりに線形探索（ボタン数は通常数個以内）を採用。
  - `UpsHidMapper` で `values` をディクショナリ/ルックアップにマッピングして $O(1)$ 検索化し、LINQ オーバーヘッドを大幅削減。

### D. `WebhookNotifier.cs` (`UpsMonitor.Infrastructure/WebhookNotifier.cs`)
- **課題**: 一時的なネットワークエラーや HTTP 5xx / 429 エラーに対してリトライを行わず即時破棄していた。また個別試行ごとのタイムアウト制御が不十分だった。
- **改善**:
  - 最大リトライ回数（デフォルト2回）と指数バックオフ（1秒、2秒）を実装。
  - 各HTTPリクエストにタイムアウト（5秒〜10秒）を設定し、キャンセルトークンと連携。
  - 一時的失敗（`HttpRequestException`、5xx、429）のみリトライし、4xx系（400 Bad Request, 404 Not Found など）は即座に終了。

### E. `CommandRunner.cs` (`UpsMonitor.Infrastructure/CommandRunner.cs`)
- **課題**:
  1. `RedirectStandardOutput = true`, `RedirectStandardError = true` に設定されているが、ストリームを読み取っていなかったため、子プロセスの出力が 4KB を超えると OS パイプバッファが溢れてプロセスが永久にブロック（デッドロック）するリスクがあった。
  2. タイムアウト時やキャンセル時に `process.Kill(entireProcessTree: true)` を呼ばずに戻るため、子プロセス（`cmd.exe` や実行中の外部スクリプト）が孤児プロセス（ゾンビ）として残り、リソースリークしていた。
  3. コマンドライン置換時のダブルクォートや特殊文字のエスケープが不完全だった。
- **改善**:
  - `process.StandardOutput.ReadToEndAsync()` および `process.StandardError.ReadToEndAsync()` を非同期でドレインし、デッドロックを完全に防止。
  - タイムアウト・キャンセル発生時（`OperationCanceledException`）およびエラー時に `process.Kill(entireProcessTree: true)` を実行してプロセスツリーを安全にクリーンアップ。
  - `{MESSAGE}` 置換時のクォートエスケープ処理を追加。

---

## 3. テスト計画
1. **既存テストの実行**:
   - `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj` で既存の全22テストがパスすることを確認。
2. **新規テストの追加**:
   - `UpsEventDetector` の定常時ゼロアロケーションテスト。
   - `UpsMonitorEngine` の高速ポーリング・シグナル即時起床・キャンセル動作テスト。
   - `WebhookNotifier` のリトライ・エラーハンドリング・タイムアウトテスト。
   - `CommandRunner` の大容量出力デッドロック防止テスト・タイムアウト時のプロセスツリー終了テスト・パラメータ置換テスト。
3. **ビルド確認**:
   - `dotnet build UpsMonitor.sln` で警告0、エラー0を確認。
