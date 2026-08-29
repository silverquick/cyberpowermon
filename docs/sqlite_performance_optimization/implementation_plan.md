# 実装計画: SQLiteデータアクセス層パフォーマンス分析と改良

## 1. 目的
SQLite データアクセス層におけるパフォーマンスのボトルネック（N+1 クエリ、重複テーブルスキャン、クリーンアップ時のフルテーブルスキャン、複数回個別メトリック集計など）を解消し、クエリ処理速度の向上と大量データ蓄積時のスケーラビリティを確保する。

---

## 2. 変更対象コンポーネントと改修内容

### 2.1 `UpsMonitor.Infrastructure/SqliteTelemetryStore.cs`
- **インデックス最適化**:
  - `ix_telemetry_samples_time` ON `telemetry_samples(timestamp_utc_ms)` を追加（クリーンアップの高速化）。
  - `ix_raw_values_time` ON `raw_telemetry_values(timestamp_utc_ms)` を追加（クリーンアップの高速化）。
- **PRAGMA パフォーマンスチューニング**:
  - `PRAGMA mmap_size = 268435456;` (256MB)
  - `PRAGMA cache_size = -20000;` (~20MB)
  - `PRAGMA temp_store = MEMORY;`

### 2.2 `UpsMonitor.Infrastructure/SqliteTelemetryQueries.cs`
- **`QueryHistoryAsync` (複数メトリック一括取得)**:
  - 生データ (`QueryRawMetricsMultiAsync`):
    要求されたメトリック一覧から動的に `MIN({col}), AVG({col}), MAX({col})` 列を構築し、1 回のクエリ・1 回の `GROUP BY` で全メトリックの `TelemetryHistoryPoint` を生成。
  - ロールアップデータ (`QueryRollupMetricsMultiAsync`):
    `metric IN (...)` を使用し、`GROUP BY metric, (bucket_utc_ms / $bucket)` で 1 回のクエリで全メトリックを取得し、メモリ上でメトリックごとのディクショナリに振り分け。
- **`QueryDailyEnergyReportsAsync` (N+1 解消)**:
  - 過去 `days` 日分の期間全体を一括指定し、1 つのクエリ（または `active_power_watts` の集計と `ups_events` の集計）で全日数を一括取得。
  - 日数分のループクエリ発行を廃止。
- **`QueryPowerTroubleSummaryAsync` (単一スキャン化)**:
  - 電圧サグとサージのカウントを `COUNT(CASE WHEN ...)` で単一クエリ・単一テーブルスキャンに統合。
- **`QueryWeeklyPatternAsync` の整理**:
  - 冗長な分岐の整理およびクエリ呼び出しの簡素化。

---

## 3. テストおよび検証計画
1. **既存機能・互換性テスト**:
   - `UpsMonitor.Core.Tests` の全22テストが全て PASS することを確認。
2. **パフォーマンスベンチマーク**:
   - 改善前後のクエリ実行時間を測定し、比較検証。
3. **成果物**:
   - `docs/performance/sqlite_query_analysis.md`
   - `docs/sqlite_performance_optimization/task.md`
   - `docs/sqlite_performance_optimization/implementation_plan.md`
   - `docs/sqlite_performance_optimization/walkthrough.md`
   - `TASK.md` への完了報告追記
