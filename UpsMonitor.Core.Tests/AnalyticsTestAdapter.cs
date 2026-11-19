using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;
namespace UpsMonitor.App;
// Exact production members; SourceParity prevents test adapter drift.
internal sealed class AnalyticsTestAdapter : INotifyPropertyChanged
{
    private readonly SqliteTelemetryStore? _historyStore;
    private UpsSnapshot? _lastSnapshot;
    public string LastError { get; private set; } = "";
    public AnalyticsTestAdapter(SqliteTelemetryStore? store) { _historyStore = store; RefreshAnalyticsOptions(); }
    public void SetSnapshot(UpsSnapshot snapshot) => _lastSnapshot = snapshot;
    public Task Refresh() => RefreshAnalyticsAsync();
    public void Language() => RefreshAnalyticsOptions();
    public event PropertyChangedEventHandler? PropertyChanged;
    private static string L(string key) => LocalizationManager.Get(key);
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; OnPropertyChanged(name); return true; }
    private IReadOnlyList<AnalyticsMetricOption> _analyticsMetricOptions = [];
    private AnalyticsMetricOption? _selectedAnalyticsMetric;
    private IReadOnlyList<AnalyticsRangeOption> _analyticsRangeOptions = [];
    private AnalyticsRangeOption? _selectedAnalyticsRange;
    private WeeklyPatternResult? _weeklyPattern;
    private string _analyticsStatus = string.Empty;
    private bool _isAnalyticsLoading;
    private string _analyticsPeakHourText = "-";
    private string _analyticsLowestHourText = "-";
    private string _analyticsOverallAvgText = "-";
    private string _analyticsSampleCountText = "-";
    private DateTimeOffset _lastAnalyticsRefresh = DateTimeOffset.MinValue;
    private CancellationTokenSource? _analyticsRefreshCancellation;

    public IReadOnlyList<AnalyticsMetricOption> AnalyticsMetricOptions
    {
        get => _analyticsMetricOptions;
        private set => SetField(ref _analyticsMetricOptions, value);
    }

    public AnalyticsMetricOption? SelectedAnalyticsMetric
    {
        get => _selectedAnalyticsMetric;
        set
        {
            if (SetField(ref _selectedAnalyticsMetric, value) && value is not null && _lastSnapshot is not null)
            {
                OnPropertyChanged(nameof(AnalyticsUnit));
                OnPropertyChanged(nameof(AnalyticsHeatmapTitle));
                _ = RefreshAnalyticsAsync();
            }
        }
    }

    public IReadOnlyList<AnalyticsRangeOption> AnalyticsRangeOptions
    {
        get => _analyticsRangeOptions;
        private set => SetField(ref _analyticsRangeOptions, value);
    }

    public AnalyticsRangeOption? SelectedAnalyticsRange
    {
        get => _selectedAnalyticsRange;
        set
        {
            if (SetField(ref _selectedAnalyticsRange, value) && value is not null && _lastSnapshot is not null)
            {
                _ = RefreshAnalyticsAsync();
            }
        }
    }

    public WeeklyPatternResult? WeeklyPattern { get => _weeklyPattern; private set => SetField(ref _weeklyPattern, value); }
    public string AnalyticsStatus { get => _analyticsStatus; private set => SetField(ref _analyticsStatus, value); }
    public bool IsAnalyticsLoading { get => _isAnalyticsLoading; private set => SetField(ref _isAnalyticsLoading, value); }
    public string AnalyticsPeakHourText { get => _analyticsPeakHourText; private set => SetField(ref _analyticsPeakHourText, value); }
    public string AnalyticsLowestHourText { get => _analyticsLowestHourText; private set => SetField(ref _analyticsLowestHourText, value); }
    public string AnalyticsOverallAvgText { get => _analyticsOverallAvgText; private set => SetField(ref _analyticsOverallAvgText, value); }
    public string AnalyticsSampleCountText { get => _analyticsSampleCountText; private set => SetField(ref _analyticsSampleCountText, value); }
    public string AnalyticsUnit => SelectedAnalyticsMetric?.Unit ?? string.Empty;
    public string AnalyticsHeatmapTitle => LocalizationManager.Format("AnalyticsHeatmapTitleFormat", SelectedAnalyticsMetric?.DisplayName);

    private void RefreshAnalyticsOptions()
    {
        var currentMetric = SelectedAnalyticsMetric?.Metric ?? WeeklyPatternMetric.ActivePower;
        AnalyticsMetricOptions = LocalizedOption.UpdateLabels(AnalyticsMetricOptions,
        [
            new(WeeklyPatternMetric.ActivePower, L("AnalyticsMetricPower"), "W"),
            new(WeeklyPatternMetric.InputVoltage, L("AnalyticsMetricVoltage"), "V"),
            new(WeeklyPatternMetric.LoadPercent, L("AnalyticsMetricLoad"), "%"),
            new(WeeklyPatternMetric.PowerFactor, L("AnalyticsMetricPowerFactor"), "%"),
        ]);
        SelectedAnalyticsMetric = AnalyticsMetricOptions.FirstOrDefault(item => item.Metric == currentMetric) ?? AnalyticsMetricOptions[0];

        var currentRange = SelectedAnalyticsRange;
        AnalyticsRangeOptions = LocalizedOption.UpdateLabels(AnalyticsRangeOptions,
        [
            new(TimeSpan.FromDays(7), L("AnalyticsRange7D")),
            new(TimeSpan.FromDays(30), L("AnalyticsRange30D")),
            new(TimeSpan.FromDays(90), L("AnalyticsRange90D")),
            new(null, L("AnalyticsRangeAll")),
        ]);
        SelectedAnalyticsRange = currentRange ?? AnalyticsRangeOptions[1];
        OnPropertyChanged(nameof(AnalyticsHeatmapTitle));
    }

    private async Task RefreshAnalyticsAsync()
    {
        if (_historyStore is null)
        {
            AnalyticsStatus = L("HistoryUnavailable");
            return;
        }

        if (_lastSnapshot?.Device is not { } device)
        {
            AnalyticsStatus = L("HistoryWaitingForUps");
            return;
        }

        var metric = SelectedAnalyticsMetric?.Metric ?? WeeklyPatternMetric.ActivePower;
        var duration = SelectedAnalyticsRange?.Duration;
        var to = DateTimeOffset.Now;
        var from = duration.HasValue ? to - duration.Value : DateTimeOffset.UnixEpoch;

        var previousCancellation = _analyticsRefreshCancellation;
        var refreshCancellation = new CancellationTokenSource();
        _analyticsRefreshCancellation = refreshCancellation;
        previousCancellation?.Cancel();
        previousCancellation?.Dispose();

        IsAnalyticsLoading = true;
        AnalyticsStatus = L("HistoryLoading");
        _lastAnalyticsRefresh = to;

        try
        {
            var result = await _historyStore.QueryWeeklyPatternAsync(
                UpsDeviceIdentity.Create(device),
                from,
                to,
                metric,
                refreshCancellation.Token);

            WeeklyPattern = result;

            var unit = SelectedAnalyticsMetric?.Unit ?? string.Empty;
            if (result.PeakHour != null && result.PeakHour.SampleCount > 0)
            {
                var dowName = LocalizationManager.Get($"DayFull_{result.PeakHour.DayOfWeek}") ?? $"{result.PeakHour.DayOfWeek}";
                AnalyticsPeakHourText = $"{dowName} {result.PeakHour.HourOfDay:D2}:00 ({result.PeakHour.Average:0.#} {unit})";
            }
            else
            {
                AnalyticsPeakHourText = "-";
            }

            if (result.LowestHour != null && result.LowestHour.SampleCount > 0)
            {
                var dowName = LocalizationManager.Get($"DayFull_{result.LowestHour.DayOfWeek}") ?? $"{result.LowestHour.DayOfWeek}";
                AnalyticsLowestHourText = $"{dowName} {result.LowestHour.HourOfDay:D2}:00 ({result.LowestHour.Average:0.#} {unit})";
            }
            else
            {
                AnalyticsLowestHourText = "-";
            }

            AnalyticsOverallAvgText = result.TotalSamples > 0 ? $"{result.OverallAvg:0.#} {unit}" : "-";
            AnalyticsSampleCountText = LocalizationManager.Format("AnalyticsSampleCountFormat", result.TotalSamples, result.EquivalentDays);

            AnalyticsStatus = LocalizationManager.Format(
                "AnalyticsStatusFormat",
                result.TotalSamples,
                DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        }
        catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AnalyticsStatus = LocalizationManager.Format("HistoryLoadErrorFormat", exception.Message);
            LastError = AnalyticsStatus;
        }
        finally
        {
            if (ReferenceEquals(_analyticsRefreshCancellation, refreshCancellation))
            {
                IsAnalyticsLoading = false;
            }
        }
    }

}
