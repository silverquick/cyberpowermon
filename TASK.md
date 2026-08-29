# タスク: Phase C 施策 #1 / #4 の UI 統合

【役割】実装担当。この worktree（cyberpowermon / WPF / net10.0-windows、ブランチ `silverquick/impl-c-ui`、main から分岐済み）で施策 #1・#4 の UI 配線を行う。
Core / Infrastructure 側（イベント検知・`QueryEnergyReportsAsync` 等）は Phase B で実装・マージ済み。このフェーズは **App レイヤーのみ**。

【設計書】`docs/improvement_backlog.md` の「## 1.」「## 4.」を読むこと。

**必ず Step 1 → build/test → Step 2 → build/test → コミット の順で進めること。各 Step 終了時に必ずビルドとテストを通す。**

---

## Step 1: 施策 #1（カスタムアラートの UI 配線）

前提: `UpsEventDetector` は既に `HighLoadWarning` / `VoltageAbnormal` イベントを発行する。`AppConfiguration.AlertsConfiguration.ToAlertThresholds()`、`UpsMonitorEngine.SetAlertThresholds(...)`、`ExternalCommandConfiguration.CommandOnHighLoad` / `CommandOnVoltageAbnormal` は実装済み。

1. `UpsMonitor.App/App.xaml.cs`（`new UpsMonitorEngine(` の箇所）: コンストラクタ引数に `alertThresholds: configuration.Alerts.ToAlertThresholds()` を追加。
2. `UpsMonitor.App/MainViewModel.cs`:
   - `CheckCustomAlerts` メソッド定義と、その呼び出し（`ApplySnapshotCore` 内あたり）を **削除**。未使用になる `_lastCustomAlertTime` フィールドも削除。
     （音だけ鳴らす旧経路。Core が発行するイベントが `OnEventDetected` を通じて Logs / DB / 通知 / Webhook に流れるので不要。）
   - 音の扱い: `OnEventDetected` で `EnableSoundAlerts` 時にイベント種別ごとに音を鳴らす既存ロジックがあれば、`HighLoadWarning` / `VoltageAbnormal` も鳴るようにする。既存ロジックが無ければ追加不要（DB/通知に載ることが主目的）。
   - 外部コマンド switch（`upsEvent.Type switch { ... CommandOnBatteryLow ... }` の箇所）に
     `UpsEventType.HighLoadWarning => _configuration.ExternalCommand.CommandOnHighLoad,`
     `UpsEventType.VoltageAbnormal => _configuration.ExternalCommand.CommandOnVoltageAbnormal,` を追加。
   - VM プロパティ `CommandOnHighLoad` / `CommandOnVoltageAbnormal` を追加（既存 `CommandOnBatteryLow` プロパティと同じ書き方）。
   - `SaveSettingsAsync`（`_configuration.ExternalCommand.CommandOnBatteryLow = ...;` や `_engine.SetRuntimeLowThreshold(...)` のある箇所）:
     - `_configuration.ExternalCommand.CommandOnHighLoad = CommandOnHighLoad;` と `...CommandOnVoltageAbnormal = CommandOnVoltageAbnormal;` を追加。
     - `_engine.SetRuntimeLowThreshold(...)` の直後に `_engine.SetAlertThresholds(_configuration.Alerts.ToAlertThresholds());` を追加。
3. `UpsMonitor.App/MainWindow.xaml`: `CommandOnBatteryLow` の TextBox 行の近くに、`CommandOnHighLoad` / `CommandOnVoltageAbnormal` 用の TextBox 行を 2 つ追加（既存行と同じ Grid パターン。Grid.Row の増加とラベル追加を忘れずに）。
4. `UpsMonitor.App/Resources/Strings.ja-JP.xaml` と `Strings.en-US.xaml`: 新規ラベルのキーを追加（例: `SettingsCommandOnHighLoad` = 「高負荷時のコマンド」/ "Command on high load"、`SettingsCommandOnVoltageAbnormal` = 「電圧異常時のコマンド」/ "Command on abnormal voltage"）。両言語ファイルで同じキー集合にすること。

→ `dotnet build UpsMonitor.sln` 0 エラー、`dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj` 全 PASS を確認。
→ `git add -A && git commit -m "feat(ui): カスタムアラートを設定・外部コマンドと連携"`

---

## Step 2: 施策 #4（月次電力量レポートの UI 追加）

前提: `SqliteTelemetryStore.QueryEnergyReportsAsync(deviceId, from, to, EnergyReportPeriod granularity, rate, ct)` と `EnergyReportItem`(`Period`,`PeriodStart`,`PeriodEnd`,`EnergyKwh`,`EstimatedCost`,`PeakWatts`,`AvgWatts`,`OutageCount`) は実装済み。
既存の `DailyEnergyReports`（`ObservableCollection<DailyEnergyReportItem>`、7 日固定）はそのまま動く（内部で正確版に移行済み）。**既存の日次表示を壊さないこと。** 月次を「追加」する方針。

1. `MainViewModel.cs`:
   - enum バインド用に `EnergyReportGranularity`（`Day` / `Month`）プロパティを追加。既定は `Day`。
   - `ObservableCollection<EnergyReportItem> EnergyReports { get; } = [];` を追加。
   - 履歴リフレッシュ（`QueryDailyEnergyReportsAsync` を呼んでいる箇所）で、`EnergyReportGranularity` に応じて:
     - `Day`: 直近 30 日を `QueryEnergyReportsAsync(devId, now.Date.AddDays(-29), now, EnergyReportPeriod.Day, rate, ct)`
     - `Month`: 直近 12 ヶ月を `QueryEnergyReportsAsync(devId, 今月-11ヶ月の月初, now, EnergyReportPeriod.Month, rate, ct)`
     を呼び、`EnergyReports` を差分更新（既存 `DailyEnergyReports` と同じ SequenceEqual パターン）。
   - `EnergyReportGranularity` の setter 変更時に該当リフレッシュを 1 回呼ぶ。
   - 既存 `DailyEnergyReports`（7日）はそのまま残してよい。UI をどちらか一方に寄せる場合も、まず `EnergyReports` を追加し、DataGrid の ItemsSource を新コレクションに差し替える形で最小変更にする。
2. `MainWindow.xaml`: 日別レポート DataGrid の近くに Day/Month 切替（RadioButton 2 個 or ComboBox）を追加。DataGrid は期間ラベル列（`PeriodStart` を "yyyy-MM-dd"（Day）/ "yyyy-MM"（Month）で表示。コンバータ or 事前整形プロパティで可）＋既存の kWh / 料金 / ピーク / 平均 / 停電回数列。
3. `Strings.*.xaml`: 切替ラベル（`EnergyGranularityDay`=「日次」/"Daily"、`EnergyGranularityMonth`=「月次」/"Monthly"）等を両言語に追加。

→ `dotnet build UpsMonitor.sln` 0 エラー、テスト全 PASS を確認。
→ `git add -A && git commit -m "feat(ui): 電力量レポートに月次集計と期間切替を追加"`

---

## 触ってはいけない範囲
- `UpsMonitor.Core/`, `UpsMonitor.Infrastructure/` の既存ロジック変更（このフェーズは App のみ。ただし新規 XAML コンバータクラスの追加は可）。
- #2 で導入した `MainViewModel` のタブ index 定数・判定ヘルパ（`AnalyticsIndex` 等）を壊さないこと。

## 完了報告
両 Step 完了後、**この TASK.md 末尾に `## 完了報告`** を追記（Step ごとの変更ファイル・追加/削除内容・build 結果・test 結果・2 つのコミットハッシュ・UI 上の見え方の説明・設計判断）。これが完了の合図。
