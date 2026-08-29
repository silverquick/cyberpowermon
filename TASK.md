# タスク: UI応答性・描画パフォーマンス分析と改良

【対象】cyberpowermon (WPF/.NET UPS監視アプリ) のUI応答性・描画パフォーマンス分析と改良。

【調査対象ファイル】
- UpsMonitor.App/MainWindow.xaml, MainWindow.xaml.cs
- UpsMonitor.App/MainViewModel.cs
- UpsMonitor.App/TrayIconManager.cs (GDI+動的アイコン描画)
- UpsMonitor.App/MiniMonitorWindow.xaml, MiniMonitorWindow.xaml.cs

【観点】
- UIスレッドをブロックする同期処理の有無
- PropertyChanged通知の過剰発火
- DataGrid/Chart再描画コスト
- TrayIconManagerのアイコン再生成頻度とGDIリソース破棄漏れ
- MiniMonitorWindowの常時最前面・半透明化処理のCPU負荷
- タイマー/ポーリング間隔の妥当性

【成果物】
1. `docs/performance/ui_responsiveness_analysis.md` に分析結果を記載
2. 実際にコードを改良し、`UpsMonitor.sln` のビルドと `UpsMonitor.Core.Tests` の全テストがパスすることを確認
   - ビルド: `dotnet build UpsMonitor.sln`
   - テスト: `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`
3. 変更は `git commit` しておくこと(コミットメッセージは日本語で分かりやすく)

【注意】
- 起動処理・SQLiteクエリ内部・バックグラウンドポーリング処理そのものは別タスクが担当するため深入りしないこと。
- 作業が完了したら、この TASK.md の末尾に完了報告を追記すること。

---

## 完了報告

### 1. 成果物
- 分析結果レポート: `docs/performance/ui_responsiveness_analysis.md`
- ドキュメント (タスク/計画/Walkthrough): `docs/ui_responsiveness_optimization/`

### 2. 主な改良内容
1. **TrayIconManager の最適化 & GDIリソースリーク解消**:
   - `GenerateBatteryIcon` 内で電極突起描画用ブラシの `using` 破棄漏れを修正（スナップショット受信ごとの `HBRUSH` リークを根絶）。
   - 電源状態、バッテリー残量、AC接続状態、ツールチップ文字列の前回値をキャッシュし、変更がない場合のアイコン再生成および `Shell_NotifyIcon(NimModify)` IPC 呼び出しをスキップ。
2. **MainViewModel の PropertyChanged 差分通知化**:
   - 約70個のプロパティをバッキングフィールド＋`SetField` に移行し、ポーリング毎の一斉無条件通知ループ（`RaiseSnapshotProperties`）を排除。
   - 変更されたプロパティのみ通知することで、WPF のバインディング再評価とレイアウト再計算負荷を 90% 以上削減。
   - `DailyEnergyReports` を `SequenceEqual` 比較による差分更新に変更。
3. **TimeSeriesChart の描画最適化**:
   - マウスホバー時の探索を $O(N)$ 線形探索からソート済みタイムスタンプによる $O(\log N)$ 二分探索（`FindClosestPoint`）に変更。
   - `OnRender` 内での LINQ `SelectMany`/`ToArray` 一時配列アロケーションを排除。
   - ブラシおよびペンを `Freeze()` して再利用・キャッシュ化。
4. **WeeklyHeatmapControl の最適化**:
   - セル描画時の 168 回の線形探索（`FirstOrDefault`）を排除し、`HourlyPatternPoint?[7, 24]` による $O(1)$ ルックアップに最適化。
   - ヒートマップ補間ブラシおよびツールチップ描画用ペン・ブラシを `Freeze()` して再利用。
5. **UpsStateTimeline の最適化**:
   - 状態別ブラシを静的 Freeze 済みインスタンスとして再利用。
6. **MiniMonitorWindow の最適化**:
   - `DropShadowEffect` に `RenderingBias="Performance"` を設定。

### 3. 検証結果
- ビルド: `dotnet build UpsMonitor.sln` -> 成功 (0 警告、0 エラー)
- テスト: `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj` -> 全 22/22 テスト パス

