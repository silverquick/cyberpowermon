# 実装計画: 包括的機能改善 (Feature Enhancements)

UPS監視アプリ「PowerGuard」の実用性・監視力・利便性を大幅に向上させるため、4つの領域にわたる機能改善を段階的に実装します。

---

## 1. 改善領域とアーキテクチャ設計

```mermaid
flowchart TD
    subgraph Core ["UpsMonitor.Core"]
        Snapshot[UpsSnapshot] --> Evaluator[State / Event Evaluator]
        Evaluator --> CustomAlerts[Custom Alert Rules]
        Snapshot --> Simulator[RuntimeEstimator / Simulator]
    end

    subgraph Infra ["UpsMonitor.Infrastructure"]
        Config[AppConfiguration (Theme/Alerts/Webhook/Commands)]
        DB[(SQLite telemetry.db)] --> Aggregator[Daily/Monthly & Outage Queries]
        Webhook[WebhookNotifier (Discord/Slack/Generic)]
        Runner[CommandRunner (External Scripts)]
    end

    subgraph App ["UpsMonitor.App"]
        Tray[TrayIconManager (Dynamic GDI+ Icons & Test Notify)]
        Theme[ThemeManager (Dark/Light/System)]
        Mini[MiniMonitorWindow (Compact Topmost PIP)]
        VM[MainViewModel]
        Views[Dashboard / History / Logs / Settings]
    end

    Core --> Infra
    Core --> App
    Infra --> App
```

### 1. タスクトレイ・通知・アラートの強化
- **動的トレイアイコン**:
  - GDI+ (`System.Drawing`) を用い、電源状態（緑/橙/赤/灰）とバッテリー残量（%の文字またはミニバッテリーゲージ）を合成した16x16 / 32x32のアイコンをリアルタイムに生成してタスクトレイに設定。
  - リソースリークを防ぐため、アイコン更新時に旧アイコンの `DestroyIcon` を徹底。
- **通知テスト**:
  - 設定画面からボタン一つでテスト通知（情報/警告/重大）を発行できる機能。
- **カスタムアラート**:
  - 負荷率超過（例: 80%超え）、入力電圧異常（例: 95V未満または105V超え）を検知した際の通知＆警告音（ビープ音再生）。

### 2. テーマ切り替え＆UI/UXのカスタマイズ
- **テーマ設定**:
  - `ThemeManager` を拡張し、`System` / `Dark` / `Light` の3モードをサポート。
  - 設定画面で選択し、即時反映および `config.json` への永続化。
- **ミニモニター（コンパクトPIPモード）**:
  - デスクトップ上に常駐できる小型（260x120px程度）の半透明・最前面フローティングウィンドウ。
  - 電源状態、バッテリー残量バー、現在電力(W)、残り時間(分)、入力電圧(V) を表示。
  - ダブルクリックでメインウィンドウ表示切替。
- **イベントログの高度な検索・フィルタ**:
  - 日付範囲（From / To）、イベント種別、重大度、フリーテキスト検索の組み合わせ。

### 3. 電力・電気代・停電の高度な統計分析レポート
- **日別・月別電力消費＆電気代集計**:
  - SQLite の `telemetry_rollups_1m` / `telemetry_samples` を集計し、過去の日別・月別の消費電力量 (kWh)、推定電気代（円）、平均/最大電力を一覧・チャート表示。
- **停電・電源トラブル履歴サマリー**:
  - 過去の停電イベント、停電継続時間、電圧低下（サグ: 95V未満）や電圧上昇（サージ: 105V超）の発生回数と履歴一覧を算出。
- **負荷別ランタイム推計シミュレーター**:
  - 現在のバッテリー定格容量、公称電圧、バッテリー健全度 (SOH)、および現在の放電特性モデルに基づき、指定電力（例: 50W, 100W, 200W, 300W, 500W, 700W）での推定駆動時間を計算。

### 4. 外部通知＆自動化連携 (Webhook / スクリプト実行)
- **Webhook 通知 (`WebhookNotifier`)**:
  - 停電、復電、バッテリー低下、過負荷発生時に非同期で Discord / Slack / 汎用 JSON Webhook へ HTTP POST。
- **外部コマンド実行 (`CommandRunner`)**:
  - 重要イベント検知時に、指定されたバッチファイル・PowerShellスクリプト・実行可能ファイルを非同期で実行（パラメータとしてイベント名や残量等を渡す）。

---

## 2. 変更・新規作成ファイル一覧

| プロジェクト | ファイル | 変更区分 | 内容 |
|:---|:---|:---|:---|
| **UpsMonitor.Core** | `RuntimeEstimator.cs` | **新規** | 負荷別稼働可能時間推計シミュレータ |
| | `UpsEvents.cs` | 更新 | カスタムアラート用イベントタイプの追加（電圧異常、高負荷等） |
| **UpsMonitor.Infrastructure** | `AppConfiguration.cs` | 更新 | テーマ設定、カスタムアラート、Webhook、外部コマンド設定の追加 |
| | `WebhookNotifier.cs` | **新規** | Discord/Slack/Webhook 非同期送信サービス |
| | `CommandRunner.cs` | **新規** | 外部スクリプト/プログラム非同期実行サービス |
| | `SqliteTelemetryQueries.cs` | 更新 | 日別/月別電力集計クエリ、停電・電圧サグ/サージサマリークエリ |
| **UpsMonitor.App** | `TrayIconManager.cs` | 更新 | GDI+ 動的アイコン描画、通知テスト |
| | `ThemeManager.cs` | 更新 | System / Dark / Light テーマ手動切り替え |
| | `MiniMonitorWindow.xaml` / `.cs` | **新規** | 最前面小型ミニモニターウィンドウ |
| | `MainViewModel.cs` | 更新 | 各新機能のプロパティ、コマンド、データバインディング |
| | `MainWindow.xaml` | 更新 | UIカード、ミニモニター起動ボタン、シミュレータ、日別/月別レポート、Settingsカード |
| | `Resources/Strings.*.xaml` | 更新 | 新機能に関する日本語・英語リソース辞書 |
| **UpsMonitor.Core.Tests** | `Program.cs` | 更新 | `RuntimeEstimator`、カスタムアラート、日別集計、Webhook等の単体テスト |

---

## 3. 検証計画
1. **単体テスト**: `UpsMonitor.Core.Tests` を実行し、全テスト（既存18件＋新規テスト）のパスを確認。
2. **ビルド検証**: ソリューション全体のビルドがエラー・警告なく正常に通ることを確認。
