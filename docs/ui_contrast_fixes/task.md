# タスク: UI コントラスト不具合の修正

## 概要
`docs/ui_contrast_fixes.md` の設計に基づき、ダーク/ライトテーマにおける DataGrid/GridView ヘッダー、RadioButton、TextBox 選択色、および状態表示テキストのコントラスト不具合を修正する。

## タスクリスト

- [x] 1. ドキュメント整備 (`task.md`, `implementation_plan.md`) <!-- id: 0 -->
- [x] 2. `App.xaml` および `ThemeManager.cs` へのブラシとスタイルの追加 <!-- id: 1 -->
  - [x] `SelectionBackgroundBrush`, `SelectionForegroundBrush` の定義と TextBox スタイルの更新 <!-- id: 1.1 -->
  - [x] 暗黙的 `RadioButton` スタイルの追加 <!-- id: 1.2 -->
  - [x] セマンティックテキストブラシ (`SuccessTextBrush`, `WarningTextBrush`, `DangerTextBrush`, `InfoTextBrush`, `AccentContentBrush`) の定義 <!-- id: 1.3 -->
- [x] 3. `MainWindow.xaml` のスタイル統合とコントラスト修正 <!-- id: 2 -->
  - [x] `TelemetryGridColumnHeaderStyle`, `TelemetryGridCellStyle`, `TelemetryGridRowStyle` を新設し `TelemetryGridStyle` に統合 <!-- id: 2.1 -->
  - [x] HID グリッドのローカルスタイル重複を削除 <!-- id: 2.2 -->
  - [x] 電源トラブル一覧（GridView/ListView）に既存ヘッダー・アイテムスタイルを適用、`EventHeaderStyle` に hover/pressed トリガーを追加 <!-- id: 2.3 -->
  - [x] 固定色 Foreground および状態色バインディングをセマンティックブラシ/TextBrush に置き換え <!-- id: 2.4 -->
- [x] 4. ビルドとテストの検証 <!-- id: 3 -->
  - [x] `dotnet build UpsMonitor.sln` が 0 エラーで成功することを確認 <!-- id: 3.1 -->
  - [x] `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj` が全件 PASS することを確認 <!-- id: 3.2 -->
- [x] 5. コミットと完了報告 <!-- id: 4 -->
  - [x] 日本語メッセージで Git コミット <!-- id: 4.1 -->
  - [x] `TASK.md` 末尾へ完了報告を追記 <!-- id: 4.2 -->
  - [x] `walkthrough.md` の作成 <!-- id: 4.3 -->
