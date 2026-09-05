# 修正内容の確認 (Walkthrough): UI コントラスト不具合の修正

## 変更概要
`docs/ui_contrast_fixes.md` の設計に基づき、ダーク/ライトテーマにおける DataGrid/GridView ヘッダーの白飛び、RadioButton の可読性、TextBox 選択色、および状態表示テキストのコントラスト比を改善しました。

## 変更内容詳細

### 1. DataGrid ヘッダーを共通スタイルへ集約
- [MainWindow.xaml](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/fix-contrast/UpsMonitor.App/MainWindow.xaml):
  - `Window.Resources` に `TelemetryGridColumnHeaderStyle`、`TelemetryGridCellStyle`、`TelemetryGridRowStyle` を新設。
  - `TelemetryGridColumnHeaderStyle` に hover (`PopupHoverBrush`) / pressed (`PopupSelectedBrush`) トリガーを設定。
  - `TelemetryGridStyle` に `ColumnHeaderStyle`、`CellStyle`、`RowStyle` を設定して一括適用。
  - HID データグリッド（`TelemetryItems`）のローカル重複スタイルを削除。

### 2. 電源トラブル一覧（ListView + GridView）へ既存スタイル適用
- [MainWindow.xaml](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/fix-contrast/UpsMonitor.App/MainWindow.xaml):
  - `TroubleSummary.TroubleEvents` の `GridView` に `ColumnHeaderContainerStyle="{StaticResource EventHeaderStyle}"` を適用。
  - 同親 `ListView` に `ItemContainerStyle="{StaticResource EventItemStyle}"` を適用。
  - `EventHeaderStyle` に hover (`PopupHoverBrush`) / pressed (`PopupSelectedBrush`) トリガーを追加。

### 3. RadioButton と TextBox 選択文字のテーマ対応
- [App.xaml](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/fix-contrast/UpsMonitor.App/App.xaml):
  - 暗黙的 `RadioButton` スタイル（`Foreground="{DynamicResource TextBrush}"`, `VerticalContentAlignment="Center"`, `Cursor="Hand"`）を追加。
  - `SelectionBackgroundBrush` / `SelectionForegroundBrush` を定義し、`TextBox` スタイルで参照。
- [ThemeManager.cs](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/fix-contrast/UpsMonitor.App/ThemeManager.cs):
  - dark (`#2563EB` / `#FFFFFF`)、light (`#1D4ED8` / `#FFFFFF`) の選択色ブラシを追加。

### 4. 状態表示の「面の色」と「文字の色」を分離
- [App.xaml](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/fix-contrast/UpsMonitor.App/App.xaml) & [ThemeManager.cs](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/fix-contrast/UpsMonitor.App/ThemeManager.cs):
  - `SuccessTextBrush` (dark: `#34D399`, light: `#047857`)
  - `WarningTextBrush` (dark: `#FBBF24`, light: `#92400E`)
  - `DangerTextBrush` (dark: `#F87171`, light: `#B91C1C`)
  - `InfoTextBrush` (dark: `#7DD3FC`, light: `#0369A1`)
  - `AccentContentBrush` (dark: `#111827`, light: `#111827`)
- [MainWindow.xaml](file:///C:/Users/geranium/orca/workspaces/cyberpowermon/fix-contrast/UpsMonitor.App/MainWindow.xaml):
  - ダッシュボード状態アイコン: `Foreground="{DynamicResource AccentContentBrush}"`
  - `LastError`: `Foreground="{DynamicResource DangerTextBrush}"`
  - `AvrBoost`: `Foreground="{DynamicResource TextBrush}"`
  - `PowerMargin`: `Foreground="{DynamicResource SuccessTextBrush}"`
  - `BatteryHealthDetailText`: `Foreground="{DynamicResource TextBrush}"`
  - `BatteryReplacementText`: `Foreground="{DynamicResource TextBrush}"`
  - `PeriodCostSummaryText`: `Foreground="{DynamicResource SuccessTextBrush}"`
  - `SimulatedRuntimeText`: `Foreground="{DynamicResource SuccessTextBrush}"`
  - `AnalyticsPeakHourText`: `Foreground="{DynamicResource WarningTextBrush}"`
  - `AnalyticsLowestHourText`: `Foreground="{DynamicResource InfoTextBrush}"`

## 検証結果
1. `dotnet build UpsMonitor.sln`: **0 警告、0 エラー** で成功。
2. `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`: **39/39 テスト PASS**。
