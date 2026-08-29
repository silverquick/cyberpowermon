# タスクリスト: 曜日×24時間 週間ヒートマップ分析タブの実装

## 1. コア & データアクセス層の実装
- [x] 1-1. `UpsMonitor.Core` に `WeeklyPatternResult`, `HourlyPatternPoint` 等のデータ構造を定義 <!-- id: 1-1 -->
- [x] 1-2. `SqliteTelemetryQueries.cs` に曜日・時間帯別集計クエリ (`QueryWeeklyPatternAsync`) を実装 <!-- id: 1-2 -->
- [x] 1-3. `SqliteTelemetryStore.cs` にインターフェースメソッドを追加 <!-- id: 1-3 -->

## 2. WPF カスタムヒートマップコントロールの実装
- [x] 2-1. `WeeklyHeatmapControl.cs` を新規作成（7×24セル描画、グラデーションカラーマップ、ホバーツールチップ、カラーバー凡例） <!-- id: 2-1 -->

## 3. ViewModel & 多言語リソースの拡張
- [x] 3-1. `MainViewModel.cs` に Analytics タブ用プロパティ（指標選択、期間選択、集計結果、ピーク/待機サマリー）と更新ロジックを実装 <!-- id: 3-1 -->
- [x] 3-2. 日英リソースファイル (`Strings.ja-JP.xaml`, `Strings.en-US.xaml`) に必要なラベル・文言を追加 <!-- id: 3-2 -->

## 4. UI マークアップの更新
- [x] 4-1. `MainWindow.xaml` に「Analytics（分析）」タブを追加し、ヒートマップコントロールとサマリーカードを配置 <!-- id: 4-1 -->

## 5. テスト・ビルド・動作確認
- [x] 5-1. 単体テストの作成・実行とソリューション全体のビルド確認 <!-- id: 5-1 -->
- [x] 5-2. ウォークスルー (`walkthrough.md`) の作成 <!-- id: 5-2 -->
