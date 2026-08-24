# 実装計画: 非対応センサー（周波数・温度等）の案内メッセージ明示化

## 概要
CyberPower CP1200PFCLCD JP などの一部の UPS では、周波数や内部温度のセンサーが USB HID Report Descriptor に含まれておらず、値が取得できません（N/A）。
現在、グラフ上に単に「データがありません」と表示され、アプリの不具合かデバイス非対応かが判別しづらいため、接続中の UPS が該当センサーに非対応であることを明確に案内するメッセージを表示するように改善します。

---

## 提案する変更内容

### 1. リソース文字列の追加
- [`UpsMonitor.App/Resources/Strings.ja-JP.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.ja-JP.xaml):
  - `SensorUnsupported`: `接続中の UPS は非対応です`
  - `SensorUnsupportedDetail`: `この UPS の USB インターフェースでは提供されていません`
- [`UpsMonitor.App/Resources/Strings.en-US.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.en-US.xaml):
  - `SensorUnsupported`: `Unsupported by connected UPS`
  - `SensorUnsupportedDetail`: `Not provided over this UPS USB interface`

### 2. ViewModel の拡張
- [`UpsMonitor.App/MainViewModel.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs):
  - `FrequencyEmptyText`, `TemperatureEmptyText` プロパティを追加。
  - スナップショット受信時・履歴更新時に、UPS のテレメトリ内に `Frequency (0x0084:0x0032)` や `Temperature (0x0084:0x0036)` が存在するかを判定。
  - ディスクリプタに存在しない場合は `SensorUnsupported` を、存在してデータがない場合は `HistoryNoData` を返す。

### 3. XAML マークアップの更新
- [`UpsMonitor.App/MainWindow.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml):
  - 周波数チャートの `EmptyText="{Binding FrequencyEmptyText}"`
  - 内部温度チャートの `EmptyText="{Binding TemperatureEmptyText}"`

---

## 検証計画
- `dotnet build UpsMonitor.sln` で 0 警告 / 0 エラーでビルドできることを確認。
- `UpsMonitor.Core.Tests` を実行して既存テストが全て PASS することを確認。
