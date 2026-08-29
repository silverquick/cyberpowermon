# ウォークスルー: 曜日×24時間 週間ヒートマップ分析タブの実装

## 1. 概要
UPS で蓄積されたテレメトリデータ（消費電力、商用入力電圧、負荷率）から、生活リズムや電圧変動傾向を直感的に可視化・分析できる**「Analytics（分析）」タブ**を新設しました。

7日間（月〜日）× 24時間（00:00〜23:00）の **168セル・ヒートマップマトリクス** により、どの時間帯に電力が集中しているか、待機電力が低い時間帯、商用電圧の低下・上昇パターンをひと目で把握できます。

---

## 2. 実装した主要コンポーネント

### ① コア & SQLite 集計層
- [`UpsMonitor.Core/TelemetryHistory.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core/TelemetryHistory.cs):
  - `HourlyPatternPoint`: 曜日（0〜6）× 時間帯（0〜23）の平均値・最小値・最大値・サンプル数。
  - `WeeklyPatternResult`: 168 セルのグリッドデータ、全体 Min/Max/Avg、ピーク時間帯、最低待機時間帯。
- [`UpsMonitor.Infrastructure/SqliteTelemetryQueries.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/SqliteTelemetryQueries.cs):
  - `QueryWeeklyPatternAsync`: SQLite の `telemetry_rollups_1m` テーブルから `strftime('%w')` / `strftime('%H')` を使用して曜日・時間別に高速集計。

### ② WPF カスタム描画ヒートマップ
- [`UpsMonitor.App/WeeklyHeatmapControl.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/WeeklyHeatmapControl.cs):
  - `DrawingContext` による高速描画。
  - スムーズなグラデーションカラーマップ（ブルー $\rightarrow$ グリーン $\rightarrow$ アンバー $\rightarrow$ レッド）。
  - セルホバーによる強調枠線と、詳細情報（曜日、時間帯、平均値、Min/Max、集計サンプル数）の吹き出しツールチップ。
  - 下部に最小値・平均値・最大値のカラーバー凡例。

### ③ UI & ViewModel
- [`UpsMonitor.App/MainWindow.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml):
  - 新規タブ「Analytics（分析）」を追加。
  - 指標切り替えドロップダウン（消費電力、商用入力電圧、負荷率）。
  - 期間切り替えドロップダウン（過去7日間、過去30日間、過去90日間、全期間）。
  - 上部 4 枚のサマリー KPI カード（最多ピーク時間帯、最低待機時間帯、期間総合平均、集計サンプル数）。
- [`UpsMonitor.App/MainViewModel.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs):
  - Analytics タブ選択時の自動リフレッシュ、手動更新コマンド、指標/期間切り替え時の非同期集計処理。
- [`Strings.ja-JP.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.ja-JP.xaml) / [`Strings.en-US.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.en-US.xaml):
  - 日英の完全ローカライズ対応。

---

## 3. テストと動作検証

1. **単体テスト (`UpsMonitor.Core.Tests`)**:
   - `Weekly heatmap pattern aggregation` テストを新設し、SQLite への書き込みから 168 セルの週パターン集計・平均値・ピーク判定が正常に動作することを確認（19/19 テスト合格）。
2. **ソリューションビルド**:
   - `dotnet build UpsMonitor.sln` にて警告・エラー 0 件でビルド成功。
