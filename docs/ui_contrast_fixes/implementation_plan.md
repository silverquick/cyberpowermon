# 実装計画: UI コントラスト不具合の修正

## 1. 目的
`docs/ui_contrast_fixes.md` の設計に厳密に従い、ダーク/ライトテーマにおける DataGrid/GridView ヘッダーの白飛び、RadioButton の可読性、TextBox 選択色、および状態表示テキストのコントラスト比を改善する。

## 2. 変更対象ファイルと具体的な変更内容

### 2.1 `UpsMonitor.App/App.xaml`
1. **暗黙的 RadioButton スタイルの追加**
   ```xml
   <Style TargetType="RadioButton">
       <Setter Property="Foreground" Value="{DynamicResource TextBrush}" />
       <Setter Property="VerticalContentAlignment" Value="Center" />
       <Setter Property="Cursor" Value="Hand" />
   </Style>
   ```
2. **選択用ブラシおよびセマンティックテキストブラシの初期定義追加**
   ```xml
   <SolidColorBrush x:Key="SelectionBackgroundBrush" Color="#2563EB" />
   <SolidColorBrush x:Key="SelectionForegroundBrush" Color="#FFFFFF" />
   <SolidColorBrush x:Key="SuccessTextBrush" Color="#34D399" />
   <SolidColorBrush x:Key="WarningTextBrush" Color="#FBBF24" />
   <SolidColorBrush x:Key="DangerTextBrush" Color="#F87171" />
   <SolidColorBrush x:Key="InfoTextBrush" Color="#7DD3FC" />
   <SolidColorBrush x:Key="AccentContentBrush" Color="#111827" />
   ```
3. **TextBox スタイルの SelectionBrush / SelectionTextBrush を DynamicResource に変更**
   ```xml
   <Setter Property="SelectionBrush" Value="{DynamicResource SelectionBackgroundBrush}" />
   <Setter Property="SelectionTextBrush" Value="{DynamicResource SelectionForegroundBrush}" />
   ```

### 2.2 `UpsMonitor.App/ThemeManager.cs`
1. **ダークテーマ辞書 (`IsDarkMode == true`) への追加**
   ```csharp
   ["SelectionBackgroundBrush"] = "#2563EB",
   ["SelectionForegroundBrush"] = "#FFFFFF",
   ["SuccessTextBrush"] = "#34D399",
   ["WarningTextBrush"] = "#FBBF24",
   ["DangerTextBrush"] = "#F87171",
   ["InfoTextBrush"] = "#7DD3FC",
   ["AccentContentBrush"] = "#111827",
   ```
2. **ライトテーマ辞書 (`IsDarkMode == false`) への追加**
   ```csharp
   ["SelectionBackgroundBrush"] = "#1D4ED8",
   ["SelectionForegroundBrush"] = "#FFFFFF",
   ["SuccessTextBrush"] = "#047857",
   ["WarningTextBrush"] = "#92400E",
   ["DangerTextBrush"] = "#B91C1C",
   ["InfoTextBrush"] = "#0369A1",
   ["AccentContentBrush"] = "#111827",
   ```

### 2.3 `UpsMonitor.App/MainWindow.xaml`
1. **DataGrid 共通スタイルの新設と `TelemetryGridStyle` への統合**
   - `Window.Resources` に `TelemetryGridColumnHeaderStyle`, `TelemetryGridCellStyle`, `TelemetryGridRowStyle` を追加。
   - `TelemetryGridColumnHeaderStyle`:
     - `Background={DynamicResource NavigationBrush}`
     - `Foreground={DynamicResource TextBrush}`
     - `BorderBrush={DynamicResource BorderBrush}`
     - `Padding="8"`
     - `HorizontalContentAlignment="Left"`
     - Trigger `IsMouseOver=True` -> `Background={DynamicResource PopupHoverBrush}`, `Foreground={DynamicResource TextBrush}`
     - Trigger `IsPressed=True` -> `Background={DynamicResource PopupSelectedBrush}`, `Foreground={DynamicResource TextBrush}`
   - `TelemetryGridCellStyle`:
     - `Padding="7,5"`
     - `BorderThickness="0"`
     - `Foreground="{DynamicResource TextBrush}"`
     - Trigger `IsSelected=True` -> `Background="{DynamicResource SelectedNavBrush}"`, `Foreground="{DynamicResource TextBrush}"`
   - `TelemetryGridRowStyle`:
     - `Foreground="{DynamicResource TextBrush}"`
     - Trigger `IsSelected=True` -> `Background="{DynamicResource SelectedNavBrush}"`, `Foreground="{DynamicResource TextBrush}"`
     - Trigger `IsMouseOver=True` -> `Background="{DynamicResource HoverBrush}"`
   - `TelemetryGridStyle` に以下を設定:
     - `ColumnHeaderStyle="{StaticResource TelemetryGridColumnHeaderStyle}"`
     - `CellStyle="{StaticResource TelemetryGridCellStyle}"`
     - `RowStyle="{StaticResource TelemetryGridRowStyle}"`
   - HID グリッド（`TelemetryItems`）のローカル `<DataGrid.Resources>` を削除。
2. **`EventHeaderStyle` への hover / pressed トリガー追加と電源トラブル一覧への適用**
   - `EventHeaderStyle` に Trigger `IsMouseOver=True` (Background: `PopupHoverBrush`), Trigger `IsPressed=True` (Background: `PopupSelectedBrush`) を追加。
   - `TroubleSummary.TroubleEvents` の `GridView` に `ColumnHeaderContainerStyle="{StaticResource EventHeaderStyle}"` を指定。
   - 同親 `ListView` に `ItemContainerStyle="{StaticResource EventItemStyle}"` を指定。
3. **固定色 Foreground および状態色バインディングの置き換え**
   - ダッシュボード状態アイコン: `Foreground="White"` -> `Foreground="{DynamicResource AccentContentBrush}"`
   - `LastError`: `Foreground="#EF4444"` -> `Foreground="{DynamicResource DangerTextBrush}"`
   - `AvrBoost`: `Foreground="{Binding AvrBoostAccent}"` -> `Foreground="{DynamicResource TextBrush}"`
   - `PowerMargin`: `Foreground="#10B981"` -> `Foreground="{DynamicResource SuccessTextBrush}"`
   - `BatteryHealthDetailText`: `Foreground="{Binding BatteryHealthAccent}"` -> `Foreground="{DynamicResource TextBrush}"`
   - `BatteryReplacementText`: `Foreground="{Binding BatteryReplacementAccent}"` -> `Foreground="{DynamicResource TextBrush}"`
   - `PeriodCostSummaryText`: `Foreground="#10B981"` -> `Foreground="{DynamicResource SuccessTextBrush}"`
   - `SimulatedRuntimeText`: `Foreground="#10B981"` -> `Foreground="{DynamicResource SuccessTextBrush}"`
   - `AnalyticsPeakHourText`: `Foreground="#F59E0B"` -> `Foreground="{DynamicResource WarningTextBrush}"`
   - `AnalyticsLowestHourText`: `Foreground="#38BDF8"` -> `Foreground="{DynamicResource InfoTextBrush}"`

## 3. 検証方針
1. `dotnet build UpsMonitor.sln` が 0 エラーでビルド完了すること。
2. `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj` が全件 PASS すること。
3. リソースキー名、StaticResource/DynamicResource の参照関係にタイポや誤りがないことを精査。
