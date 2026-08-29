# タスク: バックグラウンド処理・リアルタイムテレメトリ収集パフォーマンス分析と改良

【対象】cyberpowermon (WPF/.NET UPS監視アプリ) のバックグラウンド処理・リアルタイムテレメトリ収集パフォーマンス分析と改良。

【調査対象ファイル】
- UpsMonitor.Probe, UpsMonitor.Hid 配下のポーリング処理
- UpsMonitor.Infrastructure/WebhookNotifier.cs
- UpsMonitor.Infrastructure/CommandRunner.cs
- UpsMonitor.Core/UpsEvents.cs

【観点】
- UPSデバイスポーリングの間隔・スレッド/タスク使用効率
- Webhook送信やコマンド実行の非同期化・エラー時リトライ・タイムアウト処理
- イベント発火時のリソース確保・解放
- メモリリークの可能性

【成果物】
1. `docs/performance/background_processing_analysis.md` に分析結果を記載
2. 実際にコードを改良し、`UpsMonitor.sln` のビルドと `UpsMonitor.Core.Tests` の全テストがパスすることを確認
   - ビルド: `dotnet build UpsMonitor.sln`
   - テスト: `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`
3. 変更は `git commit` しておくこと(コミットメッセージは日本語で分かりやすく)

【注意】
- UI描画処理・起動処理・SQLiteクエリ内部は別タスクが担当するため深入りしないこと。
- 既存機能を壊さないこと。
- 作業が完了したら、この TASK.md の末尾に完了報告を追記すること。

---

## 完了報告

### 実施概要
バックグラウンド処理・リアルタイムテレメトリ収集・イベント検知・Webhook通知・外部コマンド実行のパフォーマンス分析および最適化作業が正常に完了しました。

### 主な実施内容
1. **パフォーマンス分析レポート作成**:
   - `docs/performance/background_processing_analysis.md` に4大観点（ポーリング効率、非同期通知/コマンド耐障害性、イベント検知GC負荷、メモリ/プロセスリーク）の分析結果と改善策を記録。
2. **ポーリングおよびイベント処理のゼロアロケーション化**:
   - `UpsMonitorEngine.cs`: `WaitForNextPollAsync` を `SemaphoreSlim.WaitAsync` 直接呼び出しに刷新し、毎秒の Task/CTS/例外アロケーションを根絶。
   - `UpsEvents.cs`: イベント未発生時に `Array.Empty<UpsEvent>()` を返却し、定常時の毎秒 List 割り当てをゼロ化。`CompositeUpsEventSink` の LINQ 割り当ても最適化。
3. **HID 通信およびテレメトリマッピングの最適化**:
   - `WindowsHidUpsProvider.cs`: `ReadSnapshotAsync` での不要な `Task.Run` ディスパッチを排除。
   - `HidDeviceSession.cs`: 割り込み入力レポートの受信用バッファを再利用。
   - `HidReportParser.cs`: LINQ `.Where(...).ToArray()` を排除、ボタン群解析に `ArrayPool<ushort>` を適用し 8KB のヒープ確保と `ToHashSet()` を排除。
   - `UpsHidMapper.cs`: 約30回の LINQ 検索走査を `(UsagePage, Usage)` の $O(1)$ ディクショナリルックアップに刷新。
4. **Webhook 通知・外部コマンド実行の耐障害性・安全性向上**:
   - `WebhookNotifier.cs`: 一時的エラー（ネットワーク障害、HTTP 5xx、429）に対する指数バックオフ付き非同期リトライ（最大2回）および個別タイムアウト制御を実装。
   - `CommandRunner.cs`: 標準出力・標準エラーの非同期ドレインによるパイプ枯渇デッドロック防止、タイムアウト/キャンセル時のプロセスツリー強制終了（ゾンビプロセス/ハンドルリーク防止）、引数エスケープ処理を実装。
5. **テストと検証**:
   - `UpsMonitor.Core.Tests` に4件のテストを追加（全26テストが 100% パス）。
   - `UpsMonitor.sln` のビルド正常終了（0警告・0エラー）。
   - `UpsMonitor.Probe --health` の正常実行を確認。

