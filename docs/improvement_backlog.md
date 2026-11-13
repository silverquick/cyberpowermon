# 改善施策バックログ

最終調査日: 2026-11-13
調査基準: `6cef449` (`main`)
初回調査は 2026-08-29、`330ff76` 時点。

本書は `docs/performance/*_analysis.md` と全 `docs/*/{implementation_plan,task,walkthrough}.md` を読み、walkthrough の記載だけでなく `UpsMonitor.*` の実コードと `git log` を照合して、提案済み施策の実装状況と積み残しを整理したものである。なお、依頼背景には .NET 8 とあるが、現コードの `UpsMonitor.App/UpsMonitor.App.csproj` は `net10.0-windows` を対象としているため、以下の設計は現行コードを正としている。

## 実装状況（2026-11-13 更新）

Orca オーケストレーション（設計: Codex `gpt-5.6-sol` / 実装: Antigravity / 統合: Claude）で #1・#2・#4 を実装・main へ統合済み。
#3 は Antigravity のワークスペース信頼確認でエージェント起動がブロックされたため Codex `gpt-5.6-terra`（medium）へ切り替え、Core/Infrastructure・UI統合の2waveで実装・main へ統合済み。

| 番号 | 施策 | 状態 | 関連コミット |
|---:|---|---|---|
| 1 | カスタムアラートの一貫化 | ✅ 実装済み | Core/Infra `2ecb735`、UI `2bbd7a9` |
| 2 | Analytics タブのアクティブ判定修正 | ✅ 実装済み | `8dffed8` |
| 3 | 力率ヒートマップ | ✅ 実装済み | `0026e20`（Core/Infra + UI 統合）、Merge `148d362` |
| 4 | 日次電力量の正確化＋月次集計 | ✅ 実装済み | Core/SQLite `96e2879`（正確化は旧 API ラッパー経由で既存 UI にも反映）、月次 UI `727c9c7` |
| 5 | エクスポート全期間＋ストリーミング | ⏳ 次ラウンド | — |
| 6 | 保存済みログのフィルタ | ⏳ 次ラウンド | — |
| 7〜9 | （下記「見送り推奨」参照） | 見送り | — |

### その他の実装済み改修（#1〜#9 とは別枠、2026-09-17 以降）

`git log --first-parent --oneline 80044e9..HEAD` で確認した範囲（first-parent のマージは 7 件。docs のみのマージは表から除外）。

| 種別 | 内容 | 関連コミット |
|---|---|---|
| fix(history) | SQLite 共有キャッシュ（`Cache=Shared`）を廃止し、読み取り中の書き込みで `telemetry_samples` が `SQLITE_LOCKED` になるエラーを解消。回帰テストを追加 | `d6d9f5d`、Merge `ec500d9` |
| fix(ui) | シャットダウン自動化パネルの切替電圧範囲ラベルが、存在しないリソースキー参照で空欄になる問題を修正（既存キー `TransferRange` を参照） | `f7f1055` |
| feat(health) | 公式アプリ（CyberPower BHI 等）の健全性を日時付きで記録し、その推移から現在値を推定する改善サイクルを追加（3 モデル並走・バックテスト選択・記録時の状況の自動保存など） | `a2e973b`、Merge `c2de3ce`（`f7f1055` と同一マージ） |
| fix(health) | 健全性記録で履歴状況取得に失敗しても、起動後の履歴問い合わせ成功時に不足状況を補完（既存の記録時現在値は維持） | `9cff017` ほか、Merge `1624cbf` |
| build(version) | パッチ番号の一元管理、`0.1.XXX` 表示、CHANGELOG とバージョン運用を導入し、現在値を 0.1.016 に確定 | Merge `6cef449`、タグ `v0.1.016` |

#5・#6 は未着手のまま（「⏳ 次ラウンド」を維持）。

既知の整理事項（2026-11-13 時点で再確認）: #4 実装後に残った旧 `DailyEnergyReports`（7 日固定）コレクションは、依然として削除されていない。`UpsMonitor.App/MainViewModel.cs:423` で宣言され、2164〜2176 行付近で毎リフレッシュ `QueryDailyEnergyReportsAsync` を呼んで差分更新しているが、`MainWindow.xaml` にバインドはない（grep で確認）。#7 の分割時、またはそれ以前の単独の小修正で削除できる。

### 健全性推定（公式値推移）の運用上の注意

コード（`UpsMonitor.Core/OfficialHealthEstimator.cs`・`BatteryHealth.cs`）と UI 文字列（`Strings.ja-JP.xaml` の `OfficialRecordHelp` / `OfficialModelHelp`）から確認できる範囲:

- 記録は設定画面の「公式の健全性を記録」から行い、UPS が接続されていないと記録できない（`OfficialRecordRequiresUps`）。公式値は 0〜100 の数値、時刻は `HH:mm` 形式。
- 購入（設置）日は推移の傾きを求める補助で、公式値の記録が 1 件以上必要。
- 3 モデル（直近の傾き／購入日起点の直線／購入日起点の加速曲線）のうち、全モデルのバックテストが 3 件以上になるまでは、利用可能なら購入日起点の直線が使われる（`MinimumBacktestsForSelection = 3`）。したがって採用モデルが安定するまで記録を貯める必要がある。
- 3 モデルすべてのバックテストが 3 件以上になるには記録が 5 件以上必要（加速曲線は当てはめに 2 件を要するため、バックテスト数は記録数 − 2）。それまでは購入日起点の直線（使えなければ直近の傾き）が使われる（`OfficialHealthEstimator.cs` の `SelectModel` / `Backtest`）。
- 外挿は最後の記録を起点に行う。信頼度は最後の記録からの経過日数で決まり、14 日以内が High（記録が 1 件だけなら Medium）、60 日以内が Medium、それ以降が Low。
- 推定に使うランタイムは定常状態（60 秒・負荷差 3 ポイント以内）の値のみで、負荷のべき乗則で正規化した相対性能として扱われる。
- 推定器は記録の重複除去や同日統合をせず、すべての記録を独立した点として扱う。そのため、短い間隔で何度も記録すると、その時期の点の重みが増える。
- 記録日付は過去日も入力できる（`DatePicker`。過去・未来とも検証なし）。日付だけ指定して時刻が空なら 00:00 として扱われる。

#### 推奨する記録の運用

コードの仕組みから導いたもので、実データでの検証はしていない。

1. 最初に 1 件記録する。
2. 公式値が変わったことを確認したら、確認した時刻で 1 件記録する。
3. 値が変わらなくても、最後の記録から 14 日以内に 1 件記録する。信頼度 High を保つため。値が変わらない期間の情報も推定に入る。
4. 数時間おきのような短い間隔で何度も記録しない。点の重みが偏り、バックテストの誤差が実際より小さく見えることがある。

以前ここに書いていた「公式値が下がった日にだけ記録する」は、コード上の根拠が弱いので採らない。下がったときだけ記録すると、値が平坦だった期間の情報が入らず、傾きが急に見えるおそれがある（推測）。

#### 記録時の状況（Context）と履歴 DB の保持期間

- 記録時の状況（負荷・電力・ランタイム・電圧・直前 10 分／24 時間の統計・最終放電からの日数）は、**記録した時点で**履歴 DB に問い合わせて保存する。現在時刻から ±5 分以内の記録では、瞬間値を現在値で上書きする（`OfficialHealthContextService.CaptureAsync`）。
- 起動時の backfill は、履歴取得が未完了の記録（旧記録や、記録時に履歴取得が失敗した記録）を補完する。記録時の現在値など既存の非 null 値は維持し、不足項目だけを埋める。記録時・補完時に正常に問い合わせできた記録は、結果が空でも再試行しない（冪等）。例外や履歴ソース未利用時は未完了のまま残る。
- 履歴 DB で保持期間により削除されるのは、生サンプル（`telemetry_samples`・`raw_telemetry_values`）だけ。保持期間は `history.rawRetentionDays` で、既定 14 日、範囲は 1〜365。1 分ロールアップ、状態遷移、UPS イベント、健全性観測は削除されない（`SqliteTelemetryStore.CleanupAsync`）。
- そのため保持期間を過ぎた日付の記録でも、状況の大半は 1 分ロールアップと状態遷移から補完できる。ただし充電中フラグは取れず、電力・ランタイムも取れないことがある。履歴 DB の最古時刻より前の日付では、状況は空になる。
- 状況は推定モデルでは参照されない。後から公式値と負荷などの関係を調べるための保存用で、推定精度には影響しない。記録時の負荷条件を揃える必要もない。

#### 実装済み（2026-11-12）

- `ContextBackfillAttempted` は履歴問い合わせの正常完了を表す。`CaptureForRecordAsync` は例外・ソース未利用時には false、正常完了なら結果が空でも true を保存する。現在値だけの状況が非 null でも false なら起動時補完の対象にし、既存値が null の項目だけを埋める。設定形式・DB スキーマの変更はなく、合成 SQLite と設定の保存・再読込で、live の有無にかかわらず失敗から復旧できること、live 値の維持、正常な空結果の再問い合わせなしを回帰テストで確認した。元のコードの通常の保存経路では false の記録は状況が null だったため、既存 config の補完対象は変わらない。修正前に既にフラグが true で保存された記録は、正常完了した記録と区別できないため変更しない。

#### コード上の改善余地（未着手）

- 記録の UI に、日付だけ指定すると 00:00 になること、未来日付が入力できることへの案内・検証がない。
- 同じ値・近い時刻の記録に対して、重複の抑制も重み付けもない。偏りの大きさは、合成データ（等間隔、変化時のみ、密集）で `Estimate` とバックテストを比べる単体テストで確かめられる。
- `AppPaths.DevicesFile` と `AppPaths.UserDirectory` は未使用。削除候補。
- 推定結果はあくまで公式値の再現を目的とした推定で、UPS が HID で直接公開する値ではない（README 参照）。

## 今回スコープ推奨

#1〜#4 は実装済み。未着手の推奨施策は #5・#6。

| 番号 | 施策 | 領域 | 推奨理由 | 難易度 |
|---:|---|---|---|---|
| 1 | カスタムアラートをイベント・通知・外部連携まで一貫化 | バックグラウンド・機能 | 現状は高負荷／電圧異常時に音を鳴らすだけで、定義済みイベントが永続化・Windows 通知・Webhook に流れない。監視アプリとしての機能欠落が大きい | 中 |
| 2 | Analytics タブのアクティブ判定修正 | UI | タブ番号のずれにより Analytics 選択時の即時更新が動かず、UPS タブ選択時に Analytics クエリが走る。小変更で効果と確実性が高い | 小 |
| 3 | Analytics の力率ヒートマップ追加 | UI・SQLite・機能 | 計画・リソース文字列には力率があるが、選択肢と集計ロジックがない。既存データから派生計算できる | 中 |
| 4 | 日次電力量の正確化と月次集計の追加 | SQLite・UI・機能 | 月次レポートが未実装で、現行の日次 kWh は観測時間によらず `平均W × 24h` のため当日・欠測日の値が過大になる | 中 |
| 5 | エクスポートの「全期間」選択と大容量 JSON ストリーミング | UI・SQLite・機能 | Infrastructure には全期間 API があるが UI から到達不能。現行 JSON は全行をメモリ保持するため、そのまま公開すると長期データで危険 | 中 |
| 6 | 保存済みログの日付範囲・イベント種別フィルタ | UI・SQLite・機能 | Logs は現セッション最大 500 件だけで、計画された日付範囲・種別検索がない。SQLite にはイベント履歴と索引が既にある | 中 |

## 見送り推奨

| 番号 | 施策 | 領域 | 見送り理由 | 再検討条件 |
|---:|---|---|---|---|
| 7 | `MainViewModel` の責務分割 | アーキテクチャ | 明確な積み残しだが、現ファイルは約 3,330 行（2026-11-10 時点で 3,331 行）で、今回推奨施策の大半と競合する。先に機能ギャップをテストで固定した方が安全（2026-11-10 の検討メモ参照） | #5・#6 の統合後、タブ単位の振る舞いテストを用意できた時点 |
| 8 | ReadyToRun 発行設定の検証 | 起動 | 発行方式・配布サイズ上限・cold start 基準値が未定で、設定だけ有効化しても効果を判定できない | x64 配布形態と起動計測シナリオを確定した時点 |
| 9 | 日次レポート DataGrid の差分更新 | UI | 現在は値が変わった場合だけ更新され、件数も固定 7 行で 10 秒周期。残る `Clear` + `Add` の通知コストは小さい | 4 により表示件数が増える、または UI 計測で再描画が顕在化した場合 |

## 提案施策の仕分け結果

| 出典の提案群 | 判定 | コード確認結果 |
|---|---|---|
| 起動: SQLite 遅延初期化、`user_version` 短絡、設定初回保存の非ブロッキング化、UI 先行表示 | 実装済み | `SqliteTelemetryStore.InitializeAsync` / `EnsureInitializedAsync`、`JsonConfigurationStore.LoadAsync`、`App.OnStartup` を確認。さらに `330ff76` で v1→v2 の軽量マイグレーションも追加済み |
| UI: トレイ差分更新・GDI 解放、PropertyChanged 差分化、チャート二分探索／Brush キャッシュ、ヒートマップ直接参照、Timeline Brush 共有、影の Performance bias | 実装済み | `TrayIconManager`、`MainViewModel.SetField`、`TimeSeriesChart.FindClosestPoint`、`WeeklyHeatmapControl` の 7×24 配列、`UpsStateTimeline`、`MiniMonitorWindow.xaml` を確認 |
| SQLite: 日次 N+1 解消、複数メトリック一括取得、サグ／サージ単一走査、cleanup 索引、PRAGMA 調整 | 実装済み（機能精度の残件は 4） | `SqliteTelemetryQueries` と `SqliteTelemetryStore.ExecuteSchemaAsync` を確認。`8018f3c`、`330ff76` に履歴あり |
| バックグラウンド: ポーリング待機、イベント定常時割当、HID バッファ／Parser／Mapper、Webhook retry、CommandRunner の drain／kill | 実装済み | `Monitoring.WaitForNextPollAsync`、`UpsEventDetector.Observe`、`HidDeviceSession`、`HidReportParser`、`UpsHidMapper`、`WebhookNotifier`、`CommandRunner` を確認 |
| 機能: 日付・種別ログ、日次／月次レポート、力率 Analytics、全期間 export、カスタムアラート通知 | 一部のみ実装 | それぞれ 1、3〜6 に分解。walkthrough の完了表現より実コードを優先した |
| `MainViewModel` 責務分割 | 提案のみ | サブ ViewModel は存在せず、設定・履歴・Analytics・Logs・監視表示が単一クラスに残る（7） |
| ReadyToRun | 提案のみ | `Directory.Build.props` と `UpsMonitor.App.csproj` に `PublishReadyToRun` および publish profile はない（8） |
| `docs/ui_responsiveness_optimization/task.md` の未チェック 2 件 | 管理上の古い記載 | 「git commit」は `72c6e85` として実施・マージ済み。「`TASK.md` 末尾への完了報告」はコード施策ではなく、ルート `TASK.md` 自体が後続コミットで削除済み。実装バックログにはしない |

## 1. カスタムアラートをイベント・通知・外部連携まで一貫化

- **対象領域**: バックグラウンド・機能
- **出典**:
  - `docs/feature_enhancements/implementation_plan.md`「1. タスクトレイ・通知・アラートの強化」「4. 外部通知＆自動化連携」
  - `docs/feature_enhancements/task.md`「2.2 カスタムアラート閾値」
  - `docs/feature_enhancements/walkthrough.md`「1. タスクトレイ・通知・アラートの強化」
- **現状と課題**:
  `UpsEventType.VoltageAbnormal` と `HighLoadWarning` は定義済みだが、`UpsEventDetector.Observe` は生成しない。`MainViewModel.CheckCustomAlerts` は 30 秒の共通 cooldown とシステム音だけを実行するため、イベント DB、Logs、Windows 通知、Webhook、外部コマンドに届かない。特に `WebhookNotifyOnHighLoad` は受信対象だけがあり、発生元がない。
- **提案する実装方針**:
  - `UpsMonitor.Core/UpsEvents.cs` に immutable な `UpsAlertThresholds`（高負荷、低電圧、高電圧、復帰ヒステリシス）を追加し、`UpsEventDetector` が `HighLoadWarning` / `VoltageAbnormal` を他イベントと同じ `IReadOnlyList<UpsEvent>` に追加する。
  - 連続通知を時間 cooldown だけで抑えるのではなく、`_highLoadActive` / `_voltageAbnormalActive` の edge-trigger と復帰ヒステリシス（例: 負荷は閾値より 5pt 下、電圧は境界より 2V 内側で解除）を採用する。正常復帰後の再発は再通知する。
  - `UpsMonitor.Core/Monitoring.cs` の `UpsMonitorEngine` コンストラクタと `SetAlertThresholds` から検知器へ設定を渡す。`UpsMonitor.App/App.xaml.cs` で初期設定を注入し、`MainViewModel.SaveSettingsAsync` で動的更新する。
  - `MainViewModel.CheckCustomAlerts` は削除し、既存の `OnEventDetected` を単一経路にする。これにより File/SQLite sink、Logs、通知、音、Webhook が同じイベントを扱う。
  - `UpsMonitor.Infrastructure/AppConfiguration.cs` の `ExternalCommandConfiguration` に高負荷・電圧異常用コマンドを追加し、`MainViewModel.OnEventDetected` のコマンド選択、`MainWindow.xaml`、日英リソースを拡張する。既存 config では空文字を既定値にして後方互換を保つ。
- **影響範囲・リスク**: Core のイベント発生数が変わり、通知・DB・Webhook・コマンド実行へ波及する。閾値付近のチャタリングとアプリ起動直後の誤通知が主リスク。初回観測を基準状態とするか、異常なら初回も通知するかをテストで明示する。
- **必要なテスト**: 閾値突入の 1 回通知、異常継続中の無通知、ヒステリシス内の無復帰、正常復帰後の再発、設定動的更新、切断時の非発火、Composite sink への永続化、Webhook／コマンドのイベント振り分け。
- **難易度**: 中
- **確証度**: コード確認済み
- **依存・競合**: 7 の責務分割より先に実装する。`MainViewModel.cs` / `MainWindow.xaml` / リソースは 2〜6 と競合するため UI 統合は同一グループにする。Core 部分は 3〜6 のデータ層と並列可能。

## 2. Analytics タブのアクティブ判定修正

- **対象領域**: UI
- **出典**:
  - `docs/weekly_heatmap_analytics/walkthrough.md`「③ UI & ViewModel」（Analytics タブ選択時の自動リフレッシュ）
  - `docs/performance_optimization/implementation_plan.md`「フェーズ 2」（アクティブ画面だけを更新する方針）
- **現状と課題**:
  `MainWindow.xaml` のタブ順は Dashboard=0、History=1、UPS=2、Analytics=3 だが、`MainViewModel.SelectedNavigationIndex`、`IsWindowVisible`、`OnLanguageChanged` は Analytics を `2` と判定している。Analytics を開いても即時取得されず、UPS タブで不要な集計が走る。
- **提案する実装方針**:
  - `MainViewModel` に散在する数値比較を廃止し、少なくとも `DashboardIndex`、`HistoryIndex`、`AnalyticsIndex` の名前付き定数と `IsHistoryRefreshTarget(int)` / `IsAnalyticsRefreshTarget(int)` に集約する。
  - `SelectedNavigationIndex` setter、`IsWindowVisible` setter、`OnLanguageChanged` の 3 箇所を同じ判定関数に通す。Analytics は index 3 とし、選択時に最新 request を cancel して `RefreshAnalyticsAsync` を 1 回実行する。
  - 将来のタブ追加に強くする場合は、`MainNavigationPage` enum を `TabItem.Tag` に設定し `SelectedValuePath="Tag"` で bind する。ただし今回の最小修正は名前付き定数で十分。
- **影響範囲・リスク**: UI 内の refresh routing のみ。二重 refresh とキャンセル競合が主リスク。
- **必要なテスト**: 各 index の routing 単体テスト、Analytics 選択時 1 回取得、UPS 選択時は取得しない、非表示時は取得せず再表示時に取得、言語変更時の現在タブ再取得。
- **難易度**: 小
- **確証度**: コード確認済み
- **依存・競合**: 独立して先行可能。ただし `MainViewModel.cs` は 1、3〜7 と競合するため、同じ UI 担当がまとめて適用するのが安全。

## 3. Analytics の力率ヒートマップ追加

- **対象領域**: UI・SQLite・機能
- **出典**:
  - `docs/weekly_heatmap_analytics/implementation_plan.md`「3. Analytics タブ UI」（指標: 力率）
  - `docs/feature_enhancements/implementation_plan.md`「3. 電力・電気代・停電の高度な統計分析レポート」
- **現状と課題**:
  日英リソースには `AnalyticsMetricPowerFactor` があるが、`RefreshAnalyticsOptions` は電力・入力電圧・負荷率の 3 種だけである。`TelemetryMetric` に力率はなく、履歴グラフでは Active W と Apparent VA から UI 側で派生計算しているため、既存の週間集計 API へそのまま渡せない。
- **提案する実装方針**:
  - `UpsMonitor.Core/TelemetryHistory.cs` に保存済みメトリックと派生メトリックを区別できる `WeeklyPatternMetric` enum（ActivePower、InputVoltage、LoadPercent、PowerFactor）を追加し、`WeeklyPatternResult.Metric` と query 引数をこの型へ移す。
  - `SqliteTelemetryQueries.QueryWeeklyPatternAsync` は通常 3 指標を既存の単一 metric SQL へ map する。PowerFactor は `telemetry_rollups_1m` から同一 `bucket_utc_ms` の ActivePowerWatts と ApparentPowerVoltAmperes を条件付き集約し、各分で `clamp(W / VA * 100, 0, 100)` を算出してから曜日×時刻へ集約する。`VA <= 1` またはいずれか欠測の分は除外し、比率の平均ではなく「分ごとの比率の平均」にする。
  - `MainViewModel.RefreshAnalyticsOptions` に力率を追加し、`RefreshAnalyticsAsync` から新 enum を渡す。`WeeklyHeatmapControl` は結果の数値と `%` unit を受けるだけなので原則変更不要。
- **影響範囲・リスク**: Core の結果型と query signature が変わる。既存 DB へのスキーマ変更は不要だが、W/VA の timestamp が揃わない分をどう除外するかでサンプル数が変わる。
- **必要なテスト**: W=50/VA=100→50%、VA=0・欠測除外、100% clamp、既存履歴から 168 セル生成、3 既存指標の回帰、期間・ローカル曜日境界。
- **難易度**: 中
- **確証度**: コード確認済み
- **依存・競合**: 4、6 と `SqliteTelemetryQueries.cs` が競合し、2、4〜7 と `MainViewModel.cs` が競合する。同一データアクセス担当のグループに入れる。

## 4. 日次電力量の正確化と月次集計の追加

- **対象領域**: SQLite・UI・機能
- **出典**:
  - `docs/feature_enhancements/implementation_plan.md`「3. 日別・月別電力消費＆電気代集計」
  - `docs/performance/sqlite_query_analysis.md`「2.1 (1) QueryDailyEnergyReportsAsync」
  - `docs/feature_enhancements/walkthrough.md`「3. 電力・電気代・停電の統計分析レポート」
- **現状と課題**:
  実装は `QueryDailyEnergyReportsAsync(..., days, ...)` と固定 7 日 DataGrid のみで、月次集計はない。日次電力量は各日の `AVG(active_power_watts) * 24 / 1000` で、1 時間しか観測していない当日も 24 時間分として扱い、欠測時間も消費したことになる。既存テストは件数だけを確認し、kWh 精度を検証していない。
- **提案する実装方針**:
  - `TelemetryHistory.cs` の `DailyEnergyReportItem` を、`EnergyReportPeriod`（Day / Month）と `PeriodStart` / `PeriodEnd` を持つ汎用 `EnergyReportItem` に置き換える。移行中は旧メソッドを wrapper として残せる。
  - `SqliteTelemetryQueries.cs` に `QueryEnergyReportsAsync(deviceId, from, to, granularity, rate, token)` を追加する。長期集計は保持期限の短い `telemetry_samples` ではなく `telemetry_rollups_1m` の ActivePowerWatts を使う。
  - 各 1 分 bucket の平均 W を `value_sum / sample_count` で求め、観測された bucket だけ `avgW × 1/60h` を加算する。連続 bucket の台形積分を使う場合も、間隔を最大 1 分に clamp して欠測区間を補間しない。日／月はローカル時刻で group 化し、当日・当月は観測済み分だけを表示する。
  - 平均 W は `SUM(value_sum) / SUM(sample_count)`、ピーク W は `MAX(maximum)`、停電回数は同じ period key で一括 query し、N+1 を再導入しない。
  - `MainViewModel` に日次／月次の表示粒度と期間選択を追加し、`MainWindow.xaml` の DataGrid を汎用 period label に bind する。
- **影響範囲・リスク**: 既存ユーザーが見ている kWh・料金値が下方修正される可能性が高い。DST、月境界、欠測、現在進行中 period の扱いを仕様化する必要がある。
- **必要なテスト**: 200W を 60 連続分→約0.2kWh、1 分のみ→約0.00333kWh、長い欠測を積分しない、当日部分期間、月跨ぎ、DST 日、料金換算、停電 period 集約、30 日以上で rollup を参照すること。
- **難易度**: 中
- **確証度**: コード確認済み
- **依存・競合**: 3、6 と `SqliteTelemetryQueries.cs`、3、5〜7 と `MainViewModel.cs` / `MainWindow.xaml` が競合する。データモデル変更を先に確定する。

## 5. エクスポートの「全期間」選択と大容量 JSON ストリーミング

- **対象領域**: UI・SQLite・機能
- **出典**:
  - `docs/architecture_and_ux_improvements/implementation_plan.md`「1-5. データエクスポートの期間指定」（選択期間または全期間）
  - `docs/architecture_and_ux_improvements/walkthrough.md`「5. データエクスポート機能の拡張」
- **現状と課題**:
  `TelemetryExporter.ExportAll*` の 3 API は存在するが参照箇所がなく、UI は History の選択期間（イベントは既定 30 日）しか出力できない。また JSON export は全レコードを `List<Dictionary<string, object?>>` に積んでから serialize するため、全期間を UI へ出すとメモリ使用量が DB 件数に比例する。
- **提案する実装方針**:
  - `MainViewModel` に `ExportRangeMode`（CurrentHistoryRange / All）を追加し、History と Logs の export カードで明示選択させる。選択期間時は現行 API、全期間時は `ExportAllTelemetryCsvAsync` / `ExportAllTelemetryJsonAsync` / `ExportAllEventsCsvAsync` を呼ぶ。
  - export 開始前に `_historyStore.FlushAsync` を待ち、直前までの queue を含める。SaveFileDialog の後に status と cancellation を管理し、二重実行を抑止する。
  - `TelemetryExporter.ExportTelemetryJsonAsync` は `Utf8JsonWriter` で配列開始→reader の各行を直接書き込み→配列終了とし、`List<Dictionary<...>>` を廃止する。CSV は既に streaming なので維持する。
  - 6 のログ filter を先に実装する場合は、Logs の export に「現在の filter 範囲」と「全期間」を用意し、filter 条件を exporter/query model と共有する。
- **影響範囲・リスク**: 全期間 export は長時間 I/O と大きな出力ファイルを生む。キャンセル時の中途ファイル、DB read lock、空き容量不足が主リスク。失敗・キャンセル時は一時ファイルへ書き、成功時に move するのが安全。
- **必要なテスト**: 全期間／選択期間の境界、最新 queue の flush、大量 JSON の一定メモリ、JSON 妥当性、キャンセル時 temp cleanup、CSV/JSON 行数一致、空 DB。
- **難易度**: 中
- **確証度**: コード確認済み
- **依存・競合**: 6 の filter model と連携可能。`MainViewModel.cs` / `MainWindow.xaml` は 1〜4、6、7 と競合する。`TelemetryExporter.cs` は他施策と競合しない。

## 6. 保存済みログの日付範囲・イベント種別フィルタ

- **対象領域**: UI・SQLite・機能
- **出典**:
  - `docs/feature_enhancements/implementation_plan.md`「2. イベントログの高度な検索・フィルタ」（From / To、イベント種別、重大度、フリーテキスト）
  - `docs/architecture_and_ux_improvements/implementation_plan.md`「1-3. ログ画面のフィルタリング & 検索機能」
- **現状と課題**:
  重大度とフリーテキストは実装済みだが、`Events` は起動後に検知した最大 500 件だけで、SQLite の保存済み `ups_events` を Logs に読み戻さない。日付範囲とイベント種別の UI・query もないため、アプリ再起動後に過去障害を Logs から調査できない。
- **提案する実装方針**:
  - `TelemetryHistory.cs` に `EventQuery`（DeviceId、From、To、`IReadOnlySet<UpsEventType>`、severity、limit）と page/result 型を追加する。
  - `SqliteTelemetryQueries.QueryEventsAsync` を public store API として分離し、`device_id + timestamp` index を使って期間を絞る。event type は parameterized `IN`、severity は event type の集合へ変換する。新しい複合 index は実測で必要な場合のみ schema v3 migration として追加する。
  - 新規 `UpsMonitor.App/LogsViewModel.cs`（7 の全面分割を待たず、Logs だけ先行抽出可）に From / To preset、event type、severity、search、loading/cancellation、最大表示件数を持たせる。filter 変更は 250〜300ms debounce し、最新 request だけを反映する。
  - live event は DB 書き込み完了後に同一 identity（timestamp/type/message）で upsert し、query 再読込との重複を除く。Logs タブ（現 index 6）を開いた時に即時 query する。
  - `MainWindow.xaml` の Logs filter row に期間 preset と type ComboBox を追加する。5 の event export は同じ `EventQuery` を使用する。
- **影響範囲・リスク**: live collection と DB query の競合、重複、最大件数、古い DB の enum 値が主リスク。message のローカライズは DB 保存原文ではなく event type を優先して表示する。
- **必要なテスト**: 再起動相当の DB 読み戻し、期間境界、type/severity/search の組合せ、latest-wins cancellation、live/query 重複排除、500 件超の page/limit、未接続・device 未確定、export 条件一致。
- **難易度**: 中
- **確証度**: コード確認済み
- **依存・競合**: 3、4 と `SqliteTelemetryQueries.cs` が競合。5 と query model を共有。先行して `LogsViewModel.cs` を作る場合、7 の分割先としてそのまま利用できる。

## 7. `MainViewModel` の責務分割

> **検討メモ（2026-11-10、決定ではない）**: 以前は約 2,700 行だった `MainViewModel.cs` は 3,331 行になった（#1・#4 の UI 統合と `a2e973b` の健全性推定で +約 570 行）。増加の根拠は次のとおり。`a2e973b` だけで `MainViewModel.cs` が +571 行、`MainWindow.xaml` が +112 行。見送りを維持する論拠は、#5・#6 が同じファイルと競合する点、および `Core.Tests`（58 件）が Core/Infrastructure 中心で ViewModel の振る舞いテストが無い点。優先度を上げる論拠は、機能追加のたびに同ファイルへ数百行が加わる傾向があり、将来の分割コストが増え続ける点。折衷案として、健全性記録まわり（設定・モデル比較表）だけを先に子 VM へ切り出す案もあるが、未検証。見送り維持か優先度引き上げかは別途判断する。

- **対象領域**: アーキテクチャ
- **出典**: `docs/architecture_and_ux_improvements/implementation_plan.md`「1-6. MainViewModel の責務分割・リファクタリング」
- **現状と課題**:
  `UpsMonitor.App/MainViewModel.cs` は約 3,300 行（2026-11-13 時点。調査時点では約 2,700 行・118 KB）で、監視 snapshot 表示、履歴 query、Analytics、Logs、設定保存、export、通知、シミュレータ、バッテリー基準管理を一括している。walkthrough には専用フィルタ機能の追加はあるが、責務分割そのものは実施されていない。
- **提案する実装方針**:
  - `HistoryViewModel`（履歴 range、chart、period summary、energy report）、`AnalyticsViewModel`（指標、期間、weekly pattern）、`LogsViewModel`（6 の query/filter）、`SettingsViewModel`（設定編集・保存・通知テスト）、`BatteryHealthViewModel`（baseline 編集）へ段階的に抽出する。
  - `MainViewModel` は `UpsMonitorEngine` の lifecycle、最新 `UpsSnapshot`、タブ子 VM への snapshot/context 配布、tray/minimonitor 向け最小 projection だけを担当する。
  - 子 VM には `SqliteTelemetryStore` や設定 store を constructor injection し、`CancellationTokenSource` の所有者を明確にする。共有 mutable `AppConfiguration` の直接編集は `SettingsDraft` へコピーし、保存時に一括適用する。
  - `MainWindow.xaml` は最初は `History.*` のような nested binding へ置換し、安定後に `Views/HistoryView.xaml`、`AnalyticsView.xaml`、`LogsView.xaml`、`SettingsView.xaml` へタブ単位で抽出する。全タブ一括 rewrite は避ける。
- **影響範囲・リスク**: binding path、PropertyChanged、言語／テーマ変更、snapshot 配布順、dispose/cancellation の回帰範囲が広い。機能変更と同時に行うと原因切り分けが難しい。
- **必要なテスト**: 各子 VM の純粋単体テスト、refresh の latest-wins、保存失敗時の draft 保持、language refresh、ウィンドウ非表示時の pending snapshot、dispose 後 callback なし、主要 XAML binding smoke test。
- **難易度**: 大
- **確証度**: コード確認済み
- **依存・競合**: 1〜6 の `MainViewModel.cs` / `MainWindow.xaml` と全面競合。今回推奨施策の UI 統合後に直列実施する。6 で先行抽出した `LogsViewModel` は再利用する。

## 8. ReadyToRun 発行設定の検証

- **対象領域**: 起動・ビルド
- **出典**: `docs/performance_optimization/implementation_plan.md`「フェーズ 5: ビルド・発行設定最適化と総合テスト」
- **現状と課題**:
  計画には ReadyToRun の検証があるが、`Directory.Build.props` は version metadata のみ、`UpsMonitor.App.csproj` に `PublishReadyToRun` はなく publish profile もない。通常 build の成否だけでは cold start 改善の有無は分からない。
- **提案する実装方針**:
  - 通常 Debug/Release build へ混ぜず、`Properties/PublishProfiles/win-x64-r2r.pubxml` または明示的 MSBuild property group に R2R 比較用 profile を追加する。
  - baseline profile と R2R profile を同じ self-contained / framework-dependent 条件で publish し、process start→MainWindow `ContentRendered` の cold/warm 時間、成果物サイズ、working set を複数回比較する。
  - 改善が再現し、配布サイズ増を許容できる場合だけ正式 profile に採用する。単一ファイル化・trim は WPF reflection/resource の別リスクがあるため同時に変えない。
- **影響範囲・リスク**: x64 固定、配布サイズ増、publish 時間増、ランタイム更新との関係。開発 build には影響させない。
- **必要なテスト**: clean machine 相当の起動、cold/warm 各 10 回程度、テーマ／言語 resource load、single-instance、tray start、署名／配布経路、baseline と機能同等性。
- **難易度**: 小（評価は中）
- **確証度**: コード確認済み
- **依存・競合**: 1〜7 とファイル競合なし。起動の機能変更が落ち着いた後に独立検証できる。

## 9. 日次レポート DataGrid の差分更新

- **対象領域**: UI
- **出典**:
  - `docs/performance/ui_responsiveness_analysis.md`「2.3 DataGrid / Chart 再描画コスト」
  - `docs/ui_responsiveness_optimization/implementation_plan.md`「2.2 MainViewModel」（DailyEnergyReports 差分チェック）
- **現状と課題**:
  `SequenceEqual` により同値時の更新は抑止済みだが、差分があると `ObservableCollection.Clear()` 後に 7 回 `Add()` し、CollectionChanged と行生成が複数回発生する。現状規模では実害は小さい。
- **提案する実装方針**:
  - 4 で件数が増える場合、`IReadOnlyList<EnergyReportItem>` property の参照を一度だけ差し替えて `PropertyChanged` 1 回にする。行選択維持が必要なら key（period start）で既存行を reconcile する。
  - 独自 `ObservableRangeCollection` はアプリ全体で range update の需要が増えた場合だけ導入し、単用途の抽象化は避ける。
- **影響範囲・リスク**: DataGrid の選択・スクロール位置が collection replacement で失われる可能性がある。
- **必要なテスト**: 同値時無通知、差分時 1 通知、選択維持、月次／多数行、言語変更時の表示。
- **難易度**: 小
- **確証度**: コード確認済み
- **依存・競合**: 4 と同じ `MainViewModel.cs` / `MainWindow.xaml` を触るため、4 の一部として必要性を再評価する。

## 実装フェーズの分割案

| フェーズ | 施策 | 進め方 | 主な所有ファイル／競合 |
|---|---|---|---|
| A: 低リスク先行修正 | 2 | 単独で先行。挙動を直して回帰テストを固定 | `MainViewModel.cs`（UI 統合フェーズ開始前に完了） |
| B1: イベント基盤 | 1 の Core / Infrastructure 部分 | B2 と並列可能 | `UpsEvents.cs`, `Monitoring.cs`, `AppConfiguration.cs` |
| B2: データ query 基盤 | 3、4、6 の Core / SQLite 部分 | 同じ `SqliteTelemetryQueries.cs` を触るため 1 グループ内で直列。B1、B3 と並列可能 | `TelemetryHistory.cs`, `SqliteTelemetryQueries.cs`, `SqliteTelemetryStore.cs`, tests |
| B3: Export 基盤 | 5 の streaming/temp-file 部分 | B1、B2 と並列可能 | `TelemetryExporter.cs`, tests |
| C: UI 統合 | 1、3、4、5、6 | 同一担当が直列統合。MainWindow と MainViewModel の競合を一箇所に閉じ込める。順序は 1→3→4→6→5 | `MainViewModel.cs`, `MainWindow.xaml`, `Resources/Strings.*.xaml`, `App.xaml.cs` |
| D: 構造整理 | 7（必要なら 9 を併合） | A〜C の機能テストが揃ってから直列。タブ単位に小さく移行 | `MainViewModel.cs`, `MainWindow.xaml`, 新規 child VM / View |
| E: 発行評価 | 8 | D と並列可能だが、採否は計測後に決定 | `UpsMonitor.App.csproj`, publish profile, 計測手順 |

並列化の要点は、B1（Core event）、B2（SQLite query）、B3（Exporter）を別担当にし、`MainViewModel.cs` / `MainWindow.xaml` を触る C を一つの統合グループへ集約することである。3・4・6 はすべて `SqliteTelemetryQueries.cs` を変更するため分割せず、7 は全 UI 施策と競合するため最後に回す。
