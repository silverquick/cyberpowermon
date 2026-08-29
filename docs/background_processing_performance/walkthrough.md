# 改良内容の確認 (Walkthrough): バックグラウンド処理・テレメトリ収集パフォーマンス改善

## 1. 実施内容の概要
本作業では、cyberpowermon のバックグラウンド処理、UPS HID デバイスポーリング、テレメトリ収集、イベント検知、Webhook 通知、外部コマンド実行におけるパフォーマンス分析と最適化を実施しました。

---

## 2. 主な変更点

### 2.1 ポーリング待機およびイベント処理のゼロアロケーション化
- **[`UpsMonitor.Core/Monitoring.cs`](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/perf-background/UpsMonitor.Core/Monitoring.cs)**:
  - `WaitForNextPollAsync` を `SemaphoreSlim.WaitAsync(_pollIntervalMs, cancellationToken)` による単一待機処理へリファクタリング。
  - 毎ポーリング発生していた `CancellationTokenSource`、`Task.Delay`、`Task.WhenAny` のオブジェクト確保および `OperationCanceledException` の内部例外スロー/キャッチを根絶。
  - `CompositeUpsEventSink` の LINQ `Select` による一時配列確保を排除。
  - `PublishAsync` でイベント未発生時のループ・デリゲート確保をスキップ。
- **[`UpsMonitor.Core/UpsEvents.cs`](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/perf-background/UpsMonitor.Core/UpsEvents.cs)**:
  - `UpsEventDetector.Observe` でイベント未検知時（定常時99.9%以上）に `Array.Empty<UpsEvent>()` を返却し、毎秒の `new List<UpsEvent>()` ヒープ割り当てをゼロ化。

### 2.2 HID 通信・レポート解析・テレメトリマッピングの高速化
- **[`UpsMonitor.Hid/WindowsHidUpsProvider.cs`](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/perf-background/UpsMonitor.Hid/WindowsHidUpsProvider.cs)**:
  - `ReadSnapshotAsync` で `Task.Run` の不要なスレッドプールディスパッチを排除し、直接 `session.ReadValues()` を呼び出し。
- **[`UpsMonitor.Hid/HidDeviceSession.cs`](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/perf-background/UpsMonitor.Hid/HidDeviceSession.cs)**:
  - `ReadInputLoopAsync` で割り込み入力レポート受信用バッファを再利用し、パケット受信毎の `new byte[...]` 割り当てを削減。
- **[`UpsMonitor.Hid/HidReportParser.cs`](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/perf-background/UpsMonitor.Hid/HidReportParser.cs)**:
  - レポート解析時の LINQ `.Where(...).ToArray()` を排除しインデックス付きループで走査。
  - ボタン群解析で `ArrayPool<ushort>.Shared` を使用し、最大 8KB の配列確保および `ToHashSet()` アロケーションを排除。
- **[`UpsMonitor.Hid/UpsHidMapper.cs`](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/perf-background/UpsMonitor.Hid/UpsHidMapper.cs)**:
  - `Map` 内の約30回に及ぶ `values.Where(...).OrderBy(...).ThenBy(...).FirstOrDefault()` による LINQ 走査を、`(UsagePage, Usage)` キーによる事前インデックス化 ($O(1)$) とループ走査に刷新。

### 2.3 Webhook 通知および外部コマンド実行の耐障害性・安全性向上
- **[`UpsMonitor.Infrastructure/WebhookNotifier.cs`](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/perf-background/UpsMonitor.Infrastructure/WebhookNotifier.cs)**:
  - 一時的な通信障害（ネットワーク例外、HTTP 5xx、429、タイムアウト）に対する最大 2 回の指数バックオフ（1秒、2秒）付き非同期リトライを実装。
  - 各試行に 8 秒の個別タイムアウト制御（キャンセルトークン連携）を付与。
- **[`UpsMonitor.Infrastructure/CommandRunner.cs`](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/perf-background/UpsMonitor.Infrastructure/CommandRunner.cs)**:
  - 標準出力・標準エラー出力を非同期（`ReadToEndAsync`）でドレインし、4KB 以上の出力による OS パイプバッファ枯渇デッドロックを根絶。
  - タイムアウト・キャンセル・エラー発生時に `process.Kill(entireProcessTree: true)` を確実に呼び出し、孤児プロセス（ゾンビプロセス）とハンドルリークを防止。
  - `{MESSAGE}` 置換時のダブルクォート・改行文字のエスケープ処理を実装。

---

## 3. テストと検証結果

### 3.1 単体テスト実行結果
- テストコマンド: `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`
- 結果: **全26テスト パス (26/26 passed)**
  - `Event detector zero-allocation when quiet` (新規追加: 定常時ゼロアロケーション検証)
  - `Command runner execution, large output, and escaping` (新規追加: 大容量出力パイプドレイン・エスケープ・タイムアウトツリー強制終了検証)
  - `Webhook notifier validation` (新規追加: URLバリデーション検証)
  - `Polling engine lifecycle and interval` (新規追加: ポーリング間隔バリデーション・ライフサイクル検証)
  - 既存の全22テストもすべて PASS

### 3.2 ソリューションビルド結果
- ビルドコマンド: `dotnet build UpsMonitor.sln`
- 結果: **0 個の警告, 0 エラー (正常終了)**

### 3.3 Probe 実行結果
- 実行コマンド: `dotnet run --project .\UpsMonitor.Probe\UpsMonitor.Probe.csproj -- --health`
- 結果: 正常終了（バッテリー健全性・テレメトリ統計・イベント履歴レポート正常出力）
