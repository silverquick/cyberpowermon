# 実装計画: 施策 #2 Analytics タブのアクティブ判定修正

## 1. 目的と背景
`MainWindow.xaml` におけるナビゲーションタブの並び順は以下のとおりです：
- Index 0: Dashboard
- Index 1: History
- Index 2: UPS
- Index 3: Analytics
- Index 4: Devices
- Index 5: Actions
- Index 6: Logs
- Index 7: Settings

しかし、従来の `MainViewModel.cs` 内では数値リテラル `2` を Analytics タブと誤判定していました。これにより、以下の不具合が生じていました：
1. Analytics タブ（Index 3）を開いても即時リフレッシュがトリガーされない
2. UPS タブ（Index 2）を選択した際に、不要な Analytics 集計クエリが実行されてしまう

## 2. 変更方針
1. **名前付き定数と判定ヘルパの導入**
   - `MainViewModel.cs` に各タブインデックスの定数（`DashboardIndex = 0`, `HistoryIndex = 1`, `UpsIndex = 2`, `AnalyticsIndex = 3`, 等）を定義。
   - `IsHistoryRefreshTarget(int)`（Dashboard または History を対象）および `IsAnalyticsRefreshTarget(int)`（Analytics のみを対象）ヘルパ関数を作成。
2. **呼び出し元の集約**
   - `SelectedNavigationIndex` setter
   - `IsWindowVisible` setter
   - `OnLanguageChanged`
   - `ApplySnapshot`
   の各所で判定ヘルパを経由するように統一。
3. **テストの追加**
   - `UpsMonitor.Core.Tests/Program.cs` に判定ルール、タブ選択時、表示状態変更時、言語変更時のリフレッシュ動作検証テストを追加。

## 3. 影響範囲
- 変更ファイル:
  - `UpsMonitor.App/MainViewModel.cs`
  - `UpsMonitor.Core.Tests/Program.cs`
- XAML構造およびCoreビジネスロジックへの影響なし。
