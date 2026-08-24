# タスクリスト: 非対応センサー表示の明示化

## 1. センサー対応可否の判定ロジックと ViewModel 拡張
- [x] 1-1. `MainViewModel` に周波数・温度センサーの対応可否プロパティ (`HasFrequencySensor`, `HasTemperatureSensor`, `FrequencyEmptyText`, `TemperatureEmptyText`) を実装 <!-- id: 1-1 -->
- [x] 1-2. 日英リソースファイル (`Strings.ja-JP.xaml`, `Strings.en-US.xaml`) に `SensorUnsupported` 文字列を追加 <!-- id: 1-2 -->

## 2. UI バインディングの更新
- [x] 2-1. `MainWindow.xaml` の History タブ（周波数・温度グラフ）の `EmptyText` に対応テキストをバインド <!-- id: 2-1 -->
- [x] 2-2. 必要に応じて Dashboard や UPS タブの N/A 表示部を最適化 <!-- id: 2-2 -->

## 3. テストと動作確認
- [x] 3-1. 単体テストの実行とソリューション全体のビルド確認 <!-- id: 3-1 -->
- [x] 3-2. ウォークスルー (`walkthrough.md`) の作成 <!-- id: 3-2 -->
