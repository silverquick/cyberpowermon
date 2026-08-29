# タスク: 施策 #2 Analytics タブのアクティブ判定修正

## 概要
`MainWindow.xaml` のタブ順序（Dashboard=0, History=1, UPS=2, Analytics=3）に対し、`MainViewModel.cs` 内のタブ判定（Index 2 を Analytics と誤認していた問題）を修正し、判定ヘルパおよび名前付き定数へ集約する。

## タスクリスト

- [x] 1. 現状コードと設計書の調査 (`TASK.md`, `docs/improvement_backlog.md`, `MainViewModel.cs`, `MainWindow.xaml`)
- [x] 2. `MainViewModel.cs` にタブ定数と判定ヘルパを定義
  - `DashboardIndex = 0`, `HistoryIndex = 1`, `UpsIndex = 2`, `AnalyticsIndex = 3`, `DevicesIndex = 4`, `ActionsIndex = 5`, `LogsIndex = 6`, `SettingsIndex = 7`
  - `IsHistoryRefreshTarget(int)`, `IsAnalyticsRefreshTarget(int)`
- [x] 3. `MainViewModel.cs` の各処理を判定ヘルパ経由に変更
  - `SelectedNavigationIndex` setter
  - `IsWindowVisible` setter
  - `OnLanguageChanged`
  - `ApplySnapshot`
- [x] 4. テストケースの追加 (`UpsMonitor.Core.Tests/Program.cs`)
  - 各 index の refresh routing 判定ルール
  - タブ選択時のリフレッシュ動作（UPS 選択時は無動作、Analytics 選択時は 1 回取得、キャンセル処理）
  - ウィンドウ表示状態遷移時のリフレッシュ動作
  - 言語変更時のアクティブタブ再取得
- [x] 5. ソリューション全体のビルドと全テスト実行（0 警告、0 エラー、全テスト PASS）
- [x] 6. 日本語コミットメッセージによるコミット
- [x] 7. `TASK.md` 末尾へ「## 完了報告」の追記
