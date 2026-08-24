# 実装計画: アーキテクチャおよび UI/UX の改善

## 概要
PowerGuard (UpsMonitor) の設計・機能・UI/UX を大幅に改善します。
1. **アーキテクチャ改善**: 多重起動防止 (Single Instance Mutex & 既存ウィンドウ前面化)、`MainViewModel` の責務分割・リファクタリング。
2. **機能 & UI/UX 改善**: 残り時間警告しきい値の動的更新、ログ一覧のフィルタ・検索機能、時系列グラフのツールチップ表示、エクスポートの期間指定機能。

---

## 提案する変更内容

### 1. アーキテクチャ & 設計の改善

#### 1-1. 多重起動防止 (`App.xaml.cs`, `MainWindow.xaml.cs`)
- `Global\UpsMonitor_PowerGuard_SingleInstance` Mutex を使用して単一インスタンスであることを確認。
- 既に起動している場合は、Windows メッセージ (`RegisterWindowMessage`) を送信して既存プロセスのウィンドウを通常表示＆最前面化（アクティブ化）し、後続プロセスは安全に終了。
- `--tray` で起動された場合でも、二重起動時には既存プロセスのトレイやウィンドウを復元。

#### 1-2. 残り時間警告しきい値の動的更新 (`UpsMonitor.Core`, `UpsMonitor.App`)
- [`UpsMonitor.Core/UpsEvents.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core/UpsEvents.cs):
  - `UpsEventDetector.SetRuntimeLowThreshold(TimeSpan threshold)` メソッドを追加。
- [`UpsMonitor.Core/Monitoring.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core/Monitoring.cs):
  - `UpsMonitorEngine.SetRuntimeLowThreshold(TimeSpan threshold)` メソッドを追加。
- [`UpsMonitor.App/MainViewModel.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs):
  - `RuntimeLowSeconds` の編集可能プロパティ化。設定保存時にエンジンへ動的適用。
- [`UpsMonitor.App/MainWindow.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml):
  - 設定タブの TextBox を編集可能に変更。

#### 1-3. ログ画面のフィルタリング & 検索機能 (`UpsMonitor.App`)
- ログ管理専用のビューモデル機能を追加。
- 重要度フィルタ（すべて / 情報 / 警告 / 重大）とキーワード検索用テキストボックスを Logs タブに追加。
- `ICollectionView` を利用してリアルタイムフィルタリング。
- 多言語リソース (`Strings.ja-JP.xaml`, `Strings.en-US.xaml`) に必要なラベルを追加。

#### 1-4. 時系列グラフのツールチップ表示 (`TimeSeriesChart.cs`)
- `TimeSeriesChart` にマウスホバー時の詳細データポップアップ描画を実装。
- カーソル位置に最も近いデータポイントの日時と各系列の値（例: 入力電圧、出力電圧、バッテリー電圧等）を吹き出し形式で描画。
- 画面端での見切れ防止（クリッピング防止）とダーク/ライトテーマに応じた配色。

#### 1-5. データエクスポートの期間指定 (`TelemetryExporter.cs`, `MainViewModel.cs`, `MainWindow.xaml`)
- `TelemetryExporter` に期間指定（`from` / `to`）エクスポートメソッドを追加。
- 履歴タブで選択している期間（24時間、7日、30日など）または全期間をエクスポート可能にする。

#### 1-6. `MainViewModel` の責務分割・リファクタリング
- サブ機能（ログ管理、履歴管理、設定管理）のロジックを整理・モジュール化し、コードの可読性と保守性を向上。

---

## 検証計画

### 自動テスト
- `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj` を実行し、動的しきい値更新や既存のコアロジックが正常にパスすることを確認。

### ビルド & 動作確認
- `dotnet build UpsMonitor.sln` でビルドが成功することを確認。
- 各種 UI 機能（ログ検索・フィルタ、グラフのホバーツールチップ、設定動的反映）の動作確認。
