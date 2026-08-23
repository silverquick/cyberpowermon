# 実装計画: アプリ軽量化・パフォーマンス最適化

## 1. 概要
本計画は、UPS 監視アプリ (PowerGuard) を常時稼働のバックグラウンド監視ツールとしてさらに極限まで軽量化し、CPU・メモリ（GC 負荷）・ディスク I/O を最小化することを目的とします。

---

## 2. 最適化対象と方針

### フェーズ 1: トレイ常駐時（ウィンドウ非表示時）の UI 処理スキップ
- **現状の課題**:
  ウィンドウが非表示（トレイ格納中や最小化中）でも、毎秒 `MainViewModel.ApplySnapshot()` で 40 個以上のプロパティ変更通知、テレメトリ ViewModel の全件生成、UI 文字列構築が実行されている。
- **改善方針**:
  - `MainWindow` の表示状態（`IsVisible`）を `MainViewModel.IsWindowVisible` に連携。
  - 非表示時はトレイアイコン用文字列、ツールチップ、イベント判定、DB 書き込みのみを行い、重い UI バインディング更新と詳細 VM の構築をスキップ。
  - 最新のスナップショットを `_pendingSnapshot` に保持し、ウィンドウが再表示された瞬間に 1 回だけ即時適用。

### フェーズ 2: 履歴グラフ定期クエリのオンデマンド化
- **現状の課題**:
  `ApplySnapshot()` 内で 10 秒おきに SQLite から 7 種類以上のグラフデータ（数千件のサンプル）を再取得・再集計する `RefreshHistoryAsync()` が走っている。これは履歴画面を開いていない時でも動作する。
- **改善方針**:
  - `MainWindow.xaml` のナビゲーション TabControl と連携し、現在「履歴」タブが表示されているか（`IsHistoryActive`）を監視。
  - 「ウィンドウが表示中」かつ「履歴タブがアクティブ」な場合のみ 10 秒定期更新を行う。
  - 履歴タブに切り替わったタイミングで即時更新を実行。

### フェーズ 3: 毎秒のメモリ割り当て（GC 負荷）の削減
- **現状の課題**:
  1. `snapshot.Telemetry.Select(item => new UpsTelemetryViewModel(item)).ToArray()` で毎秒 80 個以上の VM と配列をアロケーション。
  2. `HidDeviceSession.ReadValues()` で Feature report の `byte[]` を毎秒 new。
  3. `HidDeviceSession.ResolveIndexedString()` で静的文字列（Vendor, Chemistry等）を毎秒 Win32 API 経由で取得 & StringBuilder アロケーション。
- **改善方針**:
  1. `UpsTelemetryViewModel` を In-place 更新可能にし、アイテム数や Key が変わらない限り既存インスタンスのプロパティを更新。
  2. Feature report 用の読み取りバッファを事前確保して使い回す。
  3. インデックス文字列をキャッシュし、同じ Index に対しては API コールと文字列生成を行わない。

### フェーズ 4: SQLite 書き込み・ディスク I/O の最適化
- **現状の課題**:
  1 秒ごとに `INSERT INTO telemetry_samples` と、10 回の `INSERT ... ON CONFLICT DO UPDATE`（1 分ロールアップ更新）をトランザクション実行している。
- **改善方針**:
  - `telemetry_rollups_1m` は 1 分単位の集約データであるため、同一分内の min/max/sum/count をインメモリ（`Dictionary<TelemetryMetric, RollupAccumulator>`）で集約。
  - 毎秒の 10 回の SQL 実行を廃止し、1 分の変わり目、または Flush/Dispose 時にまとめて 1 回書き込む。
  - 毎秒の DB コマンド発行数を大幅削減し、ディスク書き込み負荷と CPU 負荷を軽減。

### フェーズ 5: ビルド・発行設定最適化と総合テスト
- ReadyToRun (R2R) 設定などの検証。
- 単体テスト（`UpsMonitor.Core.Tests`）の実行確認。
- 実際の起動・トレイ格納・履歴表示・データ整合性の動作確認。

---

## 3. 動作確認・検証方法
1. `dotnet test` または `UpsMonitor.Core.Tests` によるテスト実行。
2. アプリのビルド・通常起動およびトレイ起動の確認。
3. トレイ格納時とウィンドウ表示時の動作・リソース消費の差異確認。
4. 履歴グラフの表示・更新が正常に動作するかの確認。
