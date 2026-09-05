# UI コントラスト不具合の調査結果と修正設計

最終調査日: 2026-09-05

調査基準: `6315c7f` (`silverquick/contrast-audit`)

対象: `UpsMonitor.App/` の XAML、`ThemeManager.cs`、および色値を XAML の `Foreground` に供給する `MainViewModel.cs`

## 結論

スクリーンショットで確認された 3 つの白い列ヘッダー帯には、2 種類の根本原因がある。

1. 「消費電力量 & 推定電気代レポート」と「標準負荷別ランタイム一覧」は `DataGrid` であり、共通の `TelemetryGridStyle` に `DataGridColumnHeader` の配色がない。
2. 「直近の電源トラブルイベント」は `DataGrid` ではなく `ListView` + `GridView` であり、既存の `EventHeaderStyle` がその `GridView` に適用されていない。

したがって、3 画面すべてを `DataGridColumnHeader` の修正だけで直すことはできない。`DataGrid` 側は 1 つの共通スタイルへ集約し、電源トラブル一覧には既存の `GridViewColumnHeader` スタイルを適用する必要がある。

調査で列挙した問題は 8 件である。うち #1 と #2 が今回の白い帯を直接説明する確定原因で、#3〜#8 は同じ監査で見つかったテーマ依存または数値上のコントラスト問題である。

## リソースとスタイルの配置

- `UpsMonitor.App/App.xaml`
  - ダークテーマを初期値とする 14 個のブラシを定義している。
  - `TextBlock`、`Button`、`CheckBox`、`TextBox`、`ComboBox`、`ComboBoxItem`、`ScrollBar`、`TabItem`、`ContextMenu`、`MenuItem`、`Separator` の暗色対応スタイルを持つ。
  - `DataGrid`、`DataGridColumnHeader`、`DataGridRow`、`DataGridCell`、`RadioButton`、`ToolTip` のアプリ共通スタイルはない。
- `UpsMonitor.App/MainWindow.xaml` の `Window.Resources`
  - `TelemetryGridStyle` が全 3 個の `DataGrid` に適用されている。
  - `TelemetryGridStyle` は本体、行背景、文字、罫線を設定するが、`ColumnHeaderStyle`、`RowStyle`、`CellStyle` は設定しない。
  - `EventHeaderStyle` と `EventItemStyle` は Logs タブの `ListView` では使用されるが、履歴タブの電源トラブル一覧では使用されない。
- `UpsMonitor.App/MiniMonitorWindow.xaml`
  - 独自リソースはなく、`App.xaml` の `DynamicResource` を使用している。
- 独立した `Styles/` またはテーマ用 `ResourceDictionary` は存在しない。日英の `Resources/Strings.*.xaml` は文字列だけである。
- `ThemeManager.cs`
  - `WindowBrush` など同一キーのブラシを dark/light それぞれで差し替える。主要 XAML はブラシ参照に `DynamicResource` を使うため、アプリ内部の配色はテーマ変更直後に追従する。
  - 一方、XAML や ViewModel の `#RRGGBB` は差し替え対象外である。

## 発見した問題一覧

| # | 対象コントロール | 対象ファイル | 現象 | 根本原因 | 確証度 |
|---:|---|---|---|---|---|
| 1 | `DataGridColumnHeader` | `MainWindow.xaml` (`TelemetryGridStyle`、`EnergyReports`、`StandardLoadEstimates`) | ダークテーマで列ヘッダーが白い帯になり、列名がほぼ見えない。 | `TelemetryGridStyle` は `Foreground=TextBrush` を設定する一方、`ColumnHeaderStyle` を持たない。ヘッダー面は WPF/Windows の明色既定値に残り、ヘッダー文字はアプリの明色テキストになる。代表的な system control 背景 `#F0F0F0` と `TextBrush #F8FAFC` は約 1.09:1。 | **高（スクリーンショットと定義が一致）** |
| 2 | `GridViewColumnHeader` | `MainWindow.xaml` (`TroubleSummary.TroubleEvents`) | 「直近の電源トラブルイベント」も同じ白いヘッダー帯になる。 | 当該一覧は `DataGrid` ではなく `ListView` + `GridView`。同じファイルに安全な `EventHeaderStyle` があるが、この `GridView` だけ `ColumnHeaderContainerStyle` を指定していない。 | **高（スクリーンショットと定義が一致）** |
| 3 | `ListViewItem` | `MainWindow.xaml` (`TroubleSummary.TroubleEvents`) | 電源トラブル行の選択・ホバー色が OS 既定テーマに依存し、ダーク/ライトの一方で文字とのコントラストが崩れる可能性がある。 | 当該 `ListView` だけ `ItemContainerStyle` がなく、既存の `EventItemStyle` の `HoverBrush` / `SelectedNavBrush` / `TextBrush` の組み合わせを使っていない。 | 中（未選択行の問題はないが状態色が未統制） |
| 4 | `RadioButton` | `App.xaml`、`MainWindow.xaml`（日次/月次切り替え） | ダークテーマで「日」「月」のラベルが暗いカード面に沈む可能性が高い。 | `RadioButton` の暗色対応スタイルがない。現在の WPF 既定 `Foreground` は黒で、`PanelBrush` は暗色である。ライトテーマでは偶然安全になる OS 依存の組み合わせである。 | 高（現行 WPF の既定値と XAML を確認） |
| 5 | 固定色の `TextBlock.Foreground` | `MainWindow.xaml` | ライトテーマでエラー、電力余裕、推定費用、シミュレーション結果、Analytics のピーク/最低値が白いカード面に対して低コントラストになる。 | `#EF4444`、`#10B981`、`#F59E0B`、`#38BDF8` が dark/light 共通で固定される。白背景との比率は順に約 3.76:1、2.54:1、2.15:1、2.14:1 で、通常文字の 4.5:1 を下回る。 | **高（色値から算出）** |
| 6 | 状態色を直接使う `TextBlock.Foreground` | `MainWindow.xaml`、`MainViewModel.cs` (`AvrBoostAccent`、`BatteryHealthAccent`、`BatteryReplacementAccent`) | 特にライトテーマで、緑・黄・水色・灰色などの状態テキストが白いカード面に沈む。 | ViewModel が dark/light を区別しない固定カラー文字列を返し、同じ値を境界・ドットなどの非テキスト用途と文字色用途に兼用している。 | **高（色の供給元まで確認）** |
| 7 | 状態アイコン | `MainWindow.xaml`（ダッシュボード状態カード） | 白い UPS アイコンが Online の緑や OnBattery の黄背景に対して弱い。 | `Foreground="White"` と `Background="{Binding StatusAccent}"` の固定組み合わせ。白との比率は Online `#22C55E` で約 2.28:1、OnBattery `#F59E0B` で約 2.15:1、LowBattery `#F97316` で約 2.80:1。 | **高（色値から算出）** |
| 8 | `TextBox` の選択文字 | `App.xaml` | 選択範囲の白文字は見えるが、通常サイズ文字の基準には届かない。 | `SelectionBrush=#3B82F6` と `SelectionTextBrush=White` がハードコードされ、比率は約 3.68:1。テーマ用リソースとして調整できない。 | **高（色値から算出、深刻度は低）** |

比率は sRGB の WCAG 相対輝度式による。#1 の system control 色は代表値であり Windows テーマにより変わり得るが、「一方だけアプリテーマ、他方は OS 既定」という構造上の問題は変わらない。

## 全 DataGrid と表形式 UI の判定

`UpsMonitor.App` 以下を `<DataGrid` で検索した結果、実体は次の 3 個だけであった。

| 画面 | ItemsSource | 適用スタイル | ヘッダー判定 | 備考 |
|---|---|---|---|---|
| 履歴 / 消費電力量 & 推定電気代 | `EnergyReports` | `TelemetryGridStyle` | **不具合あり** | 個別 `DataGridColumnHeader` スタイルなし |
| 履歴 / 標準負荷別ランタイム | `StandardLoadEstimates` | `TelemetryGridStyle` | **不具合あり** | 個別 `DataGridColumnHeader` スタイルなし |
| UPS / すべての HID データ | `TelemetryItems` | `TelemetryGridStyle` + ローカル `DataGrid.Resources` | 現状は回避済み | ヘッダー、セル、行の暗色スタイルを当該グリッドだけ重複定義している |

補足:

- 履歴 / 直近の電源トラブルイベントは `ListView` + `GridView` であり、DataGrid ではない。#2 の修正が必要である。
- Logs タブも `ListView` + `GridView` であり、`EventHeaderStyle` と `EventItemStyle` が適用済みなので同じ白帯は発生しない設計である。
- Devices タブは将来追加予定のプレースホルダーのみで、DataGrid はない。

## 修正方針

### 1. DataGrid ヘッダーを 1 箇所の共通スタイルへ集約する

対象: `UpsMonitor.App/MainWindow.xaml`

`Window.Resources` で `TelemetryGridStyle` より前に、次のキーを持つスタイルを定義する。

- `TelemetryGridColumnHeaderStyle` (`DataGridColumnHeader`)
  - `Background={DynamicResource NavigationBrush}`
  - `Foreground={DynamicResource TextBrush}`
  - `BorderBrush={DynamicResource BorderBrush}`
  - `Padding=8`
  - `HorizontalContentAlignment=Left`
  - `IsMouseOver=True` では `PopupHoverBrush`、`IsPressed=True` では `PopupSelectedBrush` を使用し、どちらも `Foreground=TextBrush` を維持する。
- `TelemetryGridCellStyle` (`DataGridCell`)
  - 現在 HID グリッド内にある padding、border、foreground、選択時 background/foreground を移す。
- `TelemetryGridRowStyle` (`DataGridRow`)
  - 現在 HID グリッド内にある foreground と選択時 background/foreground を移す。必要ならホバーに `HoverBrush` を追加する。

そのうえで `TelemetryGridStyle` に以下の setter を追加する。

- `ColumnHeaderStyle={StaticResource TelemetryGridColumnHeaderStyle}`
- `CellStyle={StaticResource TelemetryGridCellStyle}`
- `RowStyle={StaticResource TelemetryGridRowStyle}`

UPS / HID の `DataGrid.Resources` にある 3 個のローカルスタイルは削除する。これにより現在の全 3 グリッドと、今後 `TelemetryGridStyle` を使用する DataGrid が同じ状態配色になる。個別 DataGrid への `ColumnHeaderStyle` 追記は行わない。

### 2. 電源トラブル一覧へ既存の GridView スタイルを適用する

対象: `UpsMonitor.App/MainWindow.xaml`

- `TroubleSummary.TroubleEvents` の `GridView` に `ColumnHeaderContainerStyle={StaticResource EventHeaderStyle}` を追加する。
- 親 `ListView` に `ItemContainerStyle={StaticResource EventItemStyle}` を追加する。
- `EventHeaderStyle` にも `IsMouseOver` / `IsPressed` トリガーを加え、`PopupHoverBrush` / `PopupSelectedBrush` と `TextBrush` の組み合わせを明示する。Logs と履歴の両方へ反映される。

### 3. 文字入力と RadioButton の共通テーマ対応

対象: `UpsMonitor.App/App.xaml`、`UpsMonitor.App/ThemeManager.cs`

- `App.xaml` に暗色対応の暗黙的 `RadioButton` スタイルを追加し、少なくとも `Foreground={DynamicResource TextBrush}` を設定する。
- `SelectionBackgroundBrush` と `SelectionForegroundBrush` を追加し、`TextBox` の `SelectionBrush` / `SelectionTextBrush` はその `DynamicResource` を使う。
- 推奨値は dark で背景 `#2563EB` + 文字 `#FFFFFF`（約 5.17:1）、light で背景 `#1D4ED8` + 文字 `#FFFFFF`（約 6.70:1）。両キーを `ThemeManager` の dark/light 辞書にも必ず追加する。

### 4. 状態表示用の「面の色」と「文字の色」を分離する

対象: `UpsMonitor.App/App.xaml`、`UpsMonitor.App/ThemeManager.cs`、`UpsMonitor.App/MainWindow.xaml`

次のテーマ別 semantic brush を追加する。

| リソースキー | dark 推奨値（`#1F2937` 面との比率） | light 推奨値（白面との比率） | 主用途 |
|---|---|---|---|
| `SuccessTextBrush` | `#34D399`（約 7.64:1） | `#047857`（約 5.48:1） | 電力余裕、費用、ランタイム結果 |
| `WarningTextBrush` | `#FBBF24`（約 8.79:1） | `#92400E`（約 7.09:1） | Analytics ピークなど |
| `DangerTextBrush` | `#F87171`（約 5.31:1） | `#B91C1C`（約 6.47:1） | `LastError` |
| `InfoTextBrush` | `#7DD3FC`（約 8.80:1） | `#0369A1`（約 5.93:1） | Analytics 最低値など |
| `AccentContentBrush` | `#111827` | `#111827` | `StatusAccent` 面の上に載るアイコン |

`MainWindow.xaml` の固定 `Foreground` は対応する semantic brush の `DynamicResource` に置き換える。`AvrBoostAccent`、`BatteryHealthAccent`、`BatteryReplacementAccent` は非テキストのドット・境界線には引き続き使用できるが、文字には直接使わず `TextBrush` を使用する。これなら ViewModel の状態色ロジックを変えずに、dark/light 双方で可読性を保証できる。

ダッシュボードの白い UPS アイコンは `AccentContentBrush` に変更する。現在の `StatusAccent` 候補すべてに対し `#111827` は 3:1 以上であり、非テキストの状態アイコンとして安全側になる。

### 5. 変更不要と判定した箇所

- ComboBox 本体、ドロップダウン、ホバー、選択項目は `PopupBrush` / `PopupHoverBrush` / `PopupSelectedBrush` と `TextBrush` を同じテンプレートで明示しており、dark/light の両辞書に対応キーがある。
- TextBox の通常状態は background、foreground、caret、border がすべてテーマブラシであり、#8 の選択範囲以外は問題ない。
- Logs の GridView ヘッダーと行は既存の `EventHeaderStyle` / `EventItemStyle` によりテーマ対応済みである。
- 現在の ToolTip は文字列を直接指定した 3 箇所だけで、OS 既定の背景・文字の対を使うため、同色化の確証はない。将来 `TextBlock` を ToolTip 内容に入れる場合に備え、`PopupBrush` / `TextBrush` / `BorderBrush` を使う暗黙的 `ToolTip` スタイルを追加するのは予防策として有効だが、今回の必須修正には含めない。
- `MiniMonitorWindow.xaml` の表示文字は `TextBrush` / `MutedTextBrush`、面は `NavigationBrush` / `SecondaryPanelBrush` を使うため変更不要である。
- `StaticResource` は主にスタイルオブジェクトの参照に使われ、色ブラシの参照には `DynamicResource` が使われている。テーマ変更を妨げる `StaticResource` の色参照は見つからなかった。

## 影響範囲

- `TelemetryGridStyle` の修正は現行 3 個すべての DataGrid に適用される。
- HID グリッドは見た目を維持したまま、ローカル重複定義が削除される。
- `EventHeaderStyle` / `EventItemStyle` の修正は履歴の電源トラブル一覧と Logs 一覧に適用される。
- `App.xaml` と `ThemeManager.cs` の新しい semantic brush は dark/light/system の切り替えに追従する。
- `MainViewModel.cs` の状態色はドット、境界、進捗などの非テキスト用途として残せるため、この設計では ViewModel の変更は不要である。

## 実装時の検証手順

1. 実装後に `UpsMonitor.App` をビルドし、XAML のリソースキー、`TargetType`、スタイル参照に誤りがないことを確認する。
2. 設定のテーマを **Dark** にして履歴タブを開き、次の 3 箇所の列名がすべて読めるスクリーンショットを撮る。
   - 消費電力量 & 推定電気代: 日付、電力量、推定電気代、最大電力、平均電力、停電回数
   - 直近の電源トラブルイベント: 日時、イベント、詳細
   - 標準負荷別ランタイム: 想定負荷、定格負荷率、推定稼働時間、推定放電電流
3. 同じ 3 箇所でヘッダーへマウスを置き、クリック可能なヘッダーは押下し、hover/pressed 中も文字が消えないことを確認する。
4. UPS タブの「すべての HID データ」を横スクロールし、全列のヘッダー、通常行、交互行、選択行、ホバー行を確認する。ローカルスタイル削除前後で回帰がないことを見る。
5. Logs タブと履歴の電源トラブル一覧で、通常・ホバー・選択行とヘッダーを確認する。
6. 履歴の「日」「月」RadioButton と、設定画面の各 TextBox の選択文字を確認する。
7. ダッシュボード、履歴、Analytics で成功・警告・危険・情報の semantic text を確認する。可能なら Online / OnBattery / LowBattery / Critical / 未接続を再現し、状態アイコンと状態テキストも見る。
8. テーマを **Light** に切り替え、手順 2〜7 を繰り返す。特に白いカード面の緑、黄、水色、赤の文字を確認する。
9. テーマを **System** にし、Windows のアプリテーマが dark/light の各場合に同じ確認を行う。XAML の主要ブラシは `DynamicResource` なので、アプリ内部は再起動なしで更新されることも確認する。
10. Dark と Light で同じ画面サイズ・同じデータ状態のスクリーンショットを保存し、修正前画像と比較する。

## 品質担保と自動テストの限界

既存の `UpsMonitor.Core.Tests` は Core のロジックを対象としており、WPF の XAML リソース解決、Windows 既定 ControlTemplate、hover/pressed/selected の VisualState、実際のピクセル色を検証しない。このため、今回の種類の配色不具合を既存自動テストへ追加しても直接の回帰防止にはならない。

実装フェーズでは、少なくとも次の 2 点で品質を担保する。

- `dotnet build` による XAML コンパイルとリソース参照の確認
- Dark / Light / System の各テーマにおける実アプリのスクリーンショット目視確認

将来、自動化する場合は Core テストへ混ぜず、Windows 上で WPF を起動して対象コントロールの resolved `Background` / `Foreground` を調べる UI 専用テスト、または基準画像とのスクリーンショット比較を別プロジェクトとして検討する。
