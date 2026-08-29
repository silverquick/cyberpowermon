# 実装計画: UI応答性・描画パフォーマンス最適化

## 1. 目的
cyberpowermon (PowerGuard) のUIスレッド負荷、PropertyChanged通知の過剰発火、GDIリソースリーク、およびチャート描画負荷を低減し、省電力性とスムーズなUI応答性を実現する。

## 2. 変更対象ファイルと詳細計画

### 2.1 `UpsMonitor.App/TrayIconManager.cs`
- **GDIブラシリークの修正**:
  - `GenerateBatteryIcon` 内で電極突起を描画する `new SolidBrush(Color.FromArgb(220, 230, 242))` を `using` で保護。
- **変更検知によるスキップ**:
  - 前回の `state`, `batteryPercent`, `acPresent` を記録し、値に変更がない場合はアイコン再生成および `Shell_NotifyIcon(NimModify)` をスキップ。
  - 前回の `tooltip` 文字列を記録し、同一の場合は更新をスキップ。

### 2.2 `UpsMonitor.App/MainViewModel.cs`
- **PropertyChanged 差分通知化**:
  - スナップショット更新時に無条件で約70個のプロパティをループ通知していた `RaiseSnapshotProperties` を廃止。
  - 各プロパティをバッキングフィールド＋`SetField` による差分通知に置き換え、値が変化したプロパティのみUIに通知。
- **不要な再計算・再取得の抑制**:
  - `RecalculateSimulation` やプロパティ更新の最適化。
  - `DailyEnergyReports` 更新時の差分チェック。

### 2.3 `UpsMonitor.App/TimeSeriesChart.cs`
- **ホバー探索の二分探索化 ($O(N) \to O(\log N)$)**:
  - タイムスタンプ順のデータ点を二分探索するロジックを導入。
- **リソース Freezing とアロケーション削減**:
  - `OnRender` 内での LINQ `SelectMany`/`ToArray()` を避け、直接ループで min/max を計算。
  - 生成する Brush / Pen を `Freeze()` し、可能であればキャッシュ。

### 2.4 `UpsMonitor.App/WeeklyHeatmapControl.cs`
- **グリッドマップ作成の $O(1)$ 化**:
  - 168回の `FirstOrDefault` 線形探索を解消し、Dictionary によるルックアップに最適化。
- **ヒートマップ Brush の Freezing**:
  - 生成した `SolidColorBrush` を `Freeze()` して再利用。

### 2.5 `UpsMonitor.App/UpsStateTimeline.cs`
- **状態 Brush のキャッシュと Freezing**:
  - `StateBrush` を静的 Freeze 済み Brush として定義。

### 2.6 `UpsMonitor.App/MiniMonitorWindow.xaml`
- **ドロップシャドウの最適化**:
  - `DropShadowEffect` に `RenderingBias="Performance"` を設定。

## 3. 検証計画
- `dotnet build UpsMonitor.sln` でエラーなくビルドできること。
- `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj` で全22テストがパスすること。
