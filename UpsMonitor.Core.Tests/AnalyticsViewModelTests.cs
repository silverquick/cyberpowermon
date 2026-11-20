using System.Globalization;
using Microsoft.Data.Sqlite;
using UpsMonitor.App;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

internal static class AnalyticsViewModelTests
{
    private static readonly UpsSnapshot Snapshot = new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        Device = new("synthetic", 1, 2, "Test", "UPS", "SYNTHETIC", 0x84, 4, 64, 64),
    };
    private static WeeklyPatternResult Result(double average = 120) => new()
    {
        Metric = WeeklyPatternMetric.ActivePower, From = DateTimeOffset.UnixEpoch,
        To = Snapshot.Timestamp, OverallMin = average, OverallMax = average,
        OverallAvg = average, Grid = [], TotalSamples = 1,
    };

    public static void RoutingAndSelectionRequests()
    {
        var now = new DateTimeOffset(2026, 11, 19, 12, 0, 0, TimeSpan.Zero);
        var requests = new List<(string Device, DateTimeOffset From, DateTimeOffset To, WeeklyPatternMetric Metric)>();
        using var vm = new AnalyticsViewModel((device, from, to, metric, _) =>
        { requests.Add((device, from, to, metric)); return Task.FromResult(Result()); }, e => throw e, () => now);
        vm.SetActive(true);
        Check(requests.Count == 0, "no snapshot means no query");
        vm.SetSnapshot(Snapshot);
        vm.SetActive(false); vm.RefreshLanguage();
        Check(requests.Count == 0, "inactive tab / hidden window and language do not query");
        vm.SetActive(true);
        Check(requests.Count == 1 && requests[0].From == now.AddDays(-30), "activation queries default range");
        Check(requests[0].Device == UpsDeviceIdentity.Create(Snapshot.Device!), "device identity forwarded");
        vm.SetActive(false); now = now.AddTicks(TimeSpan.FromSeconds(5).Ticks - TimeSpan.TicksPerMillisecond); vm.SetActive(true);
        Check(requests.Count == 1, "re-entry within five seconds is throttled");
        vm.SetActive(false); now = now.AddMilliseconds(1); vm.SetActive(true);
        Check(requests.Count == 2, "visible again at five seconds queries");
        vm.RefreshLanguage();
        Check(requests.Count == 3, "active language refresh bypasses throttle");
        foreach (var metric in vm.AnalyticsMetricOptions)
        {
            vm.SelectedAnalyticsMetric = metric;
            Check(requests[^1].Metric == metric.Metric, "metric forwarded");
        }
        foreach (var range in vm.AnalyticsRangeOptions)
        {
            vm.SelectedAnalyticsRange = range;
            Check(requests[^1].From == (range.Duration is { } duration ? now - duration : DateTimeOffset.UnixEpoch), "range forwarded");
        }
        var count = requests.Count;
        vm.SelectedAnalyticsRange = vm.SelectedAnalyticsRange;
        vm.SelectedAnalyticsMetric = vm.SelectedAnalyticsMetric;
        Check(requests.Count == count, "unchanged selections do not query");
        vm.RefreshAnalyticsCommand.Execute(null);
        Check(requests.Count == count + 1, "manual refresh command");
    }

    public static void LatestWins() => LatestWinsAsync().GetAwaiter().GetResult();
    private static async Task LatestWinsAsync()
    {
        var pending = new List<(TaskCompletionSource<WeeklyPatternResult> Completion, CancellationToken Token)>();
        var errors = new List<Exception>();
        using var vm = new AnalyticsViewModel((_, _, _, _, token) =>
        {
            var completion = new TaskCompletionSource<WeeklyPatternResult>();
            pending.Add((completion, token)); return completion.Task;
        }, errors.Add);
        vm.SetSnapshot(Snapshot);
        var old = vm.RefreshAnalyticsAsync();
        var current = vm.RefreshAnalyticsAsync();
        Check(pending[0].Token.IsCancellationRequested && vm.IsAnalyticsLoading, "new request cancels old");
        pending[0].Completion.SetResult(Result(10)); await old;
        Check(vm.WeeklyPattern is null && vm.IsAnalyticsLoading, "old completion cannot publish or clear loading");
        pending[1].Completion.SetResult(Result(20)); await current;
        Check(vm.WeeklyPattern?.OverallAvg == 20 && !vm.IsAnalyticsLoading, "current completion publishes");
        old = vm.RefreshAnalyticsAsync(); current = vm.RefreshAnalyticsAsync();
        pending[3].Completion.SetResult(Result(30)); await current;
        var status = vm.AnalyticsStatus;
        pending[2].Completion.SetException(new IOException("stale failure")); await old;
        Check(vm.WeeklyPattern?.OverallAvg == 30 && vm.AnalyticsStatus == status && errors.Count == 0, "stale error ignored");
        current = vm.RefreshAnalyticsAsync();
        pending[4].Completion.SetException(new IOException("synthetic error")); await current;
        Check(errors.Single().Message == vm.AnalyticsStatus && !vm.IsAnalyticsLoading, "current error forwarded and loading cleared");
        old = vm.RefreshAnalyticsAsync(); current = vm.RefreshAnalyticsAsync();
        pending[6].Completion.SetResult(Result(40)); await current;
        pending[5].Completion.SetResult(Result(5)); await old;
        Check(vm.WeeklyPattern?.OverallAvg == 40, "late successful old result cannot replace latest result");
        old = vm.RefreshAnalyticsAsync(); current = vm.RefreshAnalyticsAsync();
        pending[7].Completion.SetCanceled(pending[7].Token); await old;
        Check(vm.IsAnalyticsLoading, "cooperative cancellation cannot clear current loading");
        pending[8].Completion.SetResult(Result() with { TotalSamples = 0 }); await current;
        Check(vm.AnalyticsOverallAvgText == "-" && vm.AnalyticsPeakHourText == "-" && vm.AnalyticsLowestHourText == "-", "empty result placeholders");
        current = vm.RefreshAnalyticsAsync();
        var notifications = 0;
        vm.PropertyChanged += (_, _) => notifications++;
        vm.Dispose();
        Check(pending[9].Token.IsCancellationRequested, "dispose cancels owned request");
        pending[9].Completion.SetResult(Result(99)); await current;
        vm.SetActive(true); vm.RefreshLanguage(); await vm.RefreshAnalyticsAsync(); vm.Dispose();
        Check(notifications == 0 && pending.Count == 10, "no publication or queries after dispose");
    }

    public static void BindingsAndMainIntegration()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var xaml = File.ReadAllText(Path.Combine(root, "UpsMonitor.App/MainWindow.xaml"));
        var bindings = System.Text.RegularExpressions.Regex.Matches(xaml, @"\{Binding Analytics\.(\w+)");
        Check(bindings.Count == 13, "all thirteen Analytics bindings migrated");
        foreach (System.Text.RegularExpressions.Match binding in bindings)
            Check(typeof(AnalyticsViewModel).GetProperty(binding.Groups[1].Value) is not null, "binding exists: " + binding.Value);
        var source = File.ReadAllText(Path.Combine(root, "UpsMonitor.App/MainViewModel.cs"));
        Check(source.Contains("public AnalyticsViewModel Analytics { get; }"), "root binding property exists");
        Check(source.Contains("public const int AnalyticsIndex = 3;") && source.Contains("index is AnalyticsIndex"), "Analytics routing is index three");
        Check(source.Contains("Analytics.SetActive(value && IsAnalyticsRefreshTarget(_selectedNavigationIndex))") &&
              source.Contains("Analytics.SetActive(_isWindowVisible && IsAnalyticsRefreshTarget(value))"), "visibility and navigation route to actual child");
        Check(source.Contains("Analytics.SetSnapshot(snapshot)") && source.Contains("Analytics.RefreshLanguage(refresh: false)") && source.Contains("Analytics.RefreshIfActive()") && source.Contains("Analytics.Dispose()"), "lifecycle forwarded");
        foreach (var language in new[] { "ja-JP", "en-US" })
        {
            var resources = System.Xml.Linq.XDocument.Load(Path.Combine(root, "UpsMonitor.App/Resources", $"Strings.{language}.xaml"))
                .Root!.Elements().Select(e => e.Attribute(System.Xml.Linq.XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value).ToHashSet();
            var vmSource = File.ReadAllText(Path.Combine(root, "UpsMonitor.App/AnalyticsViewModel.cs"));
            foreach (System.Text.RegularExpressions.Match key in System.Text.RegularExpressions.Regex.Matches(vmSource, "(?:L|Format)\\(\"(\\w+)\""))
                Check(resources.Contains(key.Groups[1].Value), "localized key exists: " + key.Groups[1].Value);
            foreach (System.Text.RegularExpressions.Match key in System.Text.RegularExpressions.Regex.Matches(xaml, @"\{DynamicResource (Analytics\w*|RefreshAnalytics)\}"))
                Check(resources.Contains(key.Groups[1].Value), "XAML localized key exists: " + key.Groups[1].Value);
            for (var day = 0; day < 7; day++) Check(resources.Contains($"DayFull_{day}"), "weekday key exists");
        }
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    public static void SelectionsAndLanguage()
    {
        var language = LocalizationManager.Language;
        try
        {
            var vm = new AnalyticsViewModel((SqliteTelemetryStore?)null, e => throw e);
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
                    LocalizationManager.Language = lang; vm.RefreshLanguage();
                    Check(ReferenceEquals(metric, vm.SelectedAnalyticsMetric) && ReferenceEquals(range, vm.SelectedAnalyticsRange), "selection identity survives language");
                    Check(vm.AnalyticsHeatmapTitle.Contains(metric.DisplayName), "localized heading");
                }
            }
            vm.RefreshAnalyticsAsync().GetAwaiter().GetResult();
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
            var vm = new AnalyticsViewModel(store, e => throw e);
            await vm.RefreshAnalyticsAsync();
            Check(vm.AnalyticsStatus == LocalizationManager.Get("HistoryWaitingForUps"), "wait for device");
            vm.SetSnapshot(snapshot);
            foreach (var lang in new[] { "ja-JP", "en-US" })
            {
                LocalizationManager.Language = lang; vm.RefreshLanguage(); await vm.RefreshAnalyticsAsync();
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
