# タスクリスト: アプリ軽量化・パフォーマンス最適化

## フェーズ 1: トレイ格納時（ウィンドウ非表示時）の UI 処理スキップ & 遅延更新
- [x] `MainViewModel` に `IsWindowVisible` プロパティを追加
- [x] `MainWindow` の表示/非表示・最小化イベント（`IsVisibleChanged`, `StateChanged`）と ViewModel の同期
- [x] `ApplySnapshot` 内で、非表示時はトレイ/通知/DB記録に必要な最小限の処理のみ行い、UI プロパティ更新・VM生成を保留（Dirtyフラグ化）
- [x] ウィンドウ再表示時に保留スナップショットを即時適用する処理の実装

## フェーズ 2: 履歴グラフ定期クエリのオンデマンド化
- [x] `MainViewModel` に現在選択されているタブの追跡（`SelectedNavigationIndex`）を追加
- [x] `MainWindow.xaml` の TabControl 選択状態とバインディング連携
- [x] Dashboard / History タブがアクティブかつウィンドウ表示中の場合のみ 10 秒定期更新を実行するように制御
- [x] 履歴タブに切り替わった際の即時更新処理の実装

## フェーズ 3: 毎秒のメモリ割り当て（GC 負荷）の削減
- [x] `UpsTelemetryViewModel` の In-place 更新化（既存インスタンスのプロパティを更新し、配列・VMの毎秒 new を廃止）
- [x] `HidDeviceSession` の Feature report 読み取り用バッファの再利用（毎秒の `byte[]` 確保を廃止）
- [x] `HidDeviceSession.ResolveIndexedString` のインデックス文字列キャッシュ（Win32 API 呼び出しと文字列生成の削減）

## フェーズ 4: SQLite 書き込み・ディスク I/O の最適化
- [x] `telemetry_rollups_1m` のロールアップ集約をインメモリ化（毎秒 10 回の `UPSERT` を廃止し、1 分周期で集約書き込み）
- [x] SQLite 書き込みチャネル・トランザクション処理の最適化（Flush / Dispose での確実な書き込み）

## フェーズ 5: ビルド・発行設定の最適化と総合テスト
- [x] 単体テスト（`UpsMonitor.Core.Tests`）の実行・パス確認
- [x] 複数バケットまたぎのロールアップ集約テストの追加・検証
- [x] 修正内容の確認（`walkthrough.md`）の作成
