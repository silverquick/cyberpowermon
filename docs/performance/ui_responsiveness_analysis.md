# UI応答性・描画パフォーマンス分析レポート

## 1. 概要
本レポートは、cyberpowermon (WPF/.NET UPS監視アプリ "PowerGuard") におけるUI応答性、描画パフォーマンス、リソース効率に関する詳細な分析結果と改善方針をまとめたものである。

---

## 2. 観点別分析結果

### 2.1 UIスレッドをブロックする同期処理の有無
- **現状分析**:
  - `MainViewModel.OnSnapshotUpdated` は `_dispatcher.InvokeAsync(() => ApplySnapshot(snapshot))` によりUIスレッドへディスパッチされている。
  - `ApplySnapshot` 内部では `BatteryHealthCalculator.Calculate`、`UpsPowerStateEvaluator.Evaluate`、`CheckCustomAlerts`、`RecalculateSimulation` などの計算処理が同期実行されている。
  - `WeeklyHeatmapControl.OnRender` 内において、グリッドマップの構築時に `data.Grid.FirstOrDefault(...)` が 7×24 = 168回呼び出されており、合計 28,224 回の比較処理が描画（およびマウス移動）のたびに同期実行され、UIスレッドのフレームレート低下を招いていた。
  - `TimeSeriesChart.DrawHoverTooltip` において、マウス移動イベントのたびに `series.Points.MinBy(...)` による全点（数百〜数千点）の線形探索が同期実行されていた。

### 2.2 PropertyChanged通知の過剰発火
- **現状分析**:
  - `MainViewModel.RaiseSnapshotProperties()` において、UPSからスナップショットを受信するたび（ポーリング間隔ごと、約1〜2秒周期）、約70個のプロパティの `OnPropertyChanged` が無条件に一斉発火されていた。
  - 製造元、型番、シリアル番号、定格電力、転送電圧しきい値などの静的情報も含め、値の変更有無にかかわらず全プロパティが通知されていたため、WPFバインディングエンジンが全コントロールに対して値の再取得とレイアウト再計算をキューイングし、高負荷の原因となっていた。

### 2.3 DataGrid / Chart再描画コスト
- **現状分析**:
  - **TimeSeriesChart**:
    - `OnRender` のたびに `SelectMany` や `Min`/`Max` などの LINQ 処理により一時配列の生成とアロケーションが発生していた。
    - `ParseBrush` による `SolidColorBrush` や `Pen` の生成が描画ループ内で頻繁に行われ、`Freeze()` されずに破棄されていたため、GC負荷が高かった。
  - **WeeklyHeatmapControl**:
    - セル描画時に色補間ブラシが毎回インスタンス化され `Freeze()` されていなかった。
    - グリッドのセル探索が $O(N)$ の二重ループになっていた。
  - **UpsStateTimeline**:
    - 電源状態に応じた `StateBrush` が描画毎に `new SolidColorBrush(...)` され `Freeze()` されていなかった。
  - **DataGrid**:
    - `DailyEnergyReports` において、履歴更新時に `Clear()` してから全件 `Add()` されていたため、行ごとの `CollectionChanged` が多重発火していた。

### 2.4 TrayIconManagerのアイコン再生成頻度とGDIリソース破棄漏れ
- **現状分析**:
  - **重大なGDIリソースリーク**:
    - `GenerateBatteryIcon` 内の 192行目において、電極突起描画用の `new SolidBrush(Color.FromArgb(220, 230, 242))` が `using` ブロック外で生成され Dispose されていなかったため、スナップショット更新（1〜2秒周期）ごとに GDI ブラシハンドル（`HBRUSH`）が永続的にリークしていた。
  - **タスクトレイ過剰更新**:
    - 電源状態、バッテリー残量、AC接続状態に変更がない場合でも、毎秒 `GenerateBatteryIcon` → `Shell_NotifyIcon(NimModify)` → `DestroyIcon` を実行していた。
    - これにより Windows エクスプローラーのタスクトレイ描画スレッドへの無駄な IPC 通信と再描画要求が発生し、タスクトレイのちらつきやシェル側のCPU負荷を誘発していた。
    - ツールチップ文字列も同様に、変化がない場合でも毎秒 `Shell_NotifyIcon(NimModify)` が呼び出されていた。

### 2.5 MiniMonitorWindowの常時最前面・半透明化処理のCPU負荷
- **現状分析**:
  - `MiniMonitorWindow.xaml` において `AllowsTransparency="True"` かつ `DropShadowEffect BlurRadius="14"` が適用されていた。
  - `RenderingBias` が指定されておらずデフォルトの `Quality` モードであったため、半透明レイヤードウィンドウにおけるDWM合成処理とソフトウェアブラー処理の負荷が高くなっていた。

### 2.6 タイマー/ポーリング間隔の妥当性
- **現状分析**:
  - ポーリング間隔（デフォルト 1000〜2000ms）および履歴自動リフレッシュ間隔（10秒）自体はUPS監視ツールとして妥当であるが、ポーリングのたびに発生する無駄な全UI更新やGDI生成が問題の本質であった。

---

## 3. 改良方針と実施内容

1. **`TrayIconManager` の最適化**:
   - GDIブラシの `using` 破棄漏れを修正し、リソースリークを解消。
   - `(state, batteryPercent, acPresent)` および `tooltip` の直前値をキャッシュし、変更がない場合は `Shell_NotifyIcon` の呼び出しをスキップ。
2. **`MainViewModel` の PropertyChanged 差分通知化**:
   - 約70個のプロパティをバッキングフィールド付きの `SetField` に移行し、値が変化したプロパティのみ `PropertyChanged` を発火するように改良。
   - `RaiseSnapshotProperties` の一括全通知ループを廃止し、不要なUIツリー再評価を徹底排除。
3. **`TimeSeriesChart` の描画最適化**:
   - ホバーツールチップの探索を $O(N)$ 線形探索からソート済みタイムスタンプを利用した $O(\log N)$ 二分探索（`BinarySearch`）に改良。
   - `OnRender` 内での LINQ/一時配列アロケーションを削減。
   - ブラシおよびペンを `Freeze()` して再利用・キャッシュ。
4. **`WeeklyHeatmapControl` の描画最適化**:
   - 描画ループ前の $O(N^2)$ 探索を解消し、`Dictionary<(int, int), HourlyPatternPoint>` による $O(1)$ ルックアップに最適化。
   - 生成ブラシの `Freeze()` 化。
5. **`UpsStateTimeline` の最適化**:
   - 状態別ブラシを `static readonly` かつ `Freeze()` されたインスタンスとして事前生成。
6. **`MiniMonitorWindow` の最適化**:
   - `DropShadowEffect` に `RenderingBias="Performance"` を指定。
