# 改修内容の確認 (Walkthrough): アーキテクチャおよび UI/UX の改善

## 変更の概要
PowerGuard (UpsMonitor) のアーキテクチャおよび UI/UX の大幅な改善を行いました。

---

## 主な改修項目と実装内容

### 1. 多重起動防止機構 (Single Instance Mutex & 既存ウィンドウ前面化)
- **[`App.xaml.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/App.xaml.cs)**:
  - `Local\UpsMonitor_PowerGuard_SingleInstance` Mutex を取得し、多重起動を防止。
  - 既にアプリが起動している場合は、Windows メッセージ (`RegisterWindowMessage` / `PostMessage`) をブロードキャストして既存プロセスのウィンドウを復元・最前面化し、後続プロセスは即時終了します。
- **[`MainWindow.xaml.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml.cs)**:
  - `WindowMessageHook` で二重起動通知メッセージを受信し、`Show()`, `WindowState = Normal`, `Activate()`, `SetForegroundWindow(hwnd)` を実行します。

### 2. 残り時間警告しきい値の動的変更
- **[`UpsEvents.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core/UpsEvents.cs)** / **[`Monitoring.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core/Monitoring.cs)**:
  - `UpsEventDetector.SetRuntimeLowThreshold` および `UpsMonitorEngine.SetRuntimeLowThreshold` を実装。
- **[`MainViewModel.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs)** / **[`MainWindow.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml)**:
  - 設定画面の「残り時間低下のしきい値 (`RuntimeLowSeconds`)」を読み書き可能に変更。
  - 設定保存時にエンジンへ動的反映されるため、**アプリの再起動が不要**になりました。

### 3. ログ画面 (Logs タブ) の重要度フィルタ & キーワード検索
- **[`MainWindow.xaml`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainWindow.xaml)**:
  - Logs タブ上部に重要度選択ドロップダウン（すべて / 情報 / 警告 / 重大）とキーワード検索用 TextBox、件数表示（「表示中: X / Y 件」）を追加。
- **[`MainViewModel.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/MainViewModel.cs)**:
  - `ICollectionView FilteredEvents` を導入し、イベントメッセージ・種別・状態遷移・日時のインクリメンタルフィルタリングを実装。
  - 日本語 / 英語リソース (`Strings.ja-JP.xaml`, `Strings.en-US.xaml`) に対応するテキストを追加。

### 4. 時系列グラフ ([`TimeSeriesChart.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.App/TimeSeriesChart.cs)) のツールチップ直接描画
- マウスホバー時にカーソル線とともに、各系列の最新値とタイムスタンプをポップアップ吹き出し（半透明ダーク/ライト背景、角丸、系列カラーマーカー）としてキャンバス内に直接描画。
- 画面端でのクリッピング（見切れ）を自動補正。

### 5. データエクスポート機能の拡張
- **[`TelemetryExporter.cs`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Infrastructure/TelemetryExporter.cs)**:
  - 全期間エクスポート用オーバーロード (`ExportAllTelemetryCsvAsync`, `ExportAllTelemetryJsonAsync`, `ExportAllEventsCsvAsync`) を追加。

---

## 検証結果

### 1. 単体テスト
[`UpsMonitor.Core.Tests`](file:///C:/Users/geranium/cyberpowermon/UpsMonitor.Core.Tests) に動的しきい値更新のテストケースを追加し、全 18 件のテストが正常にパスしました。

```text
PASS Power state priority
PASS Power loss and restore events
PASS Alarm edge events
PASS Disconnect and reconnect events
PASS Invalid charge is rejected, not clamped
PASS Percentage capacities are not physical SOH
PASS Physical capacity ratio calculates SOH
PASS Runtime baseline calculates comparable-load SOH
PASS Current baseline reports relative trend only
PASS Relative runtime decline requests a battery check
PASS Known BHI anchors the runtime estimate
PASS Missing baseline leaves health unknown
PASS Hard battery failures override score
PASS Self-test failure requests a battery check
PASS SQLite history stores samples, rollups, events, and health
PASS Event severity classification
PASS Telemetry and event export to CSV/JSON
PASS Dynamic runtime-low threshold update
18/18 tests passed.
```

### 2. ソリューション全体のビルド
`dotnet build UpsMonitor.sln` が警告 0、エラー 0 で正常に完了することを確認しました。
