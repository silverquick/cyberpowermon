# タスクリスト: SQLiteデータアクセス層パフォーマンス分析と改良

## タスク概要
cyberpowermon の SQLite データアクセス層におけるパフォーマンスのボトルネックを分析・特定し、インデックス追加、クエリ最適化（N+1解消、一括取得）、PRAGMAチューニング等を実施して大幅なパフォーマンス向上とスケーラビリティを確保する。

## タスク進行状況

- [x] 既存コードの調査・テスト実行
- [x] EXPLAIN QUERY PLAN によるクエリ実行計画の取得およびボトルネック特定
- [x] パフォーマンス分析ドキュメント作成 (`docs/performance/sqlite_query_analysis.md`)
- [x] 実装計画の作成 (`docs/sqlite_performance_optimization/implementation_plan.md`)
- [x] SQLite スキーマおよびインデックスの最適化 (`SqliteTelemetryStore.cs`)
  - [x] クリーンアップ用インデックス (`ix_telemetry_samples_time`, `ix_raw_values_time`) 追加
  - [x] PRAGMA チューニング (mmap_size, cache_size, temp_store)
- [x] データアクセスクエリの最適化 (`SqliteTelemetryQueries.cs`)
  - [x] `QueryHistoryAsync`: 複数メトリックの一括取得 (Raw & Rollup) による複数回フルスキャン・N+1解消
  - [x] `QueryDailyEnergyReportsAsync`: 日次集計の単一クエリ化・N+1解消
  - [x] `QueryPowerTroubleSummaryAsync`: 電圧サグ・サージの集計を単一スキャン化
  - [x] `QueryWeeklyPatternAsync`: コードの整理とクエリ効率化
- [x] `UpsMonitor.sln` ビルドおよび `UpsMonitor.Core.Tests` 全テストパス確認
- [x] 改善前後のベンチマーク比較検証
- [x] 修正内容の確認ドキュメント作成 (`docs/sqlite_performance_optimization/walkthrough.md`)
- [x] `TASK.md` への完了報告追記
- [x] Git コミット作成
