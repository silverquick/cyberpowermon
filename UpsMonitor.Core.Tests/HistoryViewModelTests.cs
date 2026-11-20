using Microsoft.Data.Sqlite;
using UpsMonitor.App;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

internal static class HistoryViewModelTests
{
    internal static readonly UpsDeviceInfo Device = new("synthetic", 1, 2, "Test", "UPS", "HISTORY-TEST", 0x84, 4, 64, 64);
    internal static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void ChoicesLanguageExport()
    {
        var language = LocalizationManager.Language;
        try
        {
            var vm = new HistoryViewModel(null, new(), e => throw e);
            Check(vm.SelectedHistoryRange?.Key == "1h", "default one hour");
            Check(vm.HistoryRangeOptions.Select(x => x.Key).SequenceEqual(new[] { "1h", "6h", "24h", "7d", "30d" }), "all ranges");
            foreach (var range in vm.HistoryRangeOptions)
            {
                vm.SelectedHistoryRange = range;
                foreach (var lang in new[] { "ja-JP", "en-US" })
                {
                    LocalizationManager.Language = lang; vm.RefreshLanguage();
                    Check(ReferenceEquals(range, vm.SelectedHistoryRange), "selection identity survives translation");
                    vm.HistoryExportRange = 0;
                    var (from, to) = vm.ExportRange();
                    Check(to - from == range.Duration, "export selected duration");
                    vm.HistoryExportRange = 1;
                    Check(vm.ExportRange() == (DateTimeOffset.MinValue, DateTimeOffset.MaxValue), "all time export");
                }
            }
            vm.RefreshHistoryAsync().GetAwaiter().GetResult();
            Check(vm.HistoryStatus == LocalizationManager.Get("HistoryUnavailable"), "disabled history");
        }
        finally { LocalizationManager.Language = language; }
    }

    public static void SqliteChartsSummaryReportsHealth() => WithStore(async store =>
    {
        var config = new AppConfiguration();
        config.Monitoring.ElectricityRatePerKwh = 30;
        var vm = new HistoryViewModel(HistoryQueries.FromStore(store), config, e => throw e);
        await vm.RefreshHistoryAsync();
        Check(vm.HistoryStatus == LocalizationManager.Get("HistoryWaitingForUps"), "waiting for snapshot");
        var start = DateTimeOffset.Now.AddMinutes(-40);
        var snapshot = new UpsSnapshot { Device = Device, Timestamp = start, IsConnected = true, AcPresent = true,
            InputVoltage = 100, ActivePower = 100, ApparentPower = 200, PercentLoad = 20, BatteryPercent = 90 };
        await store.WriteAsync(snapshot, default);
        await store.WriteAsync(snapshot with { Timestamp = start.AddMinutes(1), InputVoltage = 110, ActivePower = 200 }, default);
        await store.FlushAsync();
        // Synthetic health observations exceed the 720 point limit, exercising the 0.1.027 path.
        await using (var connection = new SqliteConnection($"Data Source={store.DatabasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH RECURSIVE n(i) AS (VALUES(0) UNION ALL SELECT i+1 FROM n WHERE i<799)
                INSERT INTO battery_health_observations
                (device_id,timestamp_utc_ms,health_percent,relative_performance_percent,status,method,confidence,anchor_source,vendor_category,replacement_status)
                SELECT $device,$start+i*1000,80-(i%5),90-(i%3),1,7,2,'synthetic',4,1 FROM n;
                """;
            command.Parameters.AddWithValue("$device", UpsDeviceIdentity.Create(Device));
            command.Parameters.AddWithValue("$start", start.ToUnixTimeMilliseconds());
            await command.ExecuteNonQueryAsync();
        }
        vm.SetSnapshot(snapshot);
        await vm.RefreshHistoryAsync();
        Check(vm.VoltageHistory!.Series[0].Points.Count == 2 && vm.PowerHistory!.Series.Count == 2, "charts populated");
        Check(vm.PeriodVoltageSummaryText.Contains("Min 100V") && vm.PeriodVoltageSummaryText.Contains("Avg 105V") && vm.PeriodVoltageSummaryText.Contains("Max 110V"), "voltage min avg max");
        Check(vm.PeriodPowerSummaryText.Contains("Peak 200W"), "power peak");
        var result = await store.QueryHistoryAsync(UpsDeviceIdentity.Create(Device), vm.VoltageHistory.From, vm.VoltageHistory.To, Enum.GetValues<TelemetryMetric>());
        Check(vm.PeriodCostSummaryText == UiTextFormatting.PeriodCost(result.Summary!.TotalEnergyKwh!.Value * 30, 30), "period cost");
        Check(result.BatteryHealth.Count == 0 && result.BatteryHealthSeries!.HealthPoints.Count is > 0 and <= 720, "health downsampling used");
        Check(vm.HealthHistory!.Series[0].Points.SequenceEqual(result.BatteryHealthSeries!.HealthPoints), "downsampled health points preserved");
        Check(vm.HealthHistory.Series[1].Points.SequenceEqual(result.BatteryHealthSeries.RelativePerformancePoints), "relative points preserved");
        var daily = await store.QueryEnergyReportsAsync(UpsDeviceIdentity.Create(Device), DateTimeOffset.Now.Date.AddDays(-29), DateTimeOffset.Now, EnergyReportPeriod.Day, 30);
        Check(vm.EnergyReports.SequenceEqual(daily), "daily reports");
        vm.IsEnergyReportMonth = true; await vm.RefreshHistoryAsync();
        Check(vm.EnergyReportGranularity == EnergyReportPeriod.Month && !vm.IsEnergyReportDay, "monthly choice");
        var now = DateTimeOffset.Now;
        var monthly = await store.QueryEnergyReportsAsync(UpsDeviceIdentity.Create(Device), new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset).AddMonths(-11), now, EnergyReportPeriod.Month, 30);
        Check(vm.EnergyReports.SequenceEqual(monthly), "monthly reports");
        Check(!vm.IsHistoryLoading && vm.StateHistory is not null, "refresh finishes");
    }).GetAwaiter().GetResult();

    internal static async Task WithStore(Func<SqliteTelemetryStore, Task> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "UpsMonitor.HistoryTests", Guid.NewGuid().ToString("N"));
        try
        {
            await using var store = new SqliteTelemetryStore(new AppPaths(root), new HistoryConfiguration());
            await store.InitializeAsync(); await test(store);
        }
        finally { SqliteConnection.ClearAllPools(); if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
