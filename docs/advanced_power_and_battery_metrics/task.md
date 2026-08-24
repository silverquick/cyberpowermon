# タスクリスト: 高度な電力マージン・電気代試算・AVR・バッテリー診断・デバイス状態の実装

## 1. 設定 & バックエンド拡張
- [x] 1-1. `AppConfiguration.cs` に電気料金単価 (`ElectricityRatePerKwh` デフォルト: 31.0円) の設定項目を追加 <!-- id: 1-1 -->
- [x] 1-2. `MainViewModel.cs` に電気代試算、CO2 排出量、電力残マージン、電圧マージン、セル電圧、AVR 状態等の算出ロジックを実装 <!-- id: 1-2 -->

## 2. グラフ基準線 & 多言語リソースの追加
- [x] 2-1. `MainViewModel.cs` で電力グラフに定格電力ライン (ConfigActivePower)、バッテリーグラフにしきい値ライン (Warning/Remaining Limit) を追加 <!-- id: 2-1 -->
- [x] 2-2. 日英リソースファイル (`Strings.ja-JP.xaml`, `Strings.en-US.xaml`) に必要な文言・ラベルを定義 <!-- id: 2-2 -->

## 3. UI マークアップの更新
- [x] 3-1. `MainWindow.xaml` の Dashboard タブおよび History タブに電力残マージン、推定電気代、AVR 状態、セル電圧、ブザー/テスト状態カードを配置 <!-- id: 3-1 -->
- [x] 3-2. Settings タブに電気料金単価の編集 TextBox を追加 <!-- id: 3-2 -->

## 4. テストと動作確認
- [x] 4-1. 単体テストの実行とソリューション全体のビルド確認 <!-- id: 4-1 -->
- [x] 4-2. ウォークスルー (`walkthrough.md`) の作成 <!-- id: 4-2 -->
