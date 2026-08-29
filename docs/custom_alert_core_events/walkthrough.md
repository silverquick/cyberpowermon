# Walkthrough: カスタムアラートのイベント化基盤（Phase B1 / Core・Infrastructure）

## 1. 実装の概要
施策 #1「カスタムアラートをイベント・通知・外部連携まで一貫化」の Core / Infrastructure 領域（Phase B1）を実装しました。
高負荷警告（`HighLoadWarning`）および電圧異常（`VoltageAbnormal`）について、エッジトリガー＋復帰ヒステリシスによる状態管理とイベント検知基盤を導入し、`UpsMonitorEngine` および設定ストア・テスト環境を整備しました。

## 2. 変更内容の詳細

### 2.1 `UpsMonitor.Core` (`UpsEvents.cs`, `Monitoring.cs`)
- **`UpsAlertThresholds` レコードの追加**:
  - `HighLoadPercent` (既定値 80.0%)
  - `LowVoltage` (既定値 92.0V)
  - `HighVoltage` (既定値 108.0V)
  - `LoadHysteresisPercent` (既定値 5.0%)
  - `VoltageHysteresisVolts` (既定値 2.0V)
- **`UpsEventDetector` の拡張**:
  - `SetAlertThresholds` による動的閾値更新をサポート。
  - `_highLoadActive` / `_voltageAbnormalActive` フラグによるエッジトリガー判定。
  - 復帰ヒステリシス（負荷は `HighLoad - Hysteresis` 未満、電圧は `LowVoltage + Hysteresis` 超かつ `HighVoltage - Hysteresis` 未満）でアクティブ状態を解除。
  - 初回観測時（`previous is null`）でも、異常値であれば安全側として即座にアラートイベントを発行。
  - 切断時（`!current.IsConnected`）や停電時（`ac: false` による電圧0V）はアラートイベントを発火させないよう制御。
- **`UpsMonitorEngine` の拡張**:
  - コンストラクタ引数に `UpsAlertThresholds`（省略時はデフォルト）を追加。
  - `SetAlertThresholds` メソッドを追加し、監視エンジンから検知器へ閾値を動的反映可能に。

### 2.2 `UpsMonitor.Infrastructure` (`AppConfiguration.cs`)
- **`ExternalCommandConfiguration`**:
  - `CommandOnHighLoad` (`string`, 既定値 `""`)
  - `CommandOnVoltageAbnormal` (`string`, 既定値 `""`)
- **`AlertsConfiguration`**:
  - `LoadHysteresisPercent` (`double`, 既定値 `5.0`)
  - `VoltageHysteresisVolts` (`double`, 既定値 `2.0`)
  - `ToAlertThresholds()` ヘルパーメソッドを追加。

### 2.3 `UpsMonitor.Core.Tests` (`Program.cs`)
以下のテストケースを追加・更新し、全 38 件のテストがパスすることを確認しました:
- `High load alert detector edge and hysteresis`
- `Voltage abnormal alert detector edge and hysteresis`
- `Alert detector disconnected produces no alerts`
- `Dynamic alert thresholds update`
- `Engine alert thresholds propagation and persistence`
- `Alert events SQLite round trip`
- `Configuration theme, alerts, webhook, and command settings`（新設定項目の検証追加）

## 3. 検証結果
- `dotnet build UpsMonitor.sln`: 0 警告 / 0 エラー
- `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`: 全 38 件 PASS
