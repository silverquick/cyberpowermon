# 改修内容の確認 (Walkthrough): 非対応センサー案内表示の実装

## 変更の概要
CyberPower CP1200PFCLCD JP 等の UPS デバイスにおいて、ハードウェア/ファームウェア仕様により USB HID 経由で提供されないセンサー（周波数・内部温度など）について、グラフ上に「**接続中の UPS は非対応です**」と明示する UI 改善を行いました。

---

## 主な改修項目と実装内容

### 1. センサー対応可否の自動判定 & ViewModel プロパティ
- **[`MainViewModel.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs)**:
  - 接続された UPS の HID Report Descriptor 内に該当 Usage（周波数 `0x0084:0x0032`、温度 `0x0084:0x0036`）が存在するか、またはスナップショットに値が存在するかを自動判定（`HasFrequencySensor`, `HasTemperatureSensor`）。
  - ディスクリプタに非対応の場合は `SensorUnsupported`（「接続中の UPS は非対応です」）、対応機種でデータ未蓄積の場合は `HistoryNoData`（「データがありません」）を返す動的プロパティ `FrequencyEmptyText`, `TemperatureEmptyText` を追加。

### 2. 多言語リソースの追加
- **[`Strings.ja-JP.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.ja-JP.xaml)** / **[`Strings.en-US.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.en-US.xaml)**:
  - `SensorUnsupported`: `接続中の UPS は非対応です` / `Unsupported by connected UPS`
  - `SensorUnsupportedDetail`: `この UPS の USB インターフェースでは提供されていません` / `Not provided over this UPS USB interface`

### 3. XAML マークアップの更新
- **[`MainWindow.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml)**:
  - 周波数チャート: `EmptyText="{Binding FrequencyEmptyText}"`
  - 内部温度チャート: `EmptyText="{Binding TemperatureEmptyText}"`

---

## 検証結果

### 1. 単体テスト
[`UpsMonitor.Core.Tests`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core.Tests) の全 18 件のテストが正常にパスしました。
