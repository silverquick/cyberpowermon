# 軽量化・パフォーマンス最適化の確認 (Walkthrough)

## 実施した最適化の概要

常時稼働するバックグラウンド監視ツールとしての省リソース性を極限まで高めるため、以下の 4 つの主要領域で軽量化チューニングを実施しました。

---

## 1. トレイ常駐時（ウィンドウ非表示時）の UI 処理スキップ & 遅延更新
- **変更ファイル**:
  - [MainViewModel.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs)
  - [MainWindow.xaml.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml.cs)
- **改善内容**:
  - `MainWindow` の表示状態（表示 / 最小化 / トレイ格納）を `MainViewModel.IsWindowVisible` に同期。
  - ウィンドウ非表示時は、トレイアイコン・ツールチップ・イベント通知・DB 記録に必要な最低限の処理のみを行い、50 個以上の UI バインディングプロパティ発火や詳細 ViewModel の生成を完全にスキップ。
  - 最新のスナップショットを `_pendingSnapshot` として保持し、ウィンドウが再表示された瞬間に 1 回だけ即時適用。
- **効果**:
  - バックグラウンド常駐中（トレイ格納中）の CPU 使用率および UI スレッド負荷がほぼゼロになります。

---

## 2. 履歴グラフ定期クエリのオンデマンド・アクティブ制御
- **変更ファイル**:
  - [MainViewModel.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs)
  - [MainWindow.xaml](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml)
- **改善内容**:
  - ナビゲーションの選択タブ（`SelectedNavigationIndex`）を ViewModel と同期。
  - 10 秒おきの SQLite 履歴データ再集計クエリ（`RefreshHistoryAsync`）を、「ウィンドウが表示中」かつ「Dashboard (0) または History (1) タブがアクティブ」な場合のみ実行するように制限。
  - 他のタブ（UPS, Actions, Logs, Settings）選択中やトレイ格納中はクエリを停止し、タブ切り替え時に即時リフレッシュ。
- **効果**:
  - 不要な SQLite 読み取り負荷・集計負荷・メモリ確保を大幅に削減。

---

## 3. 毎秒のヒープ割り当て・GC 負荷の削減 (In-place 更新 & バッファ再利用)
- **変更ファイル**:
  - [MainViewModel.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs)
  - [HidDeviceSession.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Hid/HidDeviceSession.cs)
- **改善内容**:
  - **テレメトリ ViewModel**: `UpsTelemetryViewModel` を `INotifyPropertyChanged` 対応にし、毎秒の全件 `new` を廃止。既存インスタンスのプロパティを In-place で更新する方式に変更。
  - **HID Feature Report バッファ**: 毎秒の `new byte[...]` 割り当てを廃止し、セッション内で事前確保したバッファを再利用。
  - **インデックス文字列キャッシュ**: デバイス名やバッテリー化学タイプなどの静的文字列を `ConcurrentDictionary` にキャッシュし、毎秒の Win32 API 呼び出しと文字列生成を排除。
- **効果**:
  - 1 秒あたり数十〜百個以上のオブジェクト生成と GC (Gen0) 発生頻度が大幅に減少。

---

## 4. SQLite ロールアップ集約のインメモリ化 & ディスク I/O 削減
- **変更ファイル**:
  - [SqliteTelemetryStore.cs](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/SqliteTelemetryStore.cs)
- **改善内容**:
  - 1 分間ロールアップ（`telemetry_rollups_1m`）の min / max / sum / count 集約をインメモリの `RollupAccumulator` で処理。
  - 毎秒 10 回実行されていた `INSERT ... ON CONFLICT DO UPDATE` を廃止し、1 分の変わり目、または Flush / Dispose 時にまとめて 1 回書き込む方式に変更。
- **効果**:
  - 毎秒の SQL 実行回数が 12 回から 1〜2 回へ激減。
  - 1 時間あたりの SQL 実行回数が約 36,000 回削減され、ディスク I/O とトランザクション負荷を大幅に軽減。

---

## 5. テスト結果

`UpsMonitor.Core.Tests` を実行し、全 17 件の単体テストがすべて正常に PASS することを確認しました：

```text
PASS Power state priority
PASS Power loss and restore events
PASS Alarm edge events
PASS Disconnect and reconnect events
PASS Invalid charge is rejected, not clamped
PASS Percentage capacities are not physical SOH
PASS Physical capacity ratio calculates SOH
PASS Runtime baseline calculates comparable-load SOH
PASS Current baseline reports relative trend only
PASS Relative runtime decline requests a battery check
PASS Known BHI anchors the runtime estimate
PASS Missing baseline leaves health unknown
PASS Hard battery failures override score
PASS Self-test failure requests a battery check
PASS SQLite history stores samples, rollups, events, and health
PASS Event severity classification
PASS Telemetry and event export to CSV/JSON
17/17 tests passed.
```
