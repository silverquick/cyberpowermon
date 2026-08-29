# 実装計画: 曜日×24時間 週間ヒートマップ分析タブの実装

## 概要
UPS で蓄積したテレメトリデータ（消費電力、商用入力電圧、負荷率、力率等）を、1週間（7日間）× 24時間のマトリクスでグラフィカルに可視化・分析できる新機能「**Analytics（分析）タブ**」を追加します。

これにより、「何曜日の何時頃に電力消費がピークを迎えるか」「夜間の待機電力はどれくらいか」「地域の商用電圧がどの時間帯にドロップしやすいか」といった生活リズムや電源環境の傾向をひと目で直感的に分析できるようになります。

---

## 主な機能とアーキテクチャ

### 1. データ集計モデル (`UpsMonitor.Core` & `UpsMonitor.Infrastructure`)
- `HourlyPatternPoint`: 曜日（0〜6）× 時間帯（0〜23）の平均値・最小値・最大値・サンプル数。
- `WeeklyPatternResult`: 168 セル（7×24）のグリッドデータおよび全体の統計情報（Min, Max, Avg, ピーク時間帯, 最低時間帯）。
- `SqliteTelemetryQueries.QueryWeeklyPatternAsync`: SQLite の `telemetry_rollups_1m` から `strftime('%w')` / `strftime('%H')` を用いて高速に集計。

### 2. WPF カスタムヒートマップコントロール (`WeeklyHeatmapControl.cs`)
- Direct2D / DrawingContext を用いた高パフォーマンスなカスタム描画。
- 7行（月〜日）× 24列（0〜23時）のセル描画。
- 指標の範囲（Min〜Max）に応じたスムーズなカラーグラデーション（低: スレート/ブルー、中: エメラルド/シアン、高: アンバー/レッド）。
- 各セルのマウスホバー時に詳細ポップアップ（曜日・時間帯・平均値・最小/最大・サンプル数）を表示。
- 下部にグラデーションカラーバー凡例を描画。

### 3. Analytics タブ UI (`MainWindow.xaml` & `MainViewModel.cs`)
- **指標切り替えドロップダウン**:
  - 消費電力 (W)
  - 入力商用電圧 (V)
  - 負荷率 (%)
  - 力率 (%)
- **集計期間ドロップダウン**:
  - 過去7日間、過去30日間、過去90日間、全期間
- **インサイトサマリーカード**:
  - 最頻ピーク時間帯（曜日と時間、平均電力）
  - 最低待機時間帯（曜日と時間、待機電力）
  - 昼夜変動差・電圧安定度
- **メインカード**:
  - `WeeklyHeatmapControl` による大画面ヒートマップ

---

## 変更対象ファイル
- [`UpsMonitor.Core/TelemetryHistory.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core/TelemetryHistory.cs): パターン集計用の型定義
- [`UpsMonitor.Infrastructure/SqliteTelemetryQueries.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/SqliteTelemetryQueries.cs): SQLite 週間パターン集計クエリ
- [`UpsMonitor.Infrastructure/SqliteTelemetryStore.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/SqliteTelemetryStore.cs): インターフェース公開
- [`UpsMonitor.App/WeeklyHeatmapControl.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/WeeklyHeatmapControl.cs): 新規作成
- [`UpsMonitor.App/MainViewModel.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs): Analytics プロパティと集計コマンド
- [`UpsMonitor.App/MainWindow.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml): Analytics タブマークアップ
- [`UpsMonitor.App/Resources/Strings.ja-JP.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.ja-JP.xaml) / [`Strings.en-US.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.en-US.xaml): 多言語ラベル
- [`UpsMonitor.Core.Tests/Program.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core.Tests/Program.cs): パターン集計の単体テスト

---

## 検証計画
- `UpsMonitor.Core.Tests` で週パターン集計ロジックの単体テストを実行。
- `dotnet build UpsMonitor.sln` でビルドが通ることを確認。
