# 改善施策バックログ

最終調査日: 2026-11-17
調査基準: `74ebc10` (`main`)
初回調査は 2026-08-29、`330ff76` 時点。

本書は `docs/performance/*_analysis.md` と全 `docs/*/{implementation_plan,task,walkthrough}.md` を読み、walkthrough の記載だけでなく `UpsMonitor.*` の実コードと `git log` を照合して、提案済み施策の実装状況と積み残しを整理したものである。なお、依頼背景には .NET 8 とあるが、現コードの `UpsMonitor.App/UpsMonitor.App.csproj` は `net10.0-windows` を対象としているため、以下の設計は現行コードを正としている。

## 実装状況（2026-11-17 更新）

Orca オーケストレーション（設計: Codex `gpt-5.6-sol` / 実装: Antigravity / 統合: Claude）で #1・#2・#4 を実装・main へ統合済み。
#3 は Antigravity のワークスペース信頼確認でエージェント起動がブロックされたため Codex `gpt-5.6-terra`（medium）へ切り替え、Core/Infrastructure・UI統合の2waveで実装・main へ統合済み。

| 番号 | 施策 | 状態 | 関連コミット |
|---:|---|---|---|
| 1 | カスタムアラートの一貫化 | ✅ 実装済み | Core/Infra `2ecb735`、UI `2bbd7a9` |
| 2 | Analytics タブのアクティブ判定修正 | ✅ 実装済み | `8dffed8` |
| 3 | 力率ヒートマップ | ✅ 実装済み | `0026e20`（Core/Infra + UI 統合）、Merge `148d362` |
| 4 | 日次電力量の正確化＋月次集計 | ✅ 実装済み | Core/SQLite `96e2879`（正確化は旧 API ラッパー経由で既存 UI にも反映）、月次 UI `727c9c7` |
| 5 | エクスポート全期間＋ストリーミング | ✅ 実装済み（0.1.018） | `feature/export-all-range-streaming` |
| 6 | 保存済みログのフィルタ | ✅ 実装済み（0.1.017） | `feature/logs-saved-event-filter` |
| 7〜9 | （下記「見送り推奨」参照） | 見送り | — |

### 固定日本語の表示修正（実装済み、0.1.026）

`fix/remaining-ui-i18n`: Dashboard の残容量・電圧マージン・セル数、History の電気代要約／概算列、通知テストを日英リソースへ移行。概算列は言語を再評価の入力に含め既存行も即時更新。App 全体の .cs/.xaml を再調査し、コメント・言語名・日本語モード条件内を除く追加の固定日本語はなし。合成値と日英リソースによるテストを追加。

### Analytics の表示修正（実装済み、0.1.025）

`fix/analytics-i18n-and-days`: 見出し・分数・凡例・ツールチップを日英リソース化。日数は有効な分バケット数を1,440で割った24時間換算（小数1桁）と明記し、`0.1` の固定文字による誤表示を修正。電力・電圧・負荷率の件数も生サンプル数から分数へ統一。対応する未着手項目は既存一覧になし。他タブの日本語残留は後続課題。

### その他の実装済み改修（#1〜#9 とは別枠、2026-09-17 以降）

`git log --first-parent --oneline 80044e9..6cef449` と `git log --first-parent --oneline 6cef449..74ebc10` で確認した範囲。docs のみのマージは表から除外。#5・#6 も含め、0.1.017〜0.1.026 の履歴を以下にまとめる。

| 種別 | 内容 | 関連コミット |
|---|---|---|
| fix(history) | SQLite 共有キャッシュ（`Cache=Shared`）を廃止し、読み取り中の書き込みで `telemetry_samples` が `SQLITE_LOCKED` になるエラーを解消。回帰テストを追加 | `d6d9f5d`、Merge `ec500d9` |
| fix(ui) | シャットダウン自動化パネルの切替電圧範囲ラベルが、存在しないリソースキー参照で空欄になる問題を修正（既存キー `TransferRange` を参照） | `f7f1055` |
| feat(health) | 公式アプリ（CyberPower BHI 等）の健全性を日時付きで記録し、その推移から現在値を推定する改善サイクルを追加（3 モデル並走・バックテスト選択・記録時の状況の自動保存など） | `a2e973b`、Merge `c2de3ce`（`f7f1055` と同一マージ） |
| fix(health) | 健全性記録で履歴状況取得に失敗しても、起動後の履歴問い合わせ成功時に不足状況を補完（既存の記録時現在値は維持） | `9cff017` ほか、Merge `1624cbf` |
| build(version) | パッチ番号の一元管理、`0.1.XXX` 表示、CHANGELOG とバージョン運用を導入し、現在値を 0.1.016 に確定 | Merge `6cef449`、タグ `v0.1.016` |
| feat(logs)（0.1.017、#6） | 保存済みログの期間・種別・重大度フィルタ、live 統合、最新 500 件の上限表示を追加 | `feature/logs-saved-event-filter`、Merge `75fc16c` |
| feat(export)（0.1.018、#5） | 全期間選択、JSON 逐次出力、安全な一時ファイル置換、キャンセル・終了待機に対応 | `feature/export-all-range-streaming`、Merge `0e8231a` |
| feat(health)（0.1.019） | 公式健全性記録の日時入力案内・未来日時検証を追加（DatePicker の起動時不具合は 0.1.021 で修正） | `feature/health-record-date-validation`、Merge `b9c7dff` |
| fix(logs)（0.1.020） | Logs の初期期間を全期間に変更し、500 件の上限と案内を維持 | `fix/logs-default-all-range`、Merge `212d18d` |
| fix(ui)（0.1.021） | DatePicker の読み取り専用プロパティへの書き戻しによる起動時終了を修正し、Logs・設定・History・Analytics の表示と選択保持を修正 | `fix/date-picker-binding-crash`、Merge `6185bdb` |
| refactor（0.1.022） | 未使用の DailyEnergyReports・旧 API/型と AppPaths の未使用パスを削除し、テストを新 API へ移行 | `fix/remove-unused-code`、`023b009`、Merge `7ff7a77` |
| fix(history)（0.1.022） | チャート範囲線の共有 Pen を凍結し、大量の健全性データ描画時の応答停止を修正 | `fix/remove-unused-code`、`f2ac66a`、Merge `7ff7a77` |
| fix(ui)（0.1.023） | 入力電圧異常の境界条件を文面に明示し、Analytics 選択ラベルの日英表示を修正 | `fix/voltage-message-and-analytics-labels`、Merge `b288edd` |
| feat(health)（0.1.024） | 公式値の密集記録を推定時に固定先頭窓24時間の最新1件へまとめ、保存件数と推定用件数を区別 | `feature/health-dense-record-grouping`、Merge `93298b4` |
| fix(analytics)（0.1.025） | Analytics の日英表示・日数書式を修正し、件数を有効な分数へ統一 | `fix/analytics-i18n-and-days`、Merge `c8d2561` |
| fix(ui)（0.1.026） | Dashboard・History・通知テストの固定日本語を日英リソース化し、概算列も言語切替で更新 | `fix/remaining-ui-i18n`、Merge `74ebc10` |

#5 は 0.1.018、#6 は 0.1.017 で実装済み。

整理済み（0.1.022、`fix/remove-unused-code`）: XAML 未バインドの旧 `DailyEnergyReports`（7 日固定）と毎リフレッシュの取得処理、`QueryDailyEnergyReportsAsync` ラッパー・`DailyEnergyReportItem` を削除。既存の新 API テストで精度・料金・停電集計が担保されているためラッパー専用検証を削除し、7 日分の件数確認とベンチマークは `QueryEnergyReportsAsync` に移行した。

### 健全性推定（公式値推移）の運用上の注意

コード（`UpsMonitor.Core/OfficialHealthEstimator.cs`・`BatteryHealth.cs`）と UI 文字列（`Strings.ja-JP.xaml` の `OfficialRecordHelp` / `OfficialModelHelp`）から確認できる範囲:

- 記録は設定画面の「公式の健全性を記録」から行い、UPS が接続されていないと記録できない（`OfficialRecordRequiresUps`）。公式値は 0〜100 の数値、時刻は `HH:mm` 形式。
- 購入（設置）日は推移の傾きを求める補助で、公式値の記録が 1 件以上必要。
- 3 モデル（直近の傾き／購入日起点の直線／購入日起点の加速曲線）のうち、全モデルのバックテストが 3 件以上になるまでは、利用可能なら購入日起点の直線が使われる（`MinimumBacktestsForSelection = 3`）。したがって採用モデルが安定するまで記録を貯める必要がある。
- 3 モデルすべてのバックテストが 3 件以上になるには推定用の代表点が 5 件以上必要（加速曲線は当てはめに購入日より後の 2 点を要するため、通常バックテスト数は代表点数 − 2）。それまでは購入日起点の直線（使えなければ直近の傾き）が使われる（`OfficialHealthEstimator.cs` の `SelectModel` / `Backtest`）。
- 外挿は最後の記録を起点に行う。信頼度は最後の記録からの経過日数で決まり、14 日以内が High（推定用の代表点が 1 件だけなら Medium）、60 日以内が Medium、それ以降が Low。
- 推定に使うランタイムは定常状態（60 秒・負荷差 3 ポイント以内）の値のみで、負荷のべき乗則で正規化した相対性能として扱われる。
- 推定器は経過時間24時間の固定先頭窓で最新の代表点を作る。暦日で分けず、窓は連鎖させない。保存記録は削除しない。
- 記録日付は過去日も入力できる。日付と時刻が空なら現在日時、日付だけなら今日は現在時刻・過去日は 00:00、時刻だけなら今日のその時刻を使う。カレンダー選択は今日までとし、記録日時が現在から5分を超える未来なら拒否する（5分は時計のずれの許容）。

#### 推奨する記録の運用

保存記録は残し、推定・予測・モデル比較・バックテスト・信頼度には24時間の代表点を共通使用する。短時間の訂正は最新の値と時刻が反映され、先頭から24時間を超えると別群になるため境界で結果が変わり得る。通常の14日間隔は変わらない。保存件数と推定用件数は画面で区別し、記録時の予測誤差（保存値）は再計算MAEと区別する。

コードの仕組みから導いたもので、実データでの検証はしていない。

1. 最初に 1 件記録する。
2. 公式値が変わったことを確認したら、確認した時刻で 1 件記録する。
3. 値が変わらなくても、最後の記録から 14 日以内に 1 件記録する。信頼度 High を保つため。値が変わらない期間の情報も推定に入る。
4. 数時間おきの追加は推定用の独立した件数を増やさない（24時間以内は最新の1件）。短時間の訂正は最新値を反映するために使い、長期の推移は間隔を空けて記録する。

以前ここに書いていた「公式値が下がった日にだけ記録する」は、コード上の根拠が弱いので採らない。下がったときだけ記録すると、値が平坦だった期間の情報が入らず、傾きが急に見えるおそれがある（推測）。

#### 記録時の状況（Context）と履歴 DB の保持期間

- 記録時の状況（負荷・電力・ランタイム・電圧・直前 10 分／24 時間の統計・最終放電からの日数）は、**記録した時点で**履歴 DB に問い合わせて保存する。現在時刻から ±5 分以内の記録では、瞬間値を現在値で上書きする（`OfficialHealthContextService.CaptureAsync`）。
- 起動時の backfill は、履歴取得が未完了の記録（旧記録や、記録時に履歴取得が失敗した記録）を補完する。記録時の現在値など既存の非 null 値は維持し、不足項目だけを埋める。記録時・補完時に正常に問い合わせできた記録は、結果が空でも再試行しない（冪等）。例外や履歴ソース未利用時は未完了のまま残る。
- 履歴 DB で保持期間により削除されるのは、生サンプル（`telemetry_samples`・`raw_telemetry_values`）だけ。保持期間は `history.rawRetentionDays` で、既定 14 日、範囲は 1〜365。1 分ロールアップ、状態遷移、UPS イベント、健全性観測は削除されない（`SqliteTelemetryStore.CleanupAsync`）。
- そのため保持期間を過ぎた日付の記録でも、状況の大半は 1 分ロールアップと状態遷移から補完できる。ただし充電中フラグは取れず、電力・ランタイムも取れないことがある。履歴 DB の最古時刻より前の日付では、状況は空になる。
- 状況は推定モデルでは参照されない。後から公式値と負荷などの関係を調べるための保存用で、推定精度には影響しない。記録時の負荷条件を揃える必要もない。

#### 実装済み（2026-11-12）

- `ContextBackfillAttempted` は履歴問い合わせの正常完了を表す。`CaptureForRecordAsync` は例外・ソース未利用時には false、正常完了なら結果が空でも true を保存する。現在値だけの状況が非 null でも false なら起動時補完の対象にし、既存値が null の項目だけを埋める。設定形式・DB スキーマの変更はなく、合成 SQLite と設定の保存・再読込で、live の有無にかかわらず失敗から復旧できること、live 値の維持、正常な空結果の再問い合わせなしを回帰テストで確認した。元のコードの通常の保存経路では false の記録は状況が null だったため、既存 config の補完対象は変わらない。修正前に既にフラグが true で保存された記録は、正常完了した記録と区別できないため変更しない。

#### 実装済み（0.1.019）

- `feature/health-record-date-validation` — 日時決定・検証を `OfficialRecordTimeResolver` に切り出し、今日の日付のみなら現在時刻を使うよう修正。未来日時（時計のずれ5分までを除く）を拒否し、DatePicker の上限を今日に設定。日英の案内とエラー、日時規則・日付境界の合成テストを追加。

#### 実装済み（0.1.024）

- `feature/health-dense-record-grouping` — 同じ値・近い時刻の記録による密集の偏りを、推定時の24時間のまとめで抑制。時刻順の固定先頭窓（境界含む、連鎖なし）で最新を採用し、同時刻は安定順の最後を使う。保存記録・記録時の予測は変更しない。過去時点の予測は先に生記録を絞り、バックテストは代表点と確定済み先行グループで行う。

#### コード上の改善余地（未着手）

- 健全性グラフ（`MainViewModel.HealthChart`、`SqliteTelemetryQueries.QueryHealthAsync`）は他のメトリクスと違い点を間引かず、観測が増えると描画が重くなる要因が残る（0.1.022 の共有 Pen 凍結で停止は解消済み）。
- ✅ 実装済み（0.1.022、`fix/remove-unused-code`）: 未使用の `AppPaths.DevicesFile`・`AppPaths.UserDirectory` とローカルパス引数を削除。テストの合成データ用パスは共通ルート引数だけで指定する。
- 推定結果はあくまで公式値の再現を目的とした推定で、UPS が HID で直接公開する値ではない（README 参照）。

## 今回スコープ推奨

#1〜#6 はすべて実装済み。以下の推奨理由は提案時の課題を記録したもの。

| 番号 | 施策 | 領域 | 推奨理由 | 難易度 |
|---:|---|---|---|---|
| 1 | カスタムアラートをイベント・通知・外部連携まで一貫化 | バックグラウンド・機能 | 現状は高負荷／電圧異常時に音を鳴らすだけで、定義済みイベントが永続化・Windows 通知・Webhook に流れない。監視アプリとしての機能欠落が大きい | 中 |
| 2 | Analytics タブのアクティブ判定修正 | UI | タブ番号のずれにより Analytics 選択時の即時更新が動かず、UPS タブ選択時に Analytics クエリが走る。小変更で効果と確実性が高い | 小 |
| 3 | Analytics の力率ヒートマップ追加 | UI・SQLite・機能 | 計画・リソース文字列には力率があるが、選択肢と集計ロジックがない。既存データから派生計算できる | 中 |
| 4 | 日次電力量の正確化と月次集計の追加 | SQLite・UI・機能 | 月次レポートが未実装で、現行の日次 kWh は観測時間によらず `平均W × 24h` のため当日・欠測日の値が過大になる | 中 |
| 5 | エクスポートの「全期間」選択と大容量 JSON ストリーミング | UI・SQLite・機能 | Infrastructure には全期間 API があるが UI から到達不能。現行 JSON は全行をメモリ保持するため、そのまま公開すると長期データで危険 | 中 |
| 6 | 保存済みログの日付範囲・イベント種別フィルタ | UI・SQLite・機能 | ✅ 0.1.017 で実装済み。保存済みログの読込、期間・種別・重大度、上限表示に対応 | 中 |

## 見送り推奨

| 番号 | 施策 | 領域 | 見送り理由 | 再検討条件 |
|---:|---|---|---|---|
| 7 | `MainViewModel` の責務分割 | アーキテクチャ | 明確な積み残し。#5・#6 との競合を理由に見送っていたが、両施策は統合済み（2026-11-10 の検討メモ参照） | #5・#6 の統合条件は達成済み（0.1.018・0.1.017）。残るタブ単位の振る舞いテストを用意できた時点で再検討 |
| 8 | ReadyToRun 発行設定の検証 | 起動 | 発行方式・配布サイズ上限・cold start 基準値が未定で、設定だけ有効化しても効果を判定できない | x64 配布形態と起動計測シナリオを確定した時点 |
| 9 | 日次レポート DataGrid の差分更新 | UI | 対象だった旧 `DailyEnergyReports` は 0.1.022 で削除済み。現行 `EnergyReports` は日次30日／月次12か月を対象に、同値時は更新を抑止し、変更時は `Clear` + `Add` で再構築 | 現行 DataGrid の再描画・選択・スクロールへの影響が UI 計測で確認された場合、または表示期間をさらに拡大する場合 |

## 提案施策の仕分け結果

| 出典の提案群 | 判定 | コード確認結果 |
|---|---|---|
| 起動: SQLite 遅延初期化、`user_version` 短絡、設定初回保存の非ブロッキング化、UI 先行表示 | 実装済み | `SqliteTelemetryStore.InitializeAsync` / `EnsureInitializedAsync`、`JsonConfigurationStore.LoadAsync`、`App.OnStartup` を確認。さらに `330ff76` で v1→v2 の軽量マイグレーションも追加済み |
| UI: トレイ差分更新・GDI 解放、PropertyChanged 差分化、チャート二分探索／Brush キャッシュ、ヒートマップ直接参照、Timeline Brush 共有、影の Performance bias | 実装済み | `TrayIconManager`、`MainViewModel.SetField`、`TimeSeriesChart.FindClosestPoint`、`WeeklyHeatmapControl` の 7×24 配列、`UpsStateTimeline`、`MiniMonitorWindow.xaml` を確認 |
| SQLite: 日次 N+1 解消、複数メトリック一括取得、サグ／サージ単一走査、cleanup 索引、PRAGMA 調整 | 実装済み（機能精度の残件は 4） | `SqliteTelemetryQueries` と `SqliteTelemetryStore.ExecuteSchemaAsync` を確認。`8018f3c`、`330ff76` に履歴あり |
| バックグラウンド: ポーリング待機、イベント定常時割当、HID バッファ／Parser／Mapper、Webhook retry、CommandRunner の drain／kill | 実装済み | `Monitoring.WaitForNextPollAsync`、`UpsEventDetector.Observe`、`HidDeviceSession`、`HidReportParser`、`UpsHidMapper`、`WebhookNotifier`、`CommandRunner` を確認 |
| 機能: 日付・種別ログ、日次／月次レポート、力率 Analytics、全期間 export、カスタムアラート通知 | 一部のみ実装 | それぞれ 1、3〜6 に分解。walkthrough の完了表現より実コードを優先した |
| `MainViewModel` 責務分割 | 提案のみ | Logs は 0.1.017 で `LogsViewModel` へ抽出済み。設定・履歴・Analytics・監視表示は単一クラスに残る（7） |
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

- **状態**: ✅ 実装済み（0.1.018、2026-11-13、`feature/export-all-range-streaming`）。
- **出典**: `docs/architecture_and_ux_improvements/implementation_plan.md`「1-5. データエクスポートの期間指定」、同 `walkthrough.md`「5. データエクスポート機能の拡張」。
- **調査時の課題**: 全期間 API が UI から呼べず、JSON は全行を List に保持。Logs CSV は History の期間（未選択時 30 日）を使い、#6 の検索条件と独立していた。
- **実装**:
  - History は「現在の履歴範囲」／「全期間」、Logs は「現在のフィルタ条件」／「全期間」を個別に選択。LogsViewModel が表示と同じ EventQuery の snapshot を生成し、現在条件では期間・種別・重大度を共有、全期間ではすべて解除する。
  - SqliteEventQueries の SQL 条件とパラメータ設定を exporter と共有。新しい EventQuery overload は [From, To) で検索し、表示上限の Limit は適用しない。従来の期間引数 overload とテレメトリの上端包含は互換維持。保存済みの全デバイス／全物理行を対象に出力し、表示用の重複除去は適用しない。
  - テキスト検索は翻訳済み表示行だけのクライアント側検索なので export へ適用しない。表示上限も適用せず、UI の日英ヘルプと README に明記した。
  - ExportOperation が FlushAsync を待ってから出力。TelemetryExporter は JSON を Utf8JsonWriter で直接 1 行ずつ書き、各行でバッファを Flush する。全件 List／Dictionary の保持はなく、CSV も従来の逐次出力。列・キー順・日時・数値丸め・nullable 値・CSV の UTF-8 BOM／JSON の BOM なしは維持。
  - 全 exporter entry point が出力先と同じディレクトリの一意な一時ファイルへ書き、ストリームを閉じた後にキャンセルを再確認して置換。失敗・キャンセル・置換失敗時は finally で一時ファイルを削除し、既存ファイルを保持する。
  - UI は Task.Run で DB／出力処理を行い、MainViewModel の export 専用 CTS を共有して CSV／JSON／Events 間の二重実行を防止。各タブに実行状態とキャンセルボタンを表示し、dispose 時にもキャンセルし、dispatcher に依存しない worker の temp cleanup まで最大 5 秒待つ。OnExit の外側待機は 6.5 秒とし、上限超過時は temp が残る可能性を許容して終了する。
- **当初案との差**: enum の追加はせず ComboBox の選択を使用し、Logs の条件は先行抽出済み LogsViewModel に置いた。UI は Flush 保証の共通 ExportOperation を通して既存の範囲 API を呼ぶ（全期間は非制限境界）。全期間 API 自体も引き続き利用可能。全期間は保存済み raw データの全期間であり、保持期限で削除済みの履歴の復元や rollup の出力は含めない。DB スキーマ・依存パッケージ・config・HID の変更なし。
- **検証**: 一時ディレクトリの合成 SQLite で全期間／選択期間の境界と UTC offset、Flush による直前行、JSON パース・キー順・型・CSV/JSON 件数・文字コード、空 DB、種別／重大度／未知 enum／デバイスと半開区間、Logs の immutable snapshot と全期間解除、表示上限を超える export、30,000 行の完走、途中キャンセル・IO 失敗・DB 失敗・置換失敗時の既存ファイル保持と temp cleanup を確認。終了待機は合成 callback の後始末完了と非 pumping 同期コンテキストでの完了、待機上限も検証。全 80 件。WPF 実画面での日英表示・配置・長時間処理のキャンセルとタブを跨ぐ二重実行防止は手動確認が必要。

## 6. 保存済みログの日付範囲・イベント種別フィルタ

- **状態**: ✅ 実装済み（0.1.017、2026-11-13、`feature/logs-saved-event-filter`）。
- **出典**: `docs/feature_enhancements/implementation_plan.md`「2. イベントログの高度な検索・フィルタ」、`docs/architecture_and_ux_improvements/implementation_plan.md`「1-3. ログ画面のフィルタリング & 検索機能」。
- **調査時の課題**: 起動後最大 500 件の live イベントだけが対象で、保存済み `ups_events` の読み戻し・期間・種別検索がなかった。
- **実装**:
  - Core の `EventQuery` は DeviceId（null は全デバイス）、From（包含）/ To（除外、UTC ミリ秒精度）、イベント種別集合（null は全種別、空集合は該当なし）、重大度、Limit を保持。検索開始時に集合を immutable snapshot 化する。結果は `EventQueryResult`（Events / HasMore）。#5 の export で再利用可能。
  - `SqliteTelemetryStore.QueryEventsAsync` はキューの Flush 後にパラメータ化 SQL で検索し、新しい順に Limit + 1 件から超過を判定。種別集合は JSON パラメータと SQLite 組込み `json_each` の IN で扱い、重大度は Core の分類から種別集合へ変換。未知の enum は Core と同様 Information。既存 `ix_events_device_time` の期間検索を EXPLAIN で確認し、スキーマ・索引は変更しない。
  - `LogsViewModel` を先行抽出し、24 時間 / 7 日 / 30 日 / 全期間、種別 ComboBox、重大度、検索、読込状態、キャンセルと 500 件上限を所有。期間の初期値は全期間（0.1.020 で変更）。Logs 表示時は即時、期間・種別・重大度変更は 300ms debounce で DB 読込し、キャンセルを無視する古い要求も反映しない。非表示・破棄時はキャンセル。
  - 再起動後・未接続時にも調査できるよう UI は全デバイス対象。live は即時表示し、DB 読込結果と `UTC timestamp（ミリ秒）/ type / 保存原文 message / previous_state / current_state` で重複除去する。保存精度未満の差・ローカライズの差は二重表示しない。live バッファも 500 件に制限する。
  - フリーテキストは従来と同様、読み込んだ行の日時・種別・翻訳メッセージ・状態遷移を対象。上限案内で絞り込みを促す。日英リソースを追加し、高負荷・電圧異常の種別を翻訳表示し、メッセージは実測値・しきい値を含む原文を維持する。日時の表示・文字列検索はローカル時刻に統一する。
- **当初案との差・後続**: 独立した `EventQuery.cs` / `SqliteEventQueries.cs` に配置。既存 History 用の非公開検索は従来の上端包含を維持し、新 API は [From, To) とする。live 通知は実コードでは永続化キュー投入より先なので、DB 完了を待たず bounded live バッファと検索結果を統合する。ページ送り・自由入力の日付範囲は追加せずプリセットと上限表示を採用。CSV export の条件共有は #5（0.1.018）で実装済み。
- **検証**: 合成 SQLite で空 DB、再オープン、期間境界・UTC offset、全種別／重大度／未知 enum／上限の組合せ、500 件超、デバイス分離、パラメータ化、索引利用、重複排除を確認。実 VM をソースリンクし debounce・latest-wins・非表示時停止・dispose・エラー表示・検索・日英切替を検証。保存済み UTC 行と +09:00 live の統合前後での日時表示・検索一致、アラート原文の維持も確認。全 74 件。WPF 実画面での配置・操作確認は別途必要。

## 7. `MainViewModel` の責務分割

2026-11-17 確認: #5（0.1.018）・#6（0.1.017）は main へ統合済みで、再検討の前提だった統合条件は満たされた。#6 では `LogsViewModel` を先行抽出し振る舞いテストも追加済み。残るタブの振る舞いテストを整えた上で、分割範囲と優先度を再検討する。

> **検討メモ（2026-11-10、決定ではない）**: 以前は約 2,700 行だった `MainViewModel.cs` は 3,331 行になった（#1・#4 の UI 統合と `a2e973b` の健全性推定で +約 570 行）。増加の根拠は次のとおり。`a2e973b` だけで `MainViewModel.cs` が +571 行、`MainWindow.xaml` が +112 行。見送りを維持する論拠は、#5・#6 が同じファイルと競合する点、および `Core.Tests`（58 件）が Core/Infrastructure 中心で ViewModel の振る舞いテストが無い点。優先度を上げる論拠は、機能追加のたびに同ファイルへ数百行が加わる傾向があり、将来の分割コストが増え続ける点。折衷案として、健全性記録まわり（設定・モデル比較表）だけを先に子 VM へ切り出す案もあるが、未検証。見送り維持か優先度引き上げかは別途判断する。

- **対象領域**: アーキテクチャ
- **出典**: `docs/architecture_and_ux_improvements/implementation_plan.md`「1-6. MainViewModel の責務分割・リファクタリング」
- **現状と課題**:
  `UpsMonitor.App/MainViewModel.cs` は約 3,300 行（2026-11-13 時点。調査時点では約 2,700 行・118 KB）で、監視 snapshot 表示、履歴 query、Analytics、設定保存、export、通知、シミュレータ、バッテリー基準管理を一括している。walkthrough には専用フィルタ機能の追加はあるが、責務分割そのものは実施されていない。
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
  当初の対象だった旧 `DailyEnergyReports`（固定7日）は 0.1.022（`fix/remove-unused-code`、Merge `7ff7a77`）で取得処理・旧 API/型とともに削除済み。現行 DataGrid は `EnergyReports` にバインドされ、`MainViewModel` は `QueryEnergyReportsAsync` で日次は当日を含む30日、月次は当月を含む12か月を取得する。`SequenceEqual` で同値時の更新を抑止するが、変更時は `EnergyReports.Clear()` 後に結果を1件ずつ `Add()` しており、行単位の差分更新ではない。通知・再描画コストの実測は未実施。
- **再検討条件**: #4 の日次／月次表示は実装済みのため、現行 DataGrid で再描画・選択・スクロールへの影響が UI 計測により確認された場合、または表示期間をさらに拡大する場合に再評価する。
- **提案する実装方針**:
  - 再検討条件を満たした場合、`IReadOnlyList<EnergyReportItem>` property の参照を一度だけ差し替えて `PropertyChanged` 1 回にする。行選択維持が必要なら key（period start）で既存行を reconcile する。
  - 独自 `ObservableRangeCollection` はアプリ全体で range update の需要が増えた場合だけ導入し、単用途の抽象化は避ける。
- **影響範囲・リスク**: DataGrid の選択・スクロール位置が collection replacement で失われる可能性がある。
- **必要なテスト**: 同値時無通知、差分時 1 通知、選択維持、月次／多数行、言語変更時の表示。
- **難易度**: 小
- **確証度**: コード確認済み
- **依存・競合**: 実装済みの #4 と同じ `MainViewModel.cs` / `MainWindow.xaml` が対象。今後の #7 の責務分割と競合するため、現行 `EnergyReports` を対象に必要性を計測してから調整する。

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
