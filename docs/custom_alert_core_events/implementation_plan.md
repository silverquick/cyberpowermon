# 実装計画: カスタムアラートのイベント化基盤（Phase B1 / Core・Infrastructure）

## 1. 概要
本実装計画は、`docs/improvement_backlog.md` の「施策 #1 カスタムアラートをイベント・通知・外部連携まで一貫化」のうち、Core / Infrastructure 領域（Phase B1）のイベント化基盤を実装するものである。

現状、高負荷や電圧異常などのカスタムアラートは UI 側の `MainViewModel.CheckCustomAlerts` で音を鳴らすだけにとどまっており、`UpsEventType.HighLoadWarning` / `VoltageAbnormal` イベントが Core から発行されず、SQLite DB、ログ、通知、Webhook、外部コマンドに届かない状態となっている。
本フェーズでは Core / Infrastructure 層において、エッジトリガー＋復帰ヒステリシスを備えたイベント検知基盤を整備する。

## 2. 変更対象ファイル
- `UpsMonitor.Core/UpsEvents.cs`
- `UpsMonitor.Core/Monitoring.cs`
- `UpsMonitor.Infrastructure/AppConfiguration.cs`
- `UpsMonitor.Core.Tests/Program.cs`

※ `UpsMonitor.App/MainViewModel.cs`, `MainWindow.xaml(.cs)`, リソース XAML, `TelemetryHistory.cs`, `SqliteTelemetryQueries.cs`, `SqliteTelemetryStore.cs` は Phase C / B2 の担当であるため変更しない。

## 3. 詳細設計

### 3.1 `UpsMonitor.Core/UpsEvents.cs`
- immutable な `UpsAlertThresholds` レコードの追加:
  - `HighLoadPercent` (既定値: 80.0)
  - `LowVoltage` (既定値: 92.0)
  - `HighVoltage` (既定値: 108.0)
  - `LoadHysteresisPercent` (既定値: 5.0)
  - `VoltageHysteresisVolts` (既定値: 2.0)
- `UpsEventDetector`:
  - 内部状態フィールド: `_alertThresholds`, `_highLoadActive`, `_voltageAbnormalActive`
  - コンストラクタ引数に `UpsAlertThresholds? alertThresholds = null` を受け取れるように拡張
  - `SetAlertThresholds(UpsAlertThresholds thresholds)` メソッドの追加
  - `Observe(UpsSnapshot current)` での検知ロジック:
    - 切断時（`!current.IsConnected`）: アラートイベントは発火させず、アクティブフラグをリセット。
    - 初回観測時（`previous is null`）: 異常状態であれば初回に即時イベントを発行（既存の `RuntimeLow` と一貫した安全側の設計）。
    - 高負荷警告 (`HighLoadWarning`):
      - 突入判定: 非アクティブ時に `current.PercentLoad >= _alertThresholds.HighLoadPercent` で発火しアクティブ化。
      - 復帰判定: アクティブ時に `current.PercentLoad < (_alertThresholds.HighLoadPercent - _alertThresholds.LoadHysteresisPercent)` で解除。
    - 電圧異常 (`VoltageAbnormal`):
      - 突入判定: 非アクティブかつ AC 通電時（`current.AcPresent is not false`）に `current.InputVoltage <= _alertThresholds.LowVoltage` または `>= _alertThresholds.HighVoltage` で発火しアクティブ化。
      - 復帰判定: アクティブ時に `current.InputVoltage > (_alertThresholds.LowVoltage + _alertThresholds.VoltageHysteresisVolts)` かつ `< (_alertThresholds.HighVoltage - _alertThresholds.VoltageHysteresisVolts)` で解除。

### 3.2 `UpsMonitor.Core/Monitoring.cs`
- `UpsMonitorEngine` コンストラクタの拡張:
  - `UpsAlertThresholds? alertThresholds = null` 引数を追加し、`UpsEventDetector` に渡す。
- `UpsMonitorEngine.SetAlertThresholds(UpsAlertThresholds thresholds)` メソッドの追加:
  - 稼働中の動的閾値更新を検知器へ委譲。

### 3.3 `UpsMonitor.Infrastructure/AppConfiguration.cs`
- `ExternalCommandConfiguration`:
  - `CommandOnHighLoad` (`string`, 既定値 `""`)
  - `CommandOnVoltageAbnormal` (`string`, 既定値 `""`)
- `AlertsConfiguration`:
  - `LoadHysteresisPercent` (`double`, 既定値 `5.0`)
  - `VoltageHysteresisVolts` (`double`, 既定値 `2.0`)
  - `ToAlertThresholds()` メソッドの追加

### 3.4 `UpsMonitor.Core.Tests/Program.cs`
手製テストランナーに以下のテストを追加:
1. `HighLoadAlertDetectorEdgeAndHysteresis`: 突入時1回通知、異常継続時の非発火、ヒステリシス帯内の非復帰、正常復帰、再発通知。
2. `VoltageAbnormalAlertDetectorEdgeAndHysteresis`: 低電圧・高電圧それぞれの突入、ヒステリシス、正常復帰。
3. `AlertDetectorDisconnectedNoAlerts`: UPS 切断状態での高負荷・電圧異常非発火。
4. `DynamicAlertThresholdsUpdate`: 実行時の閾値変更による即時判定反映。
5. `EngineAlertThresholdsPropagationAndPersistence`: `UpsMonitorEngine` を経由したイベント発火および Sink への永続化連携。
6. `ConfigurationAlertsAndCommandsRoundTrip`: 新規設定項目の後方互換性および ToAlertThresholds の検証。

## 4. 検証手順
1. `dotnet build UpsMonitor.sln` でエラー・警告なくビルドできることを確認。
2. `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj` で全テスト（既存および新規）が PASS することを確認。
