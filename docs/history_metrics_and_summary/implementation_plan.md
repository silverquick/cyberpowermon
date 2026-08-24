# 実装計画: 履歴メトリクス（電力量・周波数・温度・力率）および期間サマリー指標の実装

## 概要
History（履歴）タブにおいて、UPS監視・電源品質管理としての実用性を最大化するため、以下の4つの新グラフと期間統計サマリー（KPIカード）を実装します。

1. **新グラフ追加**:
   - **積算電力量グラフ (Energy Consumption / kWh)**: 消費電力量の累積推移
   - **周波数グラフ (Frequency / Hz)**: 商用周波数の安定度推移
   - **内部温度グラフ (Temperature / ℃)**: UPS/バッテリー温度推移
   - **力率グラフ (Power Factor / %)**: 有効電力(W)と皮相電力(VA)から算出する力率推移
2. **期間統計サマリー (KPIカード)**:
   - 選択された期間（24時間、7日、30日など）における「停電回数・総停電時間」「電圧Min/Avg/Max」「平均/ピーク電力」「総消費電力量(kWh)」「最低バッテリー残量」を一目で把握できるサマリーカードを画面上部に新設。

---

## 提案する変更内容

### 1. ドメイン & データ集計 (`UpsMonitor.Core`, `UpsMonitor.Infrastructure`)
- [`UpsMonitor.Core/TelemetryHistory.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core/TelemetryHistory.cs):
  - `TelemetryPeriodSummary` レコードを追加（停電統計、電圧統計、電力統計、電力量、バッテリー統計を保持）。
  - `TelemetryMetric` に必要に応じて拡張。
- [`UpsMonitor.Infrastructure/SqliteTelemetryQueries.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/SqliteTelemetryQueries.cs):
  - `QueryPeriodSummaryAsync` を実装し、選択期間の SQL 集計（MIN/AVG/MAX、停電時間積分、台形積分による電力量 kWh 算出）を実行。
  - 周波数、温度、力率、電力量の時系列データを `QueryHistoryAsync` で生成。

### 2. ViewModel & UI 実装 (`UpsMonitor.App`)
- [`UpsMonitor.App/MainViewModel.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs):
  - `FrequencyHistory`, `TemperatureHistory`, `PowerFactorHistory`, `EnergyHistory` プロパティを追加。
  - 期間統計サマリーの表示用プロパティ（`PeriodOutageSummaryText`, `PeriodVoltageSummaryText`, `PeriodPowerSummaryText`, `PeriodEnergySummaryText` 等）を追加。
  - 履歴更新時 (`RefreshHistoryAsync`) にこれらを並行集計。
- [`UpsMonitor.App/Resources/Strings.ja-JP.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.ja-JP.xaml) / [`Strings.en-US.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.en-US.xaml):
  - 新規グラフ名・ヘルプ文、サマリーカードの見出し、単位テキストを日英両言語に追加。
- [`UpsMonitor.App/MainWindow.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml):
  - History タブの上部に 4 連の KPI サマリーカードを追加。
  - グラフエリアに周波数、内部温度、力率、積算電力量のチャートを追加。

---

## 検証計画
- `UpsMonitor.Core.Tests` に期間集計および新メトリクスの単体テストを追加してパスすることを確認。
- `dotnet build UpsMonitor.sln` で 0 警告 / 0 エラーでビルドできることを確認。
