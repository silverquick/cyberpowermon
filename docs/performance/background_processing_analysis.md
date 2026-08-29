# バックグラウンド処理・リアルタイムテレメトリ収集パフォーマンス分析レポート

## 1. エグゼクティブサマリー
cyberpowermon は WPF/.NET 10 をベースとした常駐型の UPS 監視デスクトップアプリケーションです。
本レポートでは、バックグラウンドでのリアルタイムテレメトリ収集、HID デバイス通信、イベント検知、Webhook 通知、外部コマンド実行におけるパフォーマンス、リソース効率、耐障害性、メモリリークのリスクについて詳細なコード分析を実施し、その改善方針をまとめました。

---

## 2. 調査対象と観点

### 調査対象コンポーネント
1. **ポーリングおよび監視エンジン**: `UpsMonitor.Core/Monitoring.cs` (`UpsMonitorEngine`), `UpsMonitor.Probe`
2. **HID 通信およびデータマッピング**: `UpsMonitor.Hid/WindowsHidUpsProvider.cs`, `HidDeviceSession.cs`, `HidReportParser.cs`, `UpsHidMapper.cs`
3. **イベント検知およびシンク**: `UpsMonitor.Core/UpsEvents.cs` (`UpsEventDetector`), `UpsMonitor.Infrastructure/FileUpsEventSink.cs`
4. **外部連携・通知**: `UpsMonitor.Infrastructure/WebhookNotifier.cs`, `UpsMonitor.Infrastructure/CommandRunner.cs`
5. **ストレージ連携**: `UpsMonitor.Infrastructure/SqliteTelemetryStore.cs`

### 分析の 4 大観点
1. **UPSデバイスポーリングの間隔・スレッド/タスク使用効率**
2. **Webhook送信やコマンド実行の非同期化・エラー時リトライ・タイムアウト処理**
3. **イベント発火時のリソース確保・解放および GC プレッシャー**
4. **メモリリークおよび孤児プロセス・ハンドルリークの可能性**

---

## 3. 発見された課題と詳細分析

### 3.1 ポーリング待機ループにおける不要オブジェクト確保と例外発生
- **現状コード**:
  `UpsMonitorEngine.WaitForNextPollAsync` において、毎回のポーリング周期（デフォルト 1 秒、最小 250ms）ごとに以下のリソースが生成されていました。
  - `CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)`
  - `Task.Delay(_pollIntervalMs, ...)`
  - `_wakeSignal.WaitAsync(...)`
  - `Task.WhenAny(...)`
  - `waitCancellation.CancelAsync()` による内部的な `OperationCanceledException` のスローおよび catch
- **影響**:
  250ms 周期の場合、毎秒 16 個以上の Task / CTS オブジェクトが Gen0 ヒープに確保され、毎秒 4 回以上の例外オブジェクトがスロー/キャッチされていました。常駐アプリとして CPU サイクルと GC 負荷の無駄が発生していました。
- **改善方針**:
  .NET の `SemaphoreSlim.WaitAsync(int millisecondsTimeout, CancellationToken cancellationToken)` を直接利用。単一のネイティブ非同期メソッドでタイムアウト、シグナル起床、キャンセルの全てをゼロアロケーションで待機可能にします。

### 3.2 HID レポート解析およびマッピングにおけるメモリ・CPU オーバーヘッド
- **現状コード**:
  1. `WindowsHidUpsProvider.ReadSnapshotAsync`: 呼び出し元がすでにスレッドプール上で動作しているにもかかわらず `Task.Run(session.ReadValues, ...)` を呼び出し、不要な ThreadPool キューイングとコンテキストスイッチを発生。
  2. `HidDeviceSession.ReadInputLoopAsync`: 割り込み入力レポートの読み取りループ内で `new byte[_descriptor.InputReportByteLength]` を毎パケット確保。
  3. `HidReportParser.Parse`: レポート解析毎に `descriptor.Capabilities.Where(...).ToArray()`、ボタン解析時に `new ushort[4096]`（8KB）のヒープ確保および `ToHashSet()` によるハッシュセット確保。
  4. `UpsHidMapper.Map`: 1 回のスナップショット生成内で `Get()` が約 30 回呼び出され、その都度 `values.Where(...).OrderBy(...).ThenBy(...).FirstOrDefault()` による LINQ 走査・ソート・デリゲート確保が実行。
- **影響**:
  高頻度ポーリング時に GC Gen0 コレクション頻度が増大し、CPU 使用率が微増する原因となっていました。
- **改善方針**:
  - `ReadSnapshotAsync` で直接 `session.ReadValues()` を呼び出し。
  - `HidDeviceSession` で受信バッファを再利用。
  - `HidReportParser` で `ArrayPool<ushort>` の活用および線形探索化（ボタン数は通常数個以内）。
  - `UpsHidMapper` で `values` をディクショナリ等のインデックス構造に変換し $O(1)$ 参照化。

### 3.3 イベント検知における定常時ヒープ割り当て
- **現状コード**:
  `UpsEventDetector.Observe` はスナップショットを受け取るたびに `new List<UpsEvent>()` を生成。イベントが発生しない通常稼働時（99.9% 以上）であっても空の `List<UpsEvent>` がヒープに生成され続けていました。
- **改善方針**:
  イベント未検知時は `Array.Empty<UpsEvent>()` を返却するゼロアロケーション設計に変更。

### 3.4 Webhook 送信の耐障害性とタイムアウト制御の欠如
- **現状コード**:
  `WebhookNotifier.SendNotificationAsync` は単発の `HttpClient.PostAsync` のみで、一時的なネットワーク断（WiFi 不安定、DNS 遅延、HTTP 502/503、レートリミット 429）が発生した場合、停電（`PowerLost`）やバッテリー低下などの重要アラートが 1 回の通信失敗で永久に消失していました。
- **改善方針**:
  一時的エラー（ネットワーク例外、5xx、429）に対して最大 2 回の指数バックオフ（1 秒、2 秒）付き非同期リトライを実装。各試行にタイムアウト制御を付与。

### 3.5 外部コマンド実行におけるパイプデッドロックと孤児プロセスリーク
- **現状コード**:
  1. `ProcessStartInfo` で `RedirectStandardOutput = true`, `RedirectStandardError = true` と設定しているにもかかわらず、プロセス実行中に標準出力・標準エラー出力を読み取っていなかった。外部スクリプトの出力が OS パイプバッファ（4KB）を超えた場合、子プロセスがパイプ書き込み待ちでデッドロックし 30 秒タイムアウトまでフリーズする。
  2. タイムアウト時やキャンセル発生時に `process.Kill(entireProcessTree: true)` が呼ばれていなかったため、`cmd.exe` や背後で起動されたバッチ/スクリプトがバックグラウンドにゾンビプロセスとして永久に残留し、メモリとハンドルをリークしていた。
  3. `{MESSAGE}` 置換でメッセージ内のダブルクォートのエスケープが不完全であった。
- **改善方針**:
  - 標準出力・標準エラーを `ReadToEndAsync` で非同期ドレインし、デッドロックを根絶。
  - タイムアウト・キャンセル・エラー発生時に `process.Kill(entireProcessTree: true)` を確実に呼び出し、孤児プロセスを完全に防止。
  - 特殊文字・ダブルクォートのエスケープ処理を追加。

---

## 4. 改善策のベンチマーク・効果予測

| 項目 | 改善前 | 改善後 | 期待効果 |
| :--- | :--- | :--- | :--- |
| **ポーリング待機 (`WaitForNextPollAsync`)** | CTS + Delay + WhenAny + 例外 (毎秒 4+ alloc) | `_wakeSignal.WaitAsync` (0 alloc) | CPU 負荷低減、GC Gen0 割り当て削減 |
| **イベント検知 (`UpsEventDetector`)** | 毎周期 `new List<UpsEvent>()` | 定常時 `Array.Empty<UpsEvent>()` (0 alloc) | 定常稼働時のヒープ割り当てゼロ化 |
| **HID レポート解析 (`HidReportParser`)** | 毎レポート LINQ + 8KB 配列 + HashSet | プール配列 + 線形探索 | レポート解析時のメモリ確保を 90% 以上削減 |
| **HID テレメトリマッピング (`UpsHidMapper`)** | 30 回以上の LINQ 検索・ソート走査 | $O(1)$ インデックス参照 | スナップショット生成速度向上 |
| **Webhook 通知 (`WebhookNotifier`)** | 単発送信 (失敗時即破棄) | 指数バックオフ付きリトライ (最大 2 回) | 停電等の重要通知の到達率・信頼性向上 |
| **外部コマンド (`CommandRunner`)** | 4KB 出力でデッドロック、タイムアウト時ゾンビ残留 | 非同期ドレイン + プロセスツリー強制終了 | デッドロック防止、孤児プロセス/ハンドルリーク根絶 |

---

## 5. 結論
本分析に基づき、コード修正を実施してビルドおよび全単体テストをパスさせることで、cyberpowermon のバックグラウンド処理の信頼性・パフォーマンスを大幅に向上させます。
