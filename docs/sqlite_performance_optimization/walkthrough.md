# 修正内容の確認 (Walkthrough): SQLiteデータアクセス層パフォーマンス分析と改良

## 1. 実施概要
cyberpowermon の SQLite データアクセス層におけるパフォーマンスボトルネック（N+1問題、重複テーブルスキャン、クリーンアップ時のフルテーブルスキャン、メトリックごとの個別クエリ発行）を解消し、インデックス追加、クエリ最適化、PRAGMAチューニングを実施しました。

---

## 2. 変更内容一覧

### (1) スキーマ・インデックス最適化 (`SqliteTelemetryStore.cs`)
- **クリーンアップ用インデックス追加**:
  - `ix_telemetry_samples_time` ON `telemetry_samples(timestamp_utc_ms)`
  - `ix_raw_values_time` ON `raw_telemetry_values(timestamp_utc_ms)`
  - これにより、`DELETE FROM telemetry_samples WHERE timestamp_utc_ms < $cutoff` などの定期クリーンアップ処理がフルテーブルスキャンからインデックスシークへ改善。
- **PRAGMA チューニングの追加**:
  - `PRAGMA cache_size = -20000;` (~20MB)
  - `PRAGMA mmap_size = 268435456;` (256MB)
  - `PRAGMA temp_store = MEMORY;`

### (2) クエリ最適化 (`SqliteTelemetryQueries.cs`)
- **`QueryHistoryAsync` (複数メトリック一括取得)**:
  - 生データ (`QueryRawMetricsAsync`): 動的に要求メトリック列の `MIN`, `AVG`, `MAX`, `COUNT` を展開し、1 回のスキャン・GROUP BY で全メトリックの集計を取得。
  - ロールアップ (`QueryRollupMetricsAsync`): `metric IN (...)` を使用し、1 回のクエリで複数メトリックを一括取得してメモリ上でディクショナリに振り分け。
  - 処理時間を最大 69% 削減（Raw 5メトリック: 205 ms → 64 ms）。
- **`QueryDailyEnergyReportsAsync` (N+1 解消)**:
  - 過去 `days` 日分のループクエリ発行を廃止し、ローカル日付文字列（`strftime('%Y-%m-%d', ...)`）による GROUP BY を用いた一括クエリに統合。
- **`QueryPowerTroubleSummaryAsync` (単一スキャン化)**:
  - サグ・サージのカウントを `COUNT(CASE WHEN ...)` で単一クエリ・単一走査に統合。処理時間を 55% 削減（38 ms → 17 ms）。
- **`QueryWeeklyPatternAsync` の整理**:
  - 冗長な分岐コードを整理。

### (3) テストおよびベンチマーク (`UpsMonitor.Core.Tests/Program.cs`)
- EXPLAIN QUERY PLAN 出力と実行時間ベンチマークテスト（`PerformanceBenchmark`）を追加し、全 23 テストがすべて PASS することを確認。

---

## 3. 検証結果

- **ソリューションビルド**: `dotnet build UpsMonitor.sln` -> 警告 0, エラー 0 で成功
- **テスト実行**: `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj` -> 23/23 tests passed
- **後方互換性**: 既存のテーブル構造、制約、モデル型、データ整合性を完全に維持
