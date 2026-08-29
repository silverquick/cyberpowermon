# 変更内容の確認 (Walkthrough): 施策 #2 Analytics タブのアクティブ判定修正

## 変更概要
`MainWindow.xaml` のタブ順序（Dashboard=0, History=1, UPS=2, Analytics=3）と `MainViewModel.cs` のタブ判定ロジックの不整合を修正しました。

## 修正内容

### 1. `UpsMonitor.App/MainViewModel.cs`
- タブインデックスの名前付き定数を定義：
  - `DashboardIndex = 0`
  - `HistoryIndex = 1`
  - `UpsIndex = 2`
  - `AnalyticsIndex = 3`
  - `DevicesIndex = 4`, `ActionsIndex = 5`, `LogsIndex = 6`, `SettingsIndex = 7`
- 判定ヘルパメソッドを追加：
  - `IsHistoryRefreshTarget(int index) => index is DashboardIndex or HistoryIndex;`
  - `IsAnalyticsRefreshTarget(int index) => index is AnalyticsIndex;`
- リフレッシュ判定箇所の集約：
  - `SelectedNavigationIndex` setter: `IsHistoryRefreshTarget`, `IsAnalyticsRefreshTarget` を使用
  - `IsWindowVisible` setter: `IsHistoryRefreshTarget`, `IsAnalyticsRefreshTarget` を使用
  - `OnLanguageChanged`: `IsHistoryRefreshTarget`, `IsAnalyticsRefreshTarget` を使用
  - `ApplySnapshot`: `IsHistoryRefreshTarget` を使用

### 2. `UpsMonitor.Core.Tests/Program.cs`
以下の 4 つのテストを追加：
1. `Navigation tab refresh routing rules`: 各タブインデックス（0〜7, 負数, 範囲外）に対するルーティング判定の正確性を網羅検証。
2. `Navigation refresh on tab selection`: UPS タブ（2）選択時に Analytics / History 取得が走らず、Analytics タブ（3）選択時に 1 回のみ取得が走ること、短時間での多重呼び出し時に先行 CTS がキャンセルされることを検証。
3. `Navigation refresh on visibility change`: ウィンドウ非表示時には取得を行わず、再表示時に選択中タブに応じて取得が実行されることを検証。
4. `Navigation refresh on language change`: 表示中の言語変更時に現在のアクティブタブ（Analytics または History）に応じた再取得が実行され、非表示中や UPS タブでは不要な取得が走らないことを検証。

## 検証結果
- `dotnet build UpsMonitor.sln`: 0 警告、0 エラーでビルド成功
- `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`: 全 32 テスト PASS
