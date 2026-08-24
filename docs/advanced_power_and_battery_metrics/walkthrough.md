# 改修内容の確認 (Walkthrough): 高度な電力マージン・電気代試算・AVR・バッテリー診断・デバイス状態の実装

## 変更の概要
CyberPower CP1200PFCLCD JP などの実機から取得できる HID パラメータを最大限に活用し、**5 つの高度な監視・可視化機能**（電力残容量、推定電気代・CO₂、電圧マージン・AVR 状態、セル電圧、アラーム・セルフテスト状態）を実装しました。

---

## 主な改修項目と実装内容

### 1. 電力・容量マージン (Rated Capacity Margin)
- **[`MainViewModel.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs)**:
  - 定格有効電力（780W）および定格皮相電力（1200VA）から現在の消費電力を差し引いた「残り利用可能電力（例: `残り 384 W / 804 VA`）」を算出し、Dashboard の Power カードにグリーン強調で表示。
  - 消費電力時系列グラフ上に **定格有効電力（780W）の赤い上限ライン**（`#EF4444`）を自動描画。

### 2. 推定電気代 & CO₂ 排出量換算 (Energy Cost & CO₂ Emission)
- **[`AppConfiguration.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/AppConfiguration.cs)**:
  - `ElectricityRatePerKwh` 設定項目（デフォルト: `31.0` 円/kWh）を追加。
- **[`MainWindow.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml)**:
  - History タブの「積算電力量」サマリーカード内に、選択期間中の **推定電気代（例: `約 386 円 (31.0円/kWh)`）** および **CO₂ 排出量（例: `5.69 kg-CO₂`）** を表示。
  - Settings タブの「監視設定」カードに「電気料金単価 (円/kWh)」の編集 TextBox を追加。

### 3. 電圧安全マージン & AVR (自動電圧調整) 昇圧状態
- **[`MainViewModel.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs)**:
  - 低電圧切替値（92V）に対する現在の電圧余裕（例: `下限(92V)まで +5.2 V`）をリアルタイム表示。
  - 電圧降下時に UPS の昇圧機能が作動しているかを示す **AVR 昇圧（Boost）インジケーター**（通常: `商用給電中 (通常)`、昇圧中: `昇圧中 (AVR)`）をバッジ表示。

### 4. バッテリー詳細診断 & しきい値基準線
- **[`MainViewModel.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs)**:
  - バッテリー端子電圧（26.4V）と公称電圧（24V）から **1セルあたり電圧（例: `2.20 V/cell (12セル)`）** を算出して表示。
  - バッテリー残量グラフ上に **低残量警告ライン（20% / オレンジ）** および **シャットダウン移行ライン（10% / 赤）** を描画。

### 5. デバイス制御・セルフテスト情報
- **[`MainWindow.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml)**:
  - Dashboard の Shutdown & Device カードに **アラーム音設定状態（有効 / 消音中 / 無効）** および **最終セルフテスト結果（正常終了 / 未実行 等）** を追加表示。

---

## 検証結果

### 1. 単体テスト
[`UpsMonitor.Core.Tests`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core.Tests) の全 18 件のテストが正常にパスしました。
