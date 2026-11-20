# 改善施策バックログ

最終調査日: 2026-11-17
調査基準: `74ebc10` (`main`)
初回調査は 2026-08-29、`330ff76` 時点。

本書は `docs/performance/*_analysis.md` と全 `docs/*/{implementation_plan,task,walkthrough}.md` を読み、walkthrough の記載だけでなく `UpsMonitor.*` の実コードと `git log` を照合して、提案済み施策の実装状況と積み残しを整理したものである。なお、依頼背景には .NET 8 とあるが、現コードの `UpsMonitor.App/UpsMonitor.App.csproj` は `net10.0-windows` を対象としているため、以下の設計は現行コードを正としている。

## 実装状況（2026-11-20 更新）

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
| 7 | MainViewModel の責務分割 | ✅ 実装済み（統合・画面確認待ち、0.1.029） | `refactor/mainviewmodel-split` |
| 8 | ReadyToRun 発行設定の検証 | 検証済み・不採用 | 下記 #8 |
| 9 | 電力量レポート DataGrid の更新通知 | ✅ 実装済み（統合・画面確認待ち、0.1.029） | `refactor/mainviewmodel-split` |

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
| fix(history)（0.1.027） | 健全性グラフで観測件数が `maximumPoints` を超える場合に紀元揃えの等時間幅バケットへ間引き、必要な3列の流し読みで最小・平均・最大を集約して高速化・描画負荷を軽減 | `fix/health-chart-downsample` |

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

- ✅ 実装済み（0.1.027、`fix/health-chart-downsample`）: 健全性グラフ（`MainViewModel.HealthChart`、`SqliteTelemetryQueries.QueryHealthAsync`）も他のメトリクスと同様に `maximumPoints`（既定 720）を上限とし、`LIMIT N+1` で超過を判定した場合は紀元揃えのバケットで必要な3列だけを索引順に流し読みして最小・平均・最大を集約する（間引き時は `BatteryHealth` を空にして `BatteryHealthSeries` で描画し、上限以下は生データをそのまま表示）。
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

| 番号 | 施策 | 領域 | 状態 | 見送り理由 | 再検討条件 |
|---:|---|---|---|---|---|
| 8 | ReadyToRun 発行設定の検証 | 起動 | 検証済み・不採用 | 常駐アプリで起動頻度が低く、warm 起動の短縮は約 213 ms。採用には publish 出力での運用が必要 | 起動時間の要件や配布・起動方式が変わった時点 |

## 提案施策の仕分け結果

| 出典の提案群 | 判定 | コード確認結果 |
|---|---|---|
| 起動: SQLite 遅延初期化、`user_version` 短絡、設定初回保存の非ブロッキング化、UI 先行表示 | 実装済み | `SqliteTelemetryStore.InitializeAsync` / `EnsureInitializedAsync`、`JsonConfigurationStore.LoadAsync`、`App.OnStartup` を確認。さらに `330ff76` で v1→v2 の軽量マイグレーションも追加済み |
| UI: トレイ差分更新・GDI 解放、PropertyChanged 差分化、チャート二分探索／Brush キャッシュ、ヒートマップ直接参照、Timeline Brush 共有、影の Performance bias | 実装済み | `TrayIconManager`、`MainViewModel.SetField`、`TimeSeriesChart.FindClosestPoint`、`WeeklyHeatmapControl` の 7×24 配列、`UpsStateTimeline`、`MiniMonitorWindow.xaml` を確認 |
| SQLite: 日次 N+1 解消、複数メトリック一括取得、サグ／サージ単一走査、cleanup 索引、PRAGMA 調整 | 実装済み（機能精度の残件は 4） | `SqliteTelemetryQueries` と `SqliteTelemetryStore.ExecuteSchemaAsync` を確認。`8018f3c`、`330ff76` に履歴あり |
| バックグラウンド: ポーリング待機、イベント定常時割当、HID バッファ／Parser／Mapper、Webhook retry、CommandRunner の drain／kill | 実装済み | `Monitoring.WaitForNextPollAsync`、`UpsEventDetector.Observe`、`HidDeviceSession`、`HidReportParser`、`UpsHidMapper`、`WebhookNotifier`、`CommandRunner` を確認 |
| 機能: 日付・種別ログ、日次／月次レポート、力率 Analytics、全期間 export、カスタムアラート通知 | 一部のみ実装 | それぞれ 1、3〜6 に分解。walkthrough の完了表現より実コードを優先した |
| `MainViewModel` 責務分割 | 実装済み（統合待ち） | `refactor/mainviewmodel-split` で Analytics・History・Settings・BatteryHealth と共通 export を抽出（7・9） |
| ReadyToRun | 検証済み・不採用 | 比較用の publish profile と計測手順を保持。通常のビルド・起動方式では無効（8） |
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

## 7. `MainViewModel` の責務分割（実装済み、0.1.029）

2026-11-20: `refactor/mainviewmodel-split` で段階1〜4を実装。main への統合と最終画面確認は別途行う。

- **対象領域**: アーキテクチャ
- **出典**: `docs/architecture_and_ux_improvements/implementation_plan.md`「1-6. MainViewModel の責務分割・リファクタリング」
- **実装**: 振る舞いテストを先行させ、Analytics（指標・期間・weekly pattern）、History（グラフ・期間サマリー・電力量レポート・健全性グラフ）、Settings（編集・保存・言語/テーマ・通知テスト）、BatteryHealth（公式記録・補完・基準・モデル比較・購入日）を子 VM へ抽出。先行実装の LogsViewModel を維持し、History/Logs 共通 export は ExportViewModel に集約した。
- **所有権**: MainViewModel は 3,104 行から 1,175 行へ縮小し、engine lifecycle・snapshot 配布・共通状態と監視表示の projection を担当する。store/save/effects は constructor で注入。履歴・Analytics の CTS は要求自身が finally で Dispose し、置換と終了時は Cancel する。
- **互換性の判断**: SettingsDraft は採用しない。言語・テーマ・自動起動・通知・料金など既存の即時反映を維持し、保存失敗時も編集内容を保持する。共有 config の形式と編集方式を維持し、保存成功後に engine へ反映する。健全性の警告しきい値は BatteryHealth 側に置き、Settings の保存処理が検証・保存する。
- **検証**: Core.Tests から実 VM をソースリンクし、合成 SQLite と WPF 非依存 adapter で選択保持・日英切替・取得条件・latest-wins・保存失敗・公式記録と補完・24時間件数・基準と購入日・Dispose を検証。XAML の nested binding と日英キーを照合。全 130/130 件、Debug 警告0件。
- **残る確認**: Release の実画面、言語/テーマとタブ選択保持、再表示、通知、DataGrid のスクロールは最終統合前に一度だけ確認する。タブ単位の View 分割は今回の対象外。

## 8. ReadyToRun 発行設定の検証（検証済み・不採用）

- **対象領域**: 起動・ビルド
- **出典**: `docs/performance_optimization/implementation_plan.md`「フェーズ 5: ビルド・発行設定最適化と総合テスト」

### 2026-11-21 warm 起動計測と採否

合成データと UPS 切断状態で、プロセス起動から MainWindow の `ContentRendered` までを計測した。事前のウォームアップ各 1 回は集計に含めていない。

| 構成 | 回数 | 中央値 (ms) | 最小 (ms) | 最大 (ms) |
|---|---:|---:|---:|---:|
| IL baseline | 10 | 1,764.9 | 1,480.7 | 2,085.2 |
| ReadyToRun | 10 | 1,552.1 | 1,432.2 | 1,934.6 |

中央値の短縮は約 212.8 ms（12.1%）。**不採用（2026-11-21 ユーザー判断）**。常駐アプリで起動頻度が低く、短縮幅が小さい。R2R は `dotnet publish` でのみ有効で、採用には `bin\Release` の exe から publish 出力一式へ起動元を切り替える必要がある。比較用プロファイルと計測手順は再評価用に残す。

### 2026-11-19 検証準備と発行比較

比較時の配布条件は framework-dependent（利用 PC に .NET 10 / Windows Desktop Runtime がある前提）。self-contained・単一ファイル化・trim は行わない。通常の `dotnet build -c Release` は変更しない。

- 比較設定: `UpsMonitor.App/Properties/PublishProfiles/win-x64-r2r.pubxml`（Release / win-x64 / SelfContained=false / PublishReadyToRun=true）。publish 出力は `artifacts/r2r-eval/r2r/`。`dotnet publish` には明示的に `-c Release` を付ける。
- **build では R2R 化されないことを実測確認**: `dotnet build UpsMonitor.App/UpsMonitor.App.csproj -c Release -r win-x64 --self-contained false -p:PublishReadyToRun=true` の App/Core/Hid/Infrastructure DLL は全て IL。`dotnet publish ... -c Release -p:PublishProfile=win-x64-r2r` で初めて全 DLL が R2R になった。build の RID 指定出力は `bin/Release/net10.0-windows/win-x64/` だが、これも R2R ではない。
- 根拠: [Microsoft の R2R 概要](https://learn.microsoft.com/en-us/dotnet/core/deploying/ready-to-run)、[dotnet publish](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish)。ローカル SDK の `Microsoft.NET.Publish.targets` でも `ComputeFilesToPublish` の依存工程に `CreateReadyToRunImages` がある。R2R を採用する場合は、現在の通常 Release exe から **publish フォルダーの exe と関連ファイル一式**を使う運用への変更が必要（今回は変更しない）。

再現コマンド（子 worktree 内で実行。アプリは起動しない）:

```powershell
# 通常 Release build（現在の利用形態）
dotnet build UpsMonitor.App/UpsMonitor.App.csproj -c Release
# RID・配布条件を揃えた IL baseline
dotnet publish UpsMonitor.App/UpsMonitor.App.csproj -c Release -r win-x64 --self-contained false -p:PublishReadyToRun=false -p:PublishSingleFile=false -p:PublishTrimmed=false -o artifacts/r2r-eval/baseline
# R2R
dotnet publish UpsMonitor.App/UpsMonitor.App.csproj -c Release -p:PublishProfile=win-x64-r2r
```

実測環境: Windows x64、SDK 11.0.100 / MSBuild 18.12.2、対象 net10.0-windows、Windows Desktop Runtime 10.0.15 導入済み。時間は各1回の参考値で、事前に対応する `dotnet clean ... -c Release`（publish は `-r win-x64` も指定）を実行し、restore を含むプロセス全体を Stopwatch で計測した。ファイル／NuGet キャッシュは残っており、初回ダウンロードや cold build の値ではない。通常 build は RID なしのため SQLite の複数 RID 向け native asset を含み、サイズの R2R 純増比較には baseline publish 同士を使う。

| 出力 | 合計 bytes（全ファイル・PDBを含む） | ファイル数 | clean 後 build/publish 秒 |
|---|---:|---:|---:|
| 通常 Release build（RID なし） | 37,030,540 | 37 | 16.621 |
| framework-dependent win-x64 IL publish | 5,054,474 | 16 | 7.688 |
| framework-dependent win-x64 R2R publish | 6,418,090 | 16 | 9.557 |

R2R publish は IL publish 比 +1,363,616 bytes（約27.0%）。通常 build 初回は26.444秒、R2Rプロパティ付き build は7.563秒だったが、実行順・restore・キャッシュ条件が異なるので採否の根拠にはしない。並行ビルド等の負荷もあり、所要時間を比較するなら再測定する。

| 主な DLL | IL bytes | R2R bytes |
|---|---:|---:|
| UpsMonitor.App.dll | 1,843,200 | 2,183,168 |
| UpsMonitor.Core.dll | 189,440 | 442,368 |
| UpsMonitor.Hid.dll | 70,656 | 163,840 |
| UpsMonitor.Infrastructure.dll | 194,560 | 471,040 |
| Microsoft.Data.Sqlite.dll | 177,504 | 450,560 |

R2R 確認はスクラッチの `Inspector/Program.cs` で `PEReader.PEHeaders.CorHeader.ManagedNativeHeaderDirectory` を読み、その RVA の先頭32bit が `0x00525452`（`RTR\0`）であることを検証した。App.dll の native header size は196 bytes。通常 build・R2R指定 build・IL publish には native header がなく、R2R publish の管理 DLL 8本には署名があった（native SQLite は対象外）。ファイルサイズ増加だけで判定していない。

**リソース・reflection の確認**: IL/R2R 双方で App.dll の `Assembly.GetTypes()`（86型）、`.g.resources` 列挙（7件）、light/dark テーマ適用、明示的な component pack URI で日英 ResourceDictionary 読み込みが成功。検査用の WPF `Application` は作成するが、UpsMonitor の App / MainWindow / OnStartup は呼ばず、デバイス・実 config・実 DB に触れない。既存 Core.Tests も framework-dependent R2R publish して **107/107 通過**。テストが `AppContext.BaseDirectory` から4階層上のソースリソースを読むため、出力は `artifacts/r2r-eval/tests-r2r/run/` に配置した（浅い配置では14件がパスエラーになる）。通常 build・比較 publish・検査ツール・計測用コピーに新規警告なし。完全な画面動作・実機・トレイ・署名・配布経路の確認は今後。

**起動計測の準備物（コミット対象外）**:

- スクラッチ: `C:\Users\geranium\AppData\Roaming\orca\chat-scratch\e5e05225acb935fc6ff1da3360003f82\r2r-eval\`
- `Prepare-StartupBenchmark.ps1`: 子 worktree のソースをスクラッチへコピーし、そこだけで保存先をスクラッチ必須に変更、既定の合成 config と接続しない provider を使用、MainWindow の `ContentRendered` で Stopwatch timestamp を書いて自動終了するようにする。同一変更を施した IL/R2R の `measurement/baseline/` と `measurement/r2r/` を作成済み。NuGet feed は空にしてローカルキャッシュでビルドした。製品コードにはこれらの変更を入れていない。
- `Measure-Startup.ps1`: 既存 `UpsMonitor.App` の不在を各起動前に確認する。ウォームアップ各 1 回を除外し、構成の起動順を交互にしたペアを 10 回計測。プロセス開始直前から `ContentRendered` までを Stopwatch で測り、CSV・中央値・最小／最大を出す。描画上限 5 秒、終了待ち上限 2 秒。タイムアウト時はスクリプトが保持する自身の子プロセスだけを終了する。
- 2026-11-21 は、通常 Release の起動処理を削除した写し `C:\Users\geranium\AppData\Roaming\orca\chat-scratch\dd19426be334ed52c4e1e899fcbec0cf\Measure-Startup-NoRestore.ps1` を使用。再評価時はユーザー自身が常駐アプリを終了した後、PowerShell 7 で写しに `-Iterations 10` を指定する。計測中の通信・HID アクセスはなく、config/DB/ログ/描画通知/CSV は全てスクラッチへ書く。監視を再開するときは確認済みの通常 Release を別途起動する。
- 今回の CSV は上記スクラッチの `results\ba08136cf4b0477aa044d5b741f9fbd0\startup.csv`。
- cold は自動化しない。ファイルコピー・GC ではキャッシュを排除できない。必要ならユーザーが自動起動を止めた再起動直後の初回を、baseline/R2R の順を入れ替えた別セッションで比較する。今回のスクリプトは warm のみ。
- 計測は空の合成 DB／既定 config／UPS 未接続で条件を揃えた IL/R2R publish のコピー同士で実施した。実運用での起動効果、cold 起動、working set、実機での機能同等性、署名・配布経路は未検証。環境・SDK・ソースが変わったら計測コピーを再準備する。

## 9. 日次レポート DataGrid の差分更新（実装済み、0.1.029）

- **対象領域**: UI
- **出典**: `docs/performance/ui_responsiveness_analysis.md`「2.3 DataGrid / Chart 再描画コスト」、`docs/ui_responsiveness_optimization/implementation_plan.md`「2.2 MainViewModel」
- **実装**: `refactor/mainviewmodel-split` の段階3で `HistoryViewModel.EnergyReports` を `IReadOnlyList<EnergyReportItem>` とし、同値時は同じ参照を維持して無通知、変更時は結果リストの参照を1回差し替えて PropertyChanged を1回発行する。Clear/Add による行ごとの再通知を廃止した。日次30日・月次12か月の query 条件は維持。
- **選択・スクロール**: 表は読み取り専用で選択の binding や選択依存操作がなく、旧 Clear/Add も変更時に全行を削除していたため、period start による行 reconcile は不要と判断した。同値時は参照を維持する。変更時の選択・スクロール維持は新たに保証せず、最終画面確認で実表示を確認する。
- **検証**: 同値時無通知、変更時1通知、30行/月次12行、日英切替で同値参照を維持することをソースリンクした VM で検証。Debug 警告0件、全130/130件。UI計測・実画面は未実施。
- **状態**: 実装済み、main 統合・最終画面確認待ち。

## 実装フェーズの分割案

| フェーズ | 施策 | 進め方 | 主な所有ファイル／競合 |
|---|---|---|---|
| A: 低リスク先行修正 | 2 | 単独で先行。挙動を直して回帰テストを固定 | `MainViewModel.cs`（UI 統合フェーズ開始前に完了） |
| B1: イベント基盤 | 1 の Core / Infrastructure 部分 | B2 と並列可能 | `UpsEvents.cs`, `Monitoring.cs`, `AppConfiguration.cs` |
| B2: データ query 基盤 | 3、4、6 の Core / SQLite 部分 | 同じ `SqliteTelemetryQueries.cs` を触るため 1 グループ内で直列。B1、B3 と並列可能 | `TelemetryHistory.cs`, `SqliteTelemetryQueries.cs`, `SqliteTelemetryStore.cs`, tests |
| B3: Export 基盤 | 5 の streaming/temp-file 部分 | B1、B2 と並列可能 | `TelemetryExporter.cs`, tests |
| C: UI 統合 | 1、3、4、5、6 | 同一担当が直列統合。MainWindow と MainViewModel の競合を一箇所に閉じ込める。順序は 1→3→4→6→5 | `MainViewModel.cs`, `MainWindow.xaml`, `Resources/Strings.*.xaml`, `App.xaml.cs` |
| D: 構造整理 | 7（必要なら 9 を併合） | A〜C の機能テストが揃ってから直列。タブ単位に小さく移行 | `MainViewModel.cs`, `MainWindow.xaml`, 新規 child VM / View |
| E: 発行評価 | 8 | 検証済み・不採用 | 比較用 publish profile、計測手順 |

並列化の要点は、B1（Core event）、B2（SQLite query）、B3（Exporter）を別担当にし、`MainViewModel.cs` / `MainWindow.xaml` を触る C を一つの統合グループへ集約することである。3・4・6 はすべて `SqliteTelemetryQueries.cs` を変更するため分割せず、7 は全 UI 施策と競合するため最後に回す。
