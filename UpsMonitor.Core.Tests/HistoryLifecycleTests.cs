using System.Text.RegularExpressions;
using UpsMonitor.App;
using UpsMonitor.Core;
using static HistoryViewModelTests;

internal static class HistoryLifecycleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 12, 0, 0, TimeSpan.FromHours(9));
    private static UpsSnapshot Snapshot => new() { Device = Device, Timestamp = Now };
    private static TelemetryHistoryResult Result(DateTimeOffset from, DateTimeOffset to, int value = 1) => new()
    {
        From = from, To = to, Metrics = new Dictionary<TelemetryMetric, TelemetryMetricHistory>(),
        BatteryHealth = [], Events = [], StateChanges = [], SourceSampleCount = value,
        Summary = new() { MinInputVoltage = value, AvgInputVoltage = value, MaxInputVoltage = value, TotalEnergyKwh = value },
    };
    private static EnergyReportItem Row(int i = 1) => new(EnergyReportPeriod.Day, Now.AddDays(-1), Now, i, i * 30, i, i, 0);
    private static HistoryQueries Queries() => new()
    {
        History = (_, from, to, _, _) => Task.FromResult(Result(from, to)),
        Statistics = _ => Task.FromResult(new TelemetryDatabaseStatistics { SampleCount = 1, RawValueCount = 1, EventCount = 0 }),
        Energy = (_, _, _, _, _, _) => Task.FromResult<IReadOnlyList<EnergyReportItem>>([Row()]),
        Trouble = (_, _, _, _, _, _) => Task.FromResult(new PowerTroubleSummary { TroubleEvents = [] }),
    };

    public static void ActivationLanguageAndSnapshots()
    {
        var now = Now;
        var calls = new List<(DateTimeOffset From, DateTimeOffset To)>();
        var energyCalls = new List<(DateTimeOffset From, EnergyReportPeriod Period)>();
        var queries = Queries() with
        {
            History = (device, from, to, metrics, _) =>
            {
                Check(device == UpsDeviceIdentity.Create(Device) && metrics.Count == Enum.GetValues<TelemetryMetric>().Length, "device and metrics");
                calls.Add((from, to)); return Task.FromResult(Result(from, to));
            },
            Energy = (_, from, _, period, _, _) =>
            { energyCalls.Add((from, period)); return Task.FromResult<IReadOnlyList<EnergyReportItem>>([]); },
        };
        using var vm = new HistoryViewModel(queries, new(), e => throw e, () => now);
        vm.SetActive(true); Check(calls.Count == 0, "no snapshot");
        vm.SetActive(false); vm.SetSnapshot(Snapshot); vm.RefreshForSnapshot(); vm.RefreshLanguage();
        Check(calls.Count == 0, "hidden snapshot and hidden language do not query");
        vm.SetActive(true); Check(calls.Count == 1 && calls[0] == (now.AddHours(-1), now), "active initial query");
        vm.SetActive(false); now = now.AddTicks(TimeSpan.FromSeconds(5).Ticks - 1); vm.SetActive(true);
        Check(calls.Count == 1, "five second throttle");
        vm.SetActive(false); now = now.AddTicks(1); vm.SetActive(true); Check(calls.Count == 2, "re-entry at five seconds");
        vm.SetSnapshot(Snapshot with { Timestamp = now.AddSeconds(9) }); vm.RefreshForSnapshot();
        Check(calls.Count == 2, "snapshot interval below ten seconds");
        now = now.AddSeconds(10); vm.SetSnapshot(Snapshot with { Timestamp = now }); vm.RefreshForSnapshot();
        Check(calls.Count == 3, "snapshot refresh at ten seconds");
        vm.RefreshLanguage(); Check(calls.Count == 4, "language bypasses throttle");
        vm.IsEnergyReportMonth = true;
        Check(calls.Count == 5 && energyCalls[^1] == (new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset).AddMonths(-11), EnergyReportPeriod.Month), "monthly active switch");
        vm.IsEnergyReportDay = true;
        Check(calls.Count == 6 && energyCalls[^1] == (new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset).AddDays(-29), EnergyReportPeriod.Day), "daily active switch");
        vm.SetActive(false); vm.IsEnergyReportMonth = true; Check(calls.Count == 6, "hidden granularity change does not query");
        vm.SelectedHistoryRange = vm.HistoryRangeOptions[4];
        Check(calls.Count == 7 && calls[^1].From == now.AddDays(-30), "explicit range change bypasses active test as before");
        vm.RefreshHistoryCommand.Execute(null); Check(calls.Count == 8, "manual refresh");
    }

    public static void LatestWinsEveryAwait() => LatestWinsEveryAwaitAsync().GetAwaiter().GetResult();
    private static async Task LatestWinsEveryAwaitAsync()
    {
        foreach (var stage in new[] { "history", "statistics", "energy", "trouble" })
        {
            var pending = new TaskCompletionSource();
            var oldToken = default(CancellationToken);
            var errors = new List<Exception>();
            var ids = new Dictionary<CancellationToken, int>();
            async Task Wait(string current, CancellationToken token)
            { if (stage == current && ids[token] == 1) { oldToken = token; await pending.Task; } }
            var baseline = Queries();
            var queries = baseline with
            {
                History = async (_, from, to, _, token) => { ids[token] = ids.Count + 1; await Wait("history", token); return Result(from, to, ids[token]); },
                Statistics = async token => { await Wait("statistics", token); return await baseline.Statistics(token); },
                Energy = async (_, _, _, _, _, token) => { await Wait("energy", token); return new[] { Row(ids[token]) }; },
                Trouble = async (_, _, _, _, _, token) => { await Wait("trouble", token); return new PowerTroubleSummary { TotalOutages = ids[token], TroubleEvents = [] }; },
            };
            using var vm = new HistoryViewModel(queries, new(), errors.Add);
            vm.SetSnapshot(Snapshot);
            var old = vm.RefreshHistoryAsync();
            await vm.RefreshHistoryAsync();
            Check(oldToken.IsCancellationRequested, "new request cancels old " + stage);
            var voltage = vm.VoltageHistory;
            var reports = vm.EnergyReports;
            var trouble = vm.TroubleSummary;
            var status = vm.HistoryStatus;
            pending.SetResult(); await old;
            Check(ReferenceEquals(voltage, vm.VoltageHistory) && ReferenceEquals(reports, vm.EnergyReports) &&
                ReferenceEquals(trouble, vm.TroubleSummary) && status == vm.HistoryStatus && !vm.IsHistoryLoading && errors.Count == 0,
                "late completion must not publish at " + stage);
        }
    }

    public static void ReportNotificationsAndCosts() => ReportNotificationsAndCostsAsync().GetAwaiter().GetResult();
    private static async Task ReportNotificationsAndCostsAsync()
    {
        IReadOnlyList<EnergyReportItem> rows = [Row()];
        var config = new UpsMonitor.Infrastructure.AppConfiguration();
        config.Monitoring.ElectricityRatePerKwh = 30;
        var queries = Queries() with { Energy = (_, _, _, _, _, _) => Task.FromResult(rows) };
        using var vm = new HistoryViewModel(queries, config, e => throw e);
        vm.SetSnapshot(Snapshot);
        var changes = 0;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.EnergyReports)) changes++; };
        await vm.RefreshHistoryAsync();
        Check(changes == 1, "changed rows notify once");
        var original = vm.EnergyReports;
        rows = rows.Select(r => r with { }).ToArray();
        await vm.RefreshHistoryAsync();
        Check(changes == 1 && ReferenceEquals(original, vm.EnergyReports), "equal rows preserve identity without notification");
        rows = Enumerable.Range(0, 30).Select(i => Row(i) with { PeriodStart = Now.AddDays(-i) }).ToArray();
        await vm.RefreshHistoryAsync(); Check(changes == 2 && vm.EnergyReports.Count == 30, "thirty rows one notification");
        rows = Enumerable.Range(0, 12).Select(i => Row(i) with { Period = EnergyReportPeriod.Month, PeriodStart = Now.AddMonths(-i) }).ToArray();
        vm.IsEnergyReportMonth = true; await vm.RefreshHistoryAsync();
        Check(changes == 3 && vm.EnergyReports.Count == 12, "monthly rows one notification");
        config.Monitoring.ElectricityRatePerKwh = 40; vm.RefreshPeriodCost();
        Check(vm.PeriodCostSummaryText == UiTextFormatting.PeriodCost(40, 40), "edited rate updates displayed cost without saving");
        var language = LocalizationManager.Language;
        try
        {
            LocalizationManager.Language = "ja-JP"; vm.RefreshLanguage(refresh: false); await vm.RefreshHistoryAsync();
            Check(changes == 3 && vm.IsEnergyReportMonth, "language does not replace equal report rows or reset granularity");
        }
        finally { LocalizationManager.Language = language; }
    }

    public static void ChartProjectionsAndHealthFallback() => ChartProjectionsAndHealthFallbackAsync().GetAwaiter().GetResult();
    private static async Task ChartProjectionsAndHealthFallbackAsync()
    {
        var from = Now.AddHours(-4);
        var times = new[] { from, from.AddMinutes(30), Now };
        static TelemetryHistoryPoint Point(DateTimeOffset time, double value) => new(time, value, value, value, value);
        var health = new BatteryHealthObservation
        {
            DeviceId = UpsDeviceIdentity.Create(Device), Timestamp = from, HealthPercent = 70, RelativePerformancePercent = 80,
            Status = BatteryHealthStatus.Unknown, Method = BatteryHealthMethod.None,
            Confidence = BatteryHealthConfidence.Unknown, VendorCategory = VendorBatteryHealthCategory.Unknown,
            ReplacementStatus = BatteryReplacementStatus.Unknown,
        };
        var result = Result(from, Now) with
        {
            Metrics = new Dictionary<TelemetryMetric, TelemetryMetricHistory>
            {
                [TelemetryMetric.ActivePowerWatts] = new(TelemetryMetric.ActivePowerWatts, times.Select((time, i) => Point(time, i == 1 ? 300 : 100)).ToArray()),
                [TelemetryMetric.ApparentPowerVoltAmperes] = new(TelemetryMetric.ApparentPowerVoltAmperes, times.Select((time, i) => Point(time, i == 0 ? 200 : i == 1 ? 150 : 0)).ToArray()),
            },
            BatteryHealth = [health],
        };
        var queries = Queries() with { History = (_, _, _, _, _) => Task.FromResult(result) };
        using var vm = new HistoryViewModel(queries, new(), e => throw e);
        vm.SetSnapshot(Snapshot with { LowVoltageTransfer = 90, HighVoltageTransfer = 110, ConfigActivePower = 400,
            WarningCapacityLimit = 20, RemainingCapacityLimit = 10 });
        await vm.RefreshHistoryAsync();
        Check(vm.HealthHistory!.Series[0].Points.Single().Average == 70 && vm.HealthHistory.Series[1].Points.Single().Average == 80, "raw health fallback");
        Check(vm.PowerFactorHistory!.Series.Single().Points.Select(x => x.Average).SequenceEqual(new double[] { 50, 100 }), "power factor clamp and invalid VA omission");
        Check(Math.Abs(vm.EnergyHistory!.Series.Single().Points[^1].Average - 0.1) < 1e-9, "energy trapezoid and long gap exclusion");
        Check(vm.VoltageHistory!.ReferenceLines.Select(x => x.Value).SequenceEqual(new double[] { 90, 110 }) &&
            vm.PowerHistory!.ReferenceLines.Single().Value == 400 && vm.BatteryChargeHistory!.ReferenceLines.Count == 2, "snapshot reference lines");
        Check(vm.FrequencyEmptyText == LocalizationManager.Get("SensorUnsupported"), "missing sensor hint");
        vm.SetSnapshot(Snapshot with { Frequency = 50 });
        Check(vm.FrequencyEmptyText == LocalizationManager.Get("HistoryNoData"), "supported sensor hint");
    }

    public static void FailureCancellationDisposal() => FailureCancellationDisposalAsync().GetAwaiter().GetResult();
    private static async Task FailureCancellationDisposalAsync()
    {
        var requests = new List<(TaskCompletionSource<TelemetryHistoryResult> Completion, CancellationToken Token)>();
        var errors = new List<Exception>();
        var queries = Queries() with { History = (_, _, _, _, token) =>
        { var completion = new TaskCompletionSource<TelemetryHistoryResult>(); requests.Add((completion, token)); return completion.Task; } };
        using var vm = new HistoryViewModel(queries, new(), errors.Add);
        vm.SetSnapshot(Snapshot);
        var old = vm.RefreshHistoryAsync(); var current = vm.RefreshHistoryAsync();
        requests[0].Completion.SetException(new IOException("stale")); await old;
        Check(errors.Count == 0 && vm.IsHistoryLoading, "stale error ignored while current loads");
        requests[1].Completion.SetException(new IOException("synthetic failure")); await current;
        Check(errors.Count == 1 && errors[0].Message == vm.HistoryStatus && !vm.IsHistoryLoading, "current failure reported");
        old = vm.RefreshHistoryAsync(); current = vm.RefreshHistoryAsync();
        requests[2].Completion.SetCanceled(requests[2].Token); await old;
        Check(vm.IsHistoryLoading, "cooperative old cancellation leaves current loading");
        var notifications = 0; vm.PropertyChanged += (_, _) => notifications++;
        vm.Dispose(); Check(requests[3].Token.IsCancellationRequested, "dispose cancels current");
        requests[3].Completion.SetResult(Result(Now.AddHours(-1), Now)); await current;
        vm.SetActive(true); vm.RefreshLanguage(); vm.RefreshForSnapshot(); await vm.RefreshHistoryAsync(); vm.Dispose();
        Check(notifications == 0 && requests.Count == 4, "no late completion or query after disposal");
    }

    public static void BindingsAndMainIntegration()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../.."));
        var xaml = File.ReadAllText(Path.Combine(root, "UpsMonitor.App/MainWindow.xaml"));
        foreach (var (prefix, type) in new[] { ("History", typeof(HistoryViewModel)), ("Exports", typeof(ExportViewModel)) })
        {
            var bindings = Regex.Matches(xaml, @"\{Binding " + prefix + @"\.([\w.]+)");
            Check(bindings.Count > 0, "nested binding found");
            foreach (Match binding in bindings)
            {
                var current = type;
                foreach (var segment in binding.Groups[1].Value.Split('.'))
                { var property = current.GetProperty(segment); Check(property is not null, "binding exists " + binding.Value); current = property!.PropertyType; }
            }
            foreach (var property in type.GetProperties())
                Check(!Regex.IsMatch(xaml, @"\{Binding " + property.Name + @"(?=[,}.])"), "no stale root binding " + property.Name);
        }
        var vmSources = File.ReadAllText(Path.Combine(root, "UpsMonitor.App/HistoryViewModel.cs")) +
            File.ReadAllText(Path.Combine(root, "UpsMonitor.App/ExportViewModel.cs"));
        foreach (var language in new[] { "ja-JP", "en-US" })
        {
            var resources = System.Xml.Linq.XDocument.Load(Path.Combine(root, "UpsMonitor.App/Resources", $"Strings.{language}.xaml"))
                .Root!.Elements().Select(e => e.Attribute(System.Xml.Linq.XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value).ToHashSet();
            foreach (Match key in Regex.Matches(vmSources, "\"((?:History|Export|Event|Sensor|Times|Sag|Surge)\\w+)\""))
                Check(resources.Contains(key.Groups[1].Value), "localized key exists " + key.Groups[1].Value);
        }
        var source = File.ReadAllText(Path.Combine(root, "UpsMonitor.App/MainViewModel.cs"));
        Check(source.Contains("index is DashboardIndex or HistoryIndex"), "Dashboard and History both target History");
        Check(source.Contains("History.SetActive(value && IsHistoryRefreshTarget(_selectedNavigationIndex))") &&
            source.Contains("History.SetActive(_isWindowVisible && IsHistoryRefreshTarget(value))"), "visible/selected routing integration");
        Check(source.Contains("History.SetSnapshot(snapshot)") && source.Contains("History.RefreshForSnapshot()") && source.Contains("History.Dispose()"), "snapshot and lifecycle integration");
        var document = System.Xml.Linq.XDocument.Parse(xaml);
        var grid = document.Descendants().Single(e => e.Name.LocalName == "DataGrid" && e.Attribute("ItemsSource")?.Value == "{Binding History.EnergyReports}");
        Check(grid.Attribute("IsReadOnly")?.Value == "True" && grid.Attribute("SelectedItem") is null, "report has no bound selection or editing workflow");
    }
}
