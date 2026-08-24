# 実装計画: 高度な電力マージン・電気代試算・AVR・バッテリー診断・デバイス状態の実装

## 概要
CyberPower CP1200PFCLCD JP などの UPS 実機から取得される各種 HID パラメータを活用し、以下の5つの高度な監視・可視化機能を実装します。

1. **電力・容量マージン (Rated Capacity Margin)**:
   - 定格有効電力（780W）および定格皮相電力（1200VA）に対する「残り利用可能電力（例: 残り 384 W / 804 VA）」の算出・表示。
   - 消費電力時系列グラフ上に定格上限ライン（780W）を描画。
2. **推定電気代 & CO₂ 排出量換算 (Energy Cost & CO₂ Emission)**:
   - 積算電力量（kWh）から設定可能な電気料金単価（デフォルト 31 円/kWh）による電気代試算、および CO₂ 排出量（0.457 kg-CO₂/kWh）を計算して表示。
3. **電圧安全マージン & AVR (自動電圧調整) 昇圧状態**:
   - 低電圧切替値（92V）に対する電圧マージン（例: `切替下限まで +5.0V`）の算出。
   - 商用電圧低下時の AVR 昇圧（Boost）作動中インジケーター表示。
4. **バッテリー詳細診断 & しきい値基準線**:
   - バッテリー総電圧（26.4V）からセルあたり電圧（`2.20 V/cell`）を算出・表示。
   - バッテリー残量グラフ上に低残量警告ライン（20%）およびシャットダウン移行ライン（10%）を描画。
5. **デバイス制御・セルフテスト情報**:
   - ブザー（アラーム音）設定状態（有効 / 消音中 / 無効）のカード表示。
   - 最終セルフテスト結果（正常終了 / 警告 / 未実行 等）のステータスカード表示。

---

## 変更対象ファイル
- [`UpsMonitor.Infrastructure/AppConfiguration.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/AppConfiguration.cs): 電気料金単価設定 (`ElectricityRatePerKwh`) を追加。
- [`UpsMonitor.App/MainViewModel.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs):
  - 電力残容量、推定電気代、CO2 排出量、電圧マージン、セル電圧、AVR 状態、ブザー状態、セルフテスト状態の算出プロパティを追加。
  - 電力グラフおよびバッテリーグラフの基準線メソッドを拡張。
  - `ElectricityRatePerKwh` 設定の読み書き対応。
- [`UpsMonitor.App/Resources/Strings.ja-JP.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.ja-JP.xaml) / [`Strings.en-US.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/Resources/Strings.en-US.xaml): 多言語ラベルを追加。
- [`UpsMonitor.App/MainWindow.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml): Dashboard, History, Settings の各タブに UI を配置。

---

## 検証計画
- `UpsMonitor.Core.Tests` を実行して単体テストがすべて PASS することを確認。
- `dotnet build UpsMonitor.sln` で 0 警告 / 0 エラーでビルドできることを確認。
