# タスク: 施策 #2 Analytics タブのアクティブ判定修正（Phase A）

【役割】実装担当。この worktree（cyberpowermon / WPF / net10.0-windows、ブランチ `silverquick/impl-2-analytics`、main から分岐済み）で施策 #2 のみを実装する。

【設計書】`docs/improvement_backlog.md` の「## 2. Analytics タブのアクティブ判定修正」を必ず読むこと。

## 現状の不具合
`MainWindow.xaml` のタブ順は Dashboard=0, History=1, UPS=2, Analytics=3。
しかし `UpsMonitor.App/MainViewModel.cs` の `SelectedNavigationIndex` / `IsWindowVisible` / `OnLanguageChanged` は Analytics を index `2` と誤判定している。
結果、Analytics タブを開いても即時リフレッシュされず、UPS タブ選択時に不要な Analytics 集計が走る。

## 実装内容
1. `MainViewModel.cs` に散在するタブ index の数値リテラル比較を廃止し、名前付き定数（少なくとも `DashboardIndex=0`, `HistoryIndex=1`, `UpsIndex=2`, `AnalyticsIndex=3`）と判定ヘルパ（`IsHistoryRefreshTarget(int)`, `IsAnalyticsRefreshTarget(int)`）に集約する。
2. `SelectedNavigationIndex` setter、`IsWindowVisible` setter、`OnLanguageChanged` の 3 箇所を同じ判定ヘルパ経由にする。Analytics は index 3。
3. Analytics 選択時は最新リクエストを cancel してから `RefreshAnalyticsAsync` を 1 回だけ実行。二重リフレッシュ・キャンセル競合を起こさない。
4. `MainWindow.xaml` のタブ順は既に正しいので構造は変更しない。

## テスト
`UpsMonitor.Core.Tests/Program.cs` にテストを追加（手製テストランナー: 先頭の `tests` 配列にエントリ追加、ファイル末尾に static メソッド追加）:
- 各 index の refresh routing 判定
- Analytics 選択時に Analytics 取得が 1 回 / UPS 選択時は取得なし
- 非表示時は取得せず、再表示時に取得
- 言語変更時に現在タブを再取得
（内部判定を検証しにくい場合は判定ヘルパを `internal` + `InternalsVisibleTo`、または純粋関数に切り出す）

## 変更してよいファイル
- `UpsMonitor.App/MainViewModel.cs`
- `UpsMonitor.Core.Tests/Program.cs`
- 必要なら `UpsMonitor.App/UpsMonitor.App.csproj`（`InternalsVisibleTo` 追加のみ）

## 触ってはいけないファイル
`MainWindow.xaml(.cs)`, `SqliteTelemetryQueries.cs`, `TelemetryHistory.cs`, `UpsEvents.cs`, `Monitoring.cs`, `AppConfiguration.cs`, リソース XAML（後続 Phase の担当）。

## 完了条件
1. `dotnet build UpsMonitor.sln` が 0 エラー
2. `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj` が全テスト PASS
3. `git add -A && git commit`（日本語コミットメッセージ、例: `fix(ui): Analytics タブのアクティブ判定ずれを修正`）
4. **この TASK.md の末尾に `## 完了報告` セクションを追記**（変更ファイル一覧・追加テスト名・build 結果・test 結果・コミットハッシュ）。これが完了の合図。

## 完了報告

### 変更ファイル一覧
- `UpsMonitor.App/MainViewModel.cs` : タブインデックス定数（`DashboardIndex=0`, `HistoryIndex=1`, `UpsIndex=2`, `AnalyticsIndex=3`, 等）および判定ヘルパ（`IsHistoryRefreshTarget`, `IsAnalyticsRefreshTarget`）を定義し、`SelectedNavigationIndex`, `IsWindowVisible`, `OnLanguageChanged`, `ApplySnapshot` のリフレッシュルーティング判定を集約。
- `UpsMonitor.Core.Tests/Program.cs` : ナビゲーションルーティング判定ルール、タブ選択時、表示状態変更時、言語変更時のテストを追加。
- `docs/analytics_tab_active_index_fix/task.md` : タスク一覧
- `docs/analytics_tab_active_index_fix/implementation_plan.md` : 実装計画書
- `docs/analytics_tab_active_index_fix/walkthrough.md` : 修正内容の確認書

### 追加テスト名
- `Navigation tab refresh routing rules`
- `Navigation refresh on tab selection`
- `Navigation refresh on visibility change`
- `Navigation refresh on language change`

### Build 結果
- `dotnet build UpsMonitor.sln`: 0 警告、0 エラー（ビルド成功）

### Test 結果
- `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`: 全 32 テスト PASS (32/32 tests passed)

### コミットハッシュ
- `4937d6f` (`fix(ui): Analytics タブのアクティブ判定ずれを修正`)

