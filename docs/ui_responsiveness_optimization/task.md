# タスク: UI応答性・描画パフォーマンス分析と改良

## タスク概要
WPF/.NET UPS監視アプリ (PowerGuard / cyberpowermon) におけるUI応答性・描画パフォーマンスの課題を分析し、最適化を行う。

## タスク一覧
- [x] 現状のコードベース・アーキテクチャの調査と課題抽出
  - [x] UIスレッド同期処理・重い計算の確認
  - [x] PropertyChanged通知の過剰発火の確認 (`MainViewModel.cs`)
  - [x] DataGrid/Chart再描画コストの確認 (`TimeSeriesChart.cs`, `WeeklyHeatmapControl.cs`, `UpsStateTimeline.cs`)
  - [x] TrayIconManagerのアイコン再生成頻度とGDIリソース破棄漏れの確認 (`TrayIconManager.cs`)
  - [x] MiniMonitorWindowの描画負荷の確認 (`MiniMonitorWindow.xaml`)
  - [x] タイマー/ポーリング間隔の確認
- [x] `docs/performance/ui_responsiveness_analysis.md` の作成
- [x] パフォーマンス改善の実装
  - [x] `TrayIconManager.cs` の最適化 (GDIブラシリーク修正、アイコン・ツールチップ更新の差分スキップ)
  - [x] `MainViewModel.cs` の最適化 (PropertyChanged過剰発火防止、差分チェック導入、不要な再計算・再取得の抑制)
  - [x] `TimeSeriesChart.cs` の最適化 (二分探索化、LINQ/アロケーション削減、Brush/PenのFreeze & キャッシュ)
  - [x] `WeeklyHeatmapControl.cs` の最適化 (O(1)グリッドルックアップ、BrushのFreeze)
  - [x] `UpsStateTimeline.cs` の最適化 (静的Freeze済みBrushの再利用)
  - [x] `MiniMonitorWindow.xaml` の最適化 (`RenderingBias="Performance"`)
- [x] ビルドおよびテストの実行・確認
  - [x] `dotnet build UpsMonitor.sln`
  - [x] `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`
- [ ] git commit の実行 (日本語コミットメッセージ)
- [ ] `TASK.md` 末尾への完了報告追記
- [x] `walkthrough.md` の作成
