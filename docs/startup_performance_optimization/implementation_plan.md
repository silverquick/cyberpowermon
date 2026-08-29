# 実装計画: 起動・初期化パフォーマンスの最適化

## 1. 概要
cyberpowermon の起動からメインウィンドウ表示までの所要時間を最小化するため、SQLiteの初期化遅延実行・スキーマDDL短絡、設定ファイル初回保存の非ブロッキング化、および起動シーケンスの再構成を実施します。

---

## 2. 変更対象ファイル

1. `UpsMonitor.Infrastructure/SqliteTelemetryStore.cs`
   - `_initializationTask` または非同期初期化保証メカニズムの実装。
   - `ExecuteSchemaAsync` において `PRAGMA user_version` を照会し、すでにスキーマが適用されている場合は重い `CREATE TABLE/INDEX IF NOT EXISTS` DDLの実行をスキップ。
   - `ProcessWriteQueueAsync` および各種クエリメソッド（`QueryHistoryAsync` 等）実行時に初期化完了を安全に待機。

2. `UpsMonitor.Infrastructure/JsonConfigurationStore.cs`
   - `LoadAsync` で設定ファイルが存在しない場合、ディスクへのデフォルト保存（`SaveAsync`）を `Task.Run` でバックグラウンド実行し、メモリ上のデフォルトインスタンスを即時返却。

3. `UpsMonitor.App/App.xaml.cs`
   - `OnStartup` での `await _historyStore.InitializeAsync()` の直列ブロッキング待機を解消。
   - `MainViewModel` と `MainWindow` の生成および `window.Show()` を先行させ、バックグラウンドでのストア初期化・エンジン起動と連携。
   - 例外発生時の安全なエラー通知フローを維持。

---

## 3. 実装詳細

### 3.1 `SqliteTelemetryStore` のスキーマ短絡と非同期初期化
- `EnsureInitializedAsync(CancellationToken cancellationToken = default)` メソッドを提供。
- `InitializeAsync` 内で：
  ```csharp
  // PRAGMA user_version チェック
  await using var versionCmd = connection.CreateCommand();
  versionCmd.CommandText = "PRAGMA user_version;";
  var version = Convert.ToInt64(await versionCmd.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture);
  if (version < 1)
  {
      // スキーマDDLを実行し、PRAGMA user_version = 1 を設定
      ...
  }
  ```
- キュー処理（`ProcessWriteQueueAsync`）の冒頭で `EnsureInitializedAsync()` を呼ぶことで、書き込み要求（`WriteAsync`）はブロックせずにキューに入れられ、接続・スキーマ完了後に順次フラッシュされる。
- クエリメソッド（`SqliteTelemetryQueries.cs` 内の `QueryHistoryAsync`, `QueryWeeklyPatternAsync` など）の冒頭でも `EnsureInitializedAsync()` を呼ぶ。

### 3.2 `JsonConfigurationStore` の非ブロッキング化
```csharp
if (!File.Exists(_paths.ConfigurationFile))
{
    var defaults = new AppConfiguration();
    _ = Task.Run(async () =>
    {
        try { await SaveAsync(defaults).ConfigureAwait(false); } catch { }
    });
    return defaults;
}
```

### 3.3 `App.xaml.cs` の最適化
- `_historyStore.InitializeAsync()` を非同期で開始し、`window.Show()` を直ちに実行。
- UIの初期描画とレスポンスを最優先にする。

---

## 4. テスト・検証方針
1. `dotnet build UpsMonitor.sln` でコンパイルエラー・警告がないことを確認。
2. `dotnet run --project .\UpsMonitor.Core.Tests\UpsMonitor.Core.Tests.csproj` で全22テストがパスすることを確認。
3. 既存の機能（DB書き込み、集計、クエリ、イベント検出、設定保存など）が一切損なわれないことを確認。
