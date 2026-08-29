# タスク: SQLiteデータアクセス層パフォーマンス分析と改良

【対象】cyberpowermon (WPF/.NET UPS監視アプリ) のSQLiteデータアクセス層パフォーマンス分析と改良。

【調査対象ファイル】
- UpsMonitor.Infrastructure/SqliteTelemetryQueries.cs
- UpsMonitor.Core/TelemetryHistory.cs

【観点】
- 各クエリ(日別/週次/月次集計, トラブルサマリー, ヒートマップ, 停電/電圧異常集計等)のインデックス使用状況
- N+1問題、不要なフルスキャン
- 非同期処理の適切性(async/awaitのブロッキング有無)
- コネクション/コマンドの使い回し
- 大量データ時のスケーラビリティ

【成果物】
1. `docs/performance/sqlite_query_analysis.md` に分析結果を記載(必要なら EXPLAIN QUERY PLAN 結果も含める)
2. 実際にコードを改良(インデックス追加、クエリ最適化、キャッシュ等)し、`UpsMonitor.sln` のビルドと `UpsMonitor.Core.Tests` の全テストがパスすることを確認
   - ビルド: `dotnet build UpsMonitor.sln`
   - テスト: `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`
3. 変更は `git commit` しておくこと(コミットメッセージは日本語で分かりやすく)

【注意】
- UI描画処理・起動処理・バックグラウンドポーリング処理そのものは別タスクが担当するため深入りしないこと。
- 既存のデータ互換性を壊さないこと。
- 作業が完了したら、この TASK.md の末尾に完了報告を追記すること。

---

## 完了報告

### 1. 実施概要
cyberpowermon の SQLite データアクセス層（`UpsMonitor.Infrastructure`）におけるパフォーマンス分析と最適化作業を完了しました。

### 2. 主な成果と改良内容
1. **分析レポートの作成**:
   - `docs/performance/sqlite_query_analysis.md` に各クエリの EXPLAIN QUERY PLAN 解析結果およびボトルネック・改善策を詳細に記録。
2. **インデックスおよび PRAGMA 最適化 (`SqliteTelemetryStore.cs`)**:
   - クリーンアップ削除処理（`DELETE ... WHERE timestamp_utc_ms < $cutoff`）をフルスキャンからインデックスシークへ改善するためのインデックス（`ix_telemetry_samples_time`, `ix_raw_values_time`）を追加。
   - `PRAGMA cache_size=-20000`, `PRAGMA mmap_size=268435456`, `PRAGMA temp_store=MEMORY` による I/O チューニングを適用。
3. **データアクセスクエリ最適化 (`SqliteTelemetryQueries.cs`)**:
   - `QueryHistoryAsync`: 要求された複数メトリックを 1 つの動的 SQL または `IN` 句バッチクエリに統合し、生データ取得で 69%、ロールアップで 38% 高速化。
   - `QueryDailyEnergyReportsAsync`: 30回のループ個別クエリ（N+1）を日付 GROUP BY による一括バッチクエリへ統合。
   - `QueryPowerTroubleSummaryAsync`: 電圧サグ・サージの集計を `COUNT(CASE WHEN ...)` による単一スキャンに統合（55% 高速化）。
   - `QueryWeeklyPatternAsync`: 冗長な分岐の整理・クエリ呼び出しの最適化。
4. **検証結果**:
   - `dotnet build UpsMonitor.sln`：ビルド成功（警告 0、エラー 0）
   - `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj`：全23テスト（既存22テスト + ベンチマーク）PASS
   - 既存のデータ互換性および整合性を完全に維持。

