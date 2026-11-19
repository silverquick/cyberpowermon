using System.Globalization;
using Microsoft.Data.Sqlite;
using UpsMonitor.App;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

internal static class AnalyticsViewModelTests
{
    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void SourceParity()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var source = File.ReadAllText(Path.Combine(root, "UpsMonitor.App/MainViewModel.cs")).Replace("\r", "");
        var adapter = File.ReadAllText(Path.Combine(root, "UpsMonitor.Core.Tests/AnalyticsTestAdapter.cs")).Replace("\r", "");
        foreach (var (start, end) in new[] {
            ("    private IReadOnlyList<AnalyticsMetricOption>", "    private string _manufacturer"),
            ("    public IReadOnlyList<AnalyticsMetricOption>", "    public bool IsWindowVisible"),
            ("    private void RefreshAnalyticsOptions()", "    private void RefreshVendorHealthCategoryOptions()") })
            Check(adapter.Contains(source[source.IndexOf(start)..source.IndexOf(end)]), "adapter must test exact production members");
    }

    public static void SelectionsAndLanguage()
    {
        var language = LocalizationManager.Language;
        try
        {
            var vm = new AnalyticsTestAdapter(null);
            Check(vm.SelectedAnalyticsMetric?.Metric == WeeklyPatternMetric.ActivePower, "default power");
            Check(vm.SelectedAnalyticsRange?.Duration == TimeSpan.FromDays(30), "default 30 days");
            Check(vm.AnalyticsMetricOptions.Select(x => x.Metric).SequenceEqual(Enum.GetValues<WeeklyPatternMetric>()), "all metrics");
            Check(vm.AnalyticsRangeOptions.Select(x => x.Duration).SequenceEqual(new TimeSpan?[] { TimeSpan.FromDays(7), TimeSpan.FromDays(30), TimeSpan.FromDays(90), null }), "ranges");
            foreach (var metric in vm.AnalyticsMetricOptions)
            foreach (var range in vm.AnalyticsRangeOptions)
            {
                vm.SelectedAnalyticsMetric = metric; vm.SelectedAnalyticsRange = range;
                foreach (var lang in new[] { "ja-JP", "en-US" })
                {
                    LocalizationManager.Language = lang; vm.Language();
                    Check(ReferenceEquals(metric, vm.SelectedAnalyticsMetric) && ReferenceEquals(range, vm.SelectedAnalyticsRange), "selection identity survives language");
                    Check(vm.AnalyticsHeatmapTitle.Contains(metric.DisplayName), "localized heading");
                }
            }
            vm.Refresh().GetAwaiter().GetResult();
            Check(vm.AnalyticsStatus == LocalizationManager.Get("HistoryUnavailable"), "history unavailable");
        }
        finally { LocalizationManager.Language = language; }
    }

    public static void SqliteDisplay() => SqliteDisplayAsync().GetAwaiter().GetResult();
    private static async Task SqliteDisplayAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "UpsMonitor.AnalyticsTests", Guid.NewGuid().ToString("N"));
        var language = LocalizationManager.Language;
        try
        {
            await using var store = new SqliteTelemetryStore(new AppPaths(root), new HistoryConfiguration());
            await store.InitializeAsync();
            var device = new UpsDeviceInfo("synthetic", 1, 2, "Test", "UPS", "SYNTHETIC", 0x84, 4, 64, 64);
            var snapshot = new UpsSnapshot { Timestamp = DateTimeOffset.Now.AddMinutes(-10), Device = device, ActivePower = 120, InputVoltage = 100, PercentLoad = 50 };
            await store.WriteAsync(snapshot, default);
            await store.WriteAsync(snapshot with { Timestamp = snapshot.Timestamp.AddSeconds(1) }, default);
            await store.WriteAsync(snapshot with { Timestamp = snapshot.Timestamp.AddMinutes(1), ActivePower = 240 }, default);
            await store.FlushAsync();
            var vm = new AnalyticsTestAdapter(store);
            await vm.Refresh();
            Check(vm.AnalyticsStatus == LocalizationManager.Get("HistoryWaitingForUps"), "wait for device");
            vm.SetSnapshot(snapshot);
            foreach (var lang in new[] { "ja-JP", "en-US" })
            {
                LocalizationManager.Language = lang; vm.Language(); await vm.Refresh();
                Check(vm.WeeklyPattern?.TotalSamples == 2, "count populated minutes, not raw samples");
                Check(vm.WeeklyPattern!.EquivalentDays == 2 / 1440.0, "24-hour equivalent days");
                Check(vm.AnalyticsSampleCountText == LocalizationManager.Format("AnalyticsSampleCountFormat", 2L, 2 / 1440.0), "localized count and days");
                Check(vm.AnalyticsOverallAvgText == $"{vm.WeeklyPattern.OverallAvg:0.#} W", "average and unit");
                Check(vm.AnalyticsPeakHourText.Contains("W)") && vm.AnalyticsLowestHourText.Contains("W)"), "peak and lowest labels");
                Check(!vm.IsAnalyticsLoading, "loading reset");
            }
        }
        finally
        {
            LocalizationManager.Language = language;
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
}
