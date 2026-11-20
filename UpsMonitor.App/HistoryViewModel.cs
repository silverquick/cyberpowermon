using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

namespace UpsMonitor.App;

/// <summary>History and Dashboard charts. All entry points and continuations run on the UI dispatcher.</summary>
public sealed class HistoryViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly HistoryQueries? _queries;
    private readonly Action<Exception> _onError;
    private readonly Func<DateTimeOffset> _now;
    private bool _active;
    private bool _disposed;
    private readonly AppConfiguration _configuration;
    private UpsSnapshot? _lastSnapshot;
    private EnergyReportPeriod _energyReportGranularity = EnergyReportPeriod.Day;
    private PowerTroubleSummary? _troubleSummary;
    private string _troubleSummaryText = "-";

    private IReadOnlyList<HistoryRangeOption> _historyRangeOptions = [];
    private HistoryRangeOption? _selectedHistoryRange;
    private CancellationTokenSource? _historyRefreshCancellation;
    private DateTimeOffset _lastHistoryRefresh = DateTimeOffset.MinValue;
    private string _historyStatus = string.Empty;
    private bool _isHistoryLoading;
    private HistoryChartData? _voltageHistory;
    private HistoryChartData? _loadHistory;
    private HistoryChartData? _powerHistory;
    private HistoryChartData? _batteryChargeHistory;
    private HistoryChartData? _runtimeHistory;
    private HistoryChartData? _batteryVoltageHistory;
    private HistoryChartData? _healthHistory;
    private HistoryChartData? _powerFactorHistory;
    private HistoryChartData? _frequencyHistory;
    private HistoryChartData? _temperatureHistory;
    private HistoryChartData? _energyHistory;
    private HistoryStateTimelineData? _stateHistory;
    private string _periodOutageSummaryText = "-";
    private string _periodVoltageSummaryText = "-";
    private string _periodPowerSummaryText = "-";
    private string _periodEnergySummaryText = "-";
    private string _periodCostSummaryText = "-";
    private string _periodCo2SummaryText = "-";
    private TelemetryPeriodSummary? _lastPeriodSummary;
    private IReadOnlyList<EnergyReportItem> _energyReports = [];

    public int HistoryExportRange { get; set; }
    public HistoryViewModel(SqliteTelemetryStore? store, AppConfiguration configuration, ExportViewModel exports,
        Action<Exception> onError, Action<Exception> onCommandError)
        : this(HistoryQueries.FromStore(store), configuration, onError, exports: exports, onCommandError: onCommandError) { }

    internal HistoryViewModel(HistoryQueries? queries, AppConfiguration configuration, Action<Exception> onError,
        Func<DateTimeOffset>? now = null, ExportViewModel? exports = null, Action<Exception>? onCommandError = null)
    {
        _queries = queries;
        _configuration = configuration;
        _onError = onError;
        _now = now ?? (() => DateTimeOffset.Now);
        RefreshHistoryRangeOptions();
        RefreshHistoryCommand = new AsyncRelayCommand(RefreshHistoryAsync, onCommandError ?? onError);
        ExportTelemetryCsvCommand = new AsyncRelayCommand(
            () => !_disposed && exports is not null ? exports.ExportTelemetryAsync(false, ExportRange) : Task.CompletedTask, onCommandError ?? onError);
        ExportTelemetryJsonCommand = new AsyncRelayCommand(
            () => !_disposed && exports is not null ? exports.ExportTelemetryAsync(true, ExportRange) : Task.CompletedTask, onCommandError ?? onError);
    }

    public ICommand RefreshHistoryCommand { get; }
    public ICommand ExportTelemetryCsvCommand { get; }
    public ICommand ExportTelemetryJsonCommand { get; }

    public void SetSnapshot(UpsSnapshot snapshot) => _lastSnapshot = snapshot;

    public void NotifySnapshotProperties()
    {
        OnPropertyChanged(nameof(FrequencyEmptyText));
        OnPropertyChanged(nameof(TemperatureEmptyText));
    }

    public void SetActive(bool active)
    {
        _active = active;
        if (!_disposed && active && _lastSnapshot is not null && _now() - _lastHistoryRefresh >= TimeSpan.FromSeconds(5))
            _ = RefreshHistoryAsync();
    }

    public void RefreshForSnapshot()
    {
        if (!_disposed && _queries is not null && _active && _lastSnapshot is { } snapshot &&
            snapshot.Timestamp - _lastHistoryRefresh >= TimeSpan.FromSeconds(10))
            _ = RefreshHistoryAsync();
    }

    public void RefreshLanguage(bool refresh = true)
    {
        if (_disposed) return;
        RefreshHistoryRangeOptions();
        if (refresh) RefreshIfActive();
    }

    public void RefreshIfActive()
    {
        if (!_disposed && _active && _lastSnapshot is not null) _ = RefreshHistoryAsync();
    }

    public void SetWaitingState() => HistoryStatus = _queries is null ? L("HistoryUnavailable") : L("HistoryWaitingForUps");
    public void RefreshPeriodCost() => UpdatePeriodSummaries(_lastPeriodSummary);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        var request = _historyRefreshCancellation;
        _historyRefreshCancellation = null;
        request?.Cancel(); // Each async request disposes its own CTS in finally.
    }

    public (DateTimeOffset From, DateTimeOffset To) ExportRange()
    {
        var to = _now();
        var from = HistoryExportRange == 1 ? DateTimeOffset.MinValue : to - (SelectedHistoryRange?.Duration ?? TimeSpan.FromDays(7));
        var telemetryTo = HistoryExportRange == 1 ? DateTimeOffset.MaxValue : to;
        return (from, telemetryTo);
    }
    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<EnergyReportItem> EnergyReports => _energyReports;

    private void UpdateEnergyReports(IReadOnlyList<EnergyReportItem> reports)
    {
        if (_energyReports.SequenceEqual(reports)) return;
        _energyReports = reports;
        OnPropertyChanged(nameof(EnergyReports));
    }

    public EnergyReportPeriod EnergyReportGranularity
    {
        get => _energyReportGranularity;
        set
        {
            if (SetField(ref _energyReportGranularity, value))
            {
                OnPropertyChanged(nameof(IsEnergyReportDay));
                OnPropertyChanged(nameof(IsEnergyReportMonth));
                if (_lastSnapshot is not null && _active)
                {
                    _ = RefreshHistoryAsync();
                }
            }
        }
    }

    public bool IsEnergyReportDay
    {
        get => EnergyReportGranularity == EnergyReportPeriod.Day;
        set
        {
            if (value)
            {
                EnergyReportGranularity = EnergyReportPeriod.Day;
            }
        }
    }

    public bool IsEnergyReportMonth
    {
        get => EnergyReportGranularity == EnergyReportPeriod.Month;
        set
        {
            if (value)
            {
                EnergyReportGranularity = EnergyReportPeriod.Month;
            }
        }
    }
    public PowerTroubleSummary? TroubleSummary { get => _troubleSummary; private set => SetField(ref _troubleSummary, value); }
    public string TroubleSummaryText { get => _troubleSummaryText; private set => SetField(ref _troubleSummaryText, value); }

    public IReadOnlyList<HistoryRangeOption> HistoryRangeOptions
    {
        get => _historyRangeOptions;
        private set => SetField(ref _historyRangeOptions, value);
    }

    public HistoryRangeOption? SelectedHistoryRange
    {
        get => _selectedHistoryRange;
        set
        {
            if (SetField(ref _selectedHistoryRange, value) && value is not null && _lastSnapshot is not null)
            {
                _ = RefreshHistoryAsync();
            }
        }
    }

    public string HistoryStatus { get => _historyStatus; private set => SetField(ref _historyStatus, value); }

    public bool IsHistoryLoading { get => _isHistoryLoading; private set => SetField(ref _isHistoryLoading, value); }

    public HistoryChartData? VoltageHistory { get => _voltageHistory; private set => SetField(ref _voltageHistory, value); }

    public HistoryChartData? LoadHistory { get => _loadHistory; private set => SetField(ref _loadHistory, value); }

    public HistoryChartData? PowerHistory { get => _powerHistory; private set => SetField(ref _powerHistory, value); }

    public HistoryChartData? BatteryChargeHistory { get => _batteryChargeHistory; private set => SetField(ref _batteryChargeHistory, value); }

    public HistoryChartData? RuntimeHistory { get => _runtimeHistory; private set => SetField(ref _runtimeHistory, value); }

    public HistoryChartData? BatteryVoltageHistory { get => _batteryVoltageHistory; private set => SetField(ref _batteryVoltageHistory, value); }

    public HistoryChartData? HealthHistory { get => _healthHistory; private set => SetField(ref _healthHistory, value); }

    public HistoryChartData? PowerFactorHistory { get => _powerFactorHistory; private set => SetField(ref _powerFactorHistory, value); }

    public HistoryChartData? FrequencyHistory { get => _frequencyHistory; private set => SetField(ref _frequencyHistory, value); }

    public HistoryChartData? TemperatureHistory { get => _temperatureHistory; private set => SetField(ref _temperatureHistory, value); }

    public HistoryChartData? EnergyHistory { get => _energyHistory; private set => SetField(ref _energyHistory, value); }

    public HistoryStateTimelineData? StateHistory { get => _stateHistory; private set => SetField(ref _stateHistory, value); }

    public string PeriodOutageSummaryText { get => _periodOutageSummaryText; private set => SetField(ref _periodOutageSummaryText, value); }
    public string PeriodVoltageSummaryText { get => _periodVoltageSummaryText; private set => SetField(ref _periodVoltageSummaryText, value); }
    public string PeriodPowerSummaryText { get => _periodPowerSummaryText; private set => SetField(ref _periodPowerSummaryText, value); }
    public string PeriodEnergySummaryText { get => _periodEnergySummaryText; private set => SetField(ref _periodEnergySummaryText, value); }
    public string PeriodCostSummaryText { get => _periodCostSummaryText; private set => SetField(ref _periodCostSummaryText, value); }
    public string PeriodCo2SummaryText { get => _periodCo2SummaryText; private set => SetField(ref _periodCo2SummaryText, value); }

    public bool HasFrequencySensor => _lastSnapshot?.Frequency != null || (_lastSnapshot?.Telemetry.Any(t => t.UsagePage == 0x84 && t.Usage == 0x32) ?? false);
    public bool HasTemperatureSensor => _lastSnapshot?.Temperature != null || (_lastSnapshot?.Telemetry.Any(t => t.UsagePage == 0x84 && t.Usage == 0x36) ?? false);
    public string FrequencyEmptyText => HasFrequencySensor ? L("HistoryNoData") : L("SensorUnsupported");
    public string TemperatureEmptyText => HasTemperatureSensor ? L("HistoryNoData") : L("SensorUnsupported");

    public async Task RefreshHistoryAsync()
    {
        if (_disposed) return;
        if (_queries is null)
        {
            HistoryStatus = L("HistoryUnavailable");
            return;
        }

        if (_lastSnapshot?.Device is not { } device || SelectedHistoryRange is not { } range)
        {
            HistoryStatus = L("HistoryWaitingForUps");
            return;
        }

        var previousCancellation = _historyRefreshCancellation;
        var refreshCancellation = new CancellationTokenSource();
        _historyRefreshCancellation = refreshCancellation;
        previousCancellation?.Cancel();

        IsHistoryLoading = true;
        HistoryStatus = L("HistoryLoading");
        var to = _now();
        var from = to - range.Duration;
        _lastHistoryRefresh = to;

        try
        {
            var history = await _queries.History(
                UpsDeviceIdentity.Create(device),
                from,
                to,
                Enum.GetValues<TelemetryMetric>(),
                refreshCancellation.Token);
            if (!IsCurrent()) return;
            var statistics = await _queries.Statistics(refreshCancellation.Token);
            if (!IsCurrent()) return;
            var markers = history.Events.Select(ToHistoryEventMarker).ToArray();

            VoltageHistory = Chart(
                history,
                markers,
                [
                    Series(TelemetryMetric.InputVoltage, "HistorySeriesInputVoltage", "#3B82F6"),
                    Series(TelemetryMetric.OutputVoltage, "HistorySeriesOutputVoltage", "#22D3EE"),
                ],
                VoltageReferenceLines());
            LoadHistory = Chart(
                history,
                markers,
                [Series(TelemetryMetric.LoadPercent, "HistorySeriesLoad", "#F59E0B")]);
            PowerHistory = Chart(
                history,
                markers,
                [
                    Series(TelemetryMetric.ActivePowerWatts, "HistorySeriesActivePower", "#60A5FA"),
                    Series(TelemetryMetric.ApparentPowerVoltAmperes, "HistorySeriesApparentPower", "#A78BFA"),
                ],
                PowerReferenceLines());
            BatteryChargeHistory = Chart(
                history,
                markers,
                [Series(TelemetryMetric.BatteryPercent, "HistorySeriesBatteryCharge", "#22C55E")],
                BatteryChargeReferenceLines());
            RuntimeHistory = Chart(
                history,
                markers,
                [Series(TelemetryMetric.RuntimeMinutes, "HistorySeriesRuntime", "#A78BFA")]);
            BatteryVoltageHistory = Chart(
                history,
                markers,
                [Series(TelemetryMetric.BatteryVoltage, "HistorySeriesBatteryVoltage", "#38BDF8")]);
            HealthHistory = HealthChart(history, markers);
            PowerFactorHistory = PowerFactorChart(history, markers);
            FrequencyHistory = Chart(
                history,
                markers,
                [Series(TelemetryMetric.FrequencyHertz, "HistorySeriesFrequency", "#10B981")],
                FrequencyReferenceLines());
            TemperatureHistory = Chart(
                history,
                markers,
                [Series(TelemetryMetric.TemperatureCelsius, "HistorySeriesTemperature", "#F43F5E")]);
            EnergyHistory = EnergyChart(history, markers);
            UpdatePeriodSummaries(history.Summary);

            // 電力量レポート（日次/月次）の取得
            DateTimeOffset energyFrom;
            if (EnergyReportGranularity == EnergyReportPeriod.Month)
            {
                var firstDayOfThisMonth = new DateTimeOffset(to.Year, to.Month, 1, 0, 0, 0, to.Offset);
                energyFrom = firstDayOfThisMonth.AddMonths(-11);
            }
            else
            {
                var today = new DateTimeOffset(to.Year, to.Month, to.Day, 0, 0, 0, to.Offset);
                energyFrom = today.AddDays(-29);
            }

            var energyReports = await _queries.Energy(
                UpsDeviceIdentity.Create(device),
                energyFrom,
                to,
                EnergyReportGranularity,
                _configuration.Monitoring.ElectricityRatePerKwh,
                refreshCancellation.Token);

            if (!IsCurrent()) return;
            UpdateEnergyReports(energyReports);

            // 停電・電圧サマリーの取得
            var troubleSummary = await _queries.Trouble(
                UpsDeviceIdentity.Create(device),
                from,
                to,
                _configuration.Alerts.LowVoltageWarningThreshold,
                _configuration.Alerts.HighVoltageWarningThreshold,
                refreshCancellation.Token);

            if (!IsCurrent()) return;
            TroubleSummary = troubleSummary;
            if (TroubleSummary is not null)
            {
                TroubleSummaryText = $"{TroubleSummary.TotalOutages} {L("TimesUnit")} ({FormatDuration(TroubleSummary.TotalOutageDuration)}) / {L("SagLabel")}: {TroubleSummary.VoltageSagCount}, {L("SurgeLabel")}: {TroubleSummary.VoltageSurgeCount}";
            }

            StateHistory = new HistoryStateTimelineData
            {
                From = history.From,
                To = history.To,
                StateChanges = history.StateChanges,
                Events = markers,
            };
            HistoryStatus = LocalizationManager.Format(
                "HistoryStatusFormat",
                history.SourceSampleCount,
                statistics.RawValueCount,
                statistics.EventCount,
                DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        }
        catch (OperationCanceledException) when (refreshCancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            if (IsCurrent())
            {
                HistoryStatus = LocalizationManager.Format("HistoryLoadErrorFormat", exception.Message);
                _onError(new InvalidOperationException(HistoryStatus, exception));
            }
        }
        finally
        {
            if (ReferenceEquals(_historyRefreshCancellation, refreshCancellation))
            {
                _historyRefreshCancellation = null;
                IsHistoryLoading = false;
            }
            refreshCancellation.Dispose();
        }

        bool IsCurrent() => !_disposed && !refreshCancellation.IsCancellationRequested &&
            ReferenceEquals(_historyRefreshCancellation, refreshCancellation);

        (TelemetryMetric Metric, string LabelKey, string Color) Series(
            TelemetryMetric metric,
            string labelKey,
            string color) => (metric, labelKey, color);
    }

    private static HistoryChartData Chart(
        TelemetryHistoryResult history,
        IReadOnlyList<HistoryEventMarker> markers,
        IReadOnlyList<(TelemetryMetric Metric, string LabelKey, string Color)> definitions,
        IReadOnlyList<HistoryChartReferenceLine>? referenceLines = null) => new()
        {
            From = history.From,
            To = history.To,
            Series = definitions
                .Select(definition => new HistoryChartSeries(
                    L(definition.LabelKey),
                    definition.Color,
                    HistoryPoints(history, definition.Metric)))
                .ToArray(),
            Events = markers,
            ReferenceLines = referenceLines ?? [],
        };

    private static HistoryChartData HealthChart(
        TelemetryHistoryResult history,
        IReadOnlyList<HistoryEventMarker> markers)
    {
        IReadOnlyList<TelemetryHistoryPoint> healthPoints;
        IReadOnlyList<TelemetryHistoryPoint> relativePoints;
        if (history.BatteryHealthSeries is { } series)
        {
            healthPoints = series.HealthPoints;
            relativePoints = series.RelativePerformancePoints;
        }
        else
        {
            healthPoints = history.BatteryHealth
                .Where(item => item.HealthPercent.HasValue)
                .Select(item => Point(item.Timestamp, item.HealthPercent!.Value))
                .ToArray();
            relativePoints = history.BatteryHealth
                .Where(item => item.RelativePerformancePercent.HasValue)
                .Select(item => Point(item.Timestamp, item.RelativePerformancePercent!.Value))
                .ToArray();
        }

        return new HistoryChartData
        {
            From = history.From,
            To = history.To,
            Series =
            [
                new(L("HistorySeriesVendorHealth"), "#38BDF8", healthPoints),
                new(L("HistorySeriesRelativeRuntime"), "#A78BFA", relativePoints),
            ],
            Events = markers,
        };

        static TelemetryHistoryPoint Point(DateTimeOffset timestamp, double value) =>
            new(timestamp, value, value, value, value);
    }

    private static IReadOnlyList<TelemetryHistoryPoint> HistoryPoints(
        TelemetryHistoryResult history,
        TelemetryMetric metric) =>
        history.Metrics.TryGetValue(metric, out var metricHistory) ? metricHistory.Points : [];

    private IReadOnlyList<HistoryChartReferenceLine> VoltageReferenceLines()
    {
        var lines = new List<HistoryChartReferenceLine>();
        if (_lastSnapshot?.LowVoltageTransfer is { } low)
        {
            lines.Add(new(L("HistoryReferenceLowTransfer"), "#F59E0B", low));
        }

        if (_lastSnapshot?.HighVoltageTransfer is { } high)
        {
            lines.Add(new(L("HistoryReferenceHighTransfer"), "#F97316", high));
        }

        return lines;
    }

    private IReadOnlyList<HistoryChartReferenceLine> PowerReferenceLines()
    {
        var lines = new List<HistoryChartReferenceLine>();
        if (_lastSnapshot?.ConfigActivePower is { } ratedWatts)
        {
            lines.Add(new(L("HistoryReferenceRatedPower"), "#EF4444", ratedWatts));
        }

        return lines;
    }

    private IReadOnlyList<HistoryChartReferenceLine> BatteryChargeReferenceLines()
    {
        var lines = new List<HistoryChartReferenceLine>();
        if (_lastSnapshot?.WarningCapacityLimit is { } warn)
        {
            lines.Add(new(L("HistoryReferenceWarningCapacity"), "#F59E0B", warn));
        }

        if (_lastSnapshot?.RemainingCapacityLimit is { } low)
        {
            lines.Add(new(L("HistoryReferenceShutdownCapacity"), "#EF4444", low));
        }

        return lines;
    }

    private static HistoryChartData PowerFactorChart(
        TelemetryHistoryResult history,
        IReadOnlyList<HistoryEventMarker> markers)
    {
        var activePoints = HistoryPoints(history, TelemetryMetric.ActivePowerWatts);
        var apparentPoints = HistoryPoints(history, TelemetryMetric.ApparentPowerVoltAmperes);
        var pfPoints = new List<TelemetryHistoryPoint>();

        var apparentByTime = apparentPoints.ToDictionary(p => p.Timestamp);
        foreach (var ap in activePoints)
        {
            if (apparentByTime.TryGetValue(ap.Timestamp, out var va) && va.Average > 1.0)
            {
                var factor = Math.Clamp(ap.Average / va.Average * 100.0, 0.0, 100.0);
                pfPoints.Add(new TelemetryHistoryPoint(ap.Timestamp, factor, factor, factor, factor));
            }
        }

        return new HistoryChartData
        {
            From = history.From,
            To = history.To,
            Series = [new HistoryChartSeries(L("HistorySeriesPowerFactor"), "#38BDF8", pfPoints)],
            Events = markers,
        };
    }

    private static HistoryChartData EnergyChart(
        TelemetryHistoryResult history,
        IReadOnlyList<HistoryEventMarker> markers)
    {
        var activePoints = HistoryPoints(history, TelemetryMetric.ActivePowerWatts);
        var energyPoints = new List<TelemetryHistoryPoint>();
        var accumulatedKwh = 0.0;

        for (var i = 0; i < activePoints.Count; i++)
        {
            if (i > 0)
            {
                var pPrev = activePoints[i - 1];
                var pCurr = activePoints[i];
                var dtHours = (pCurr.Timestamp - pPrev.Timestamp).TotalHours;
                if (dtHours is > 0 and <= 2.0)
                {
                    var wattHours = ((pPrev.Average + pCurr.Average) / 2.0) * dtHours;
                    accumulatedKwh += wattHours / 1000.0;
                }
            }

            energyPoints.Add(new TelemetryHistoryPoint(activePoints[i].Timestamp, accumulatedKwh, accumulatedKwh, accumulatedKwh, accumulatedKwh));
        }

        return new HistoryChartData
        {
            From = history.From,
            To = history.To,
            Series = [new HistoryChartSeries(L("HistorySeriesEnergy"), "#10B981", energyPoints)],
            Events = markers,
        };
    }

    private IReadOnlyList<HistoryChartReferenceLine> FrequencyReferenceLines()
    {
        return
        [
            new(L("HistoryReference50Hz"), "#6EE7B7", 50.0),
            new(L("HistoryReference60Hz"), "#93C5FD", 60.0),
        ];
    }

    private void UpdatePeriodSummaries(TelemetryPeriodSummary? summary)
    {
        _lastPeriodSummary = summary;
        if (summary is null)
        {
            PeriodOutageSummaryText = "-";
            PeriodVoltageSummaryText = "-";
            PeriodPowerSummaryText = "-";
            PeriodEnergySummaryText = "-";
            PeriodCostSummaryText = "-";
            PeriodCo2SummaryText = "-";
            return;
        }

        PeriodOutageSummaryText = $"{summary.OutageCount} {L("TimesUnit")} ({FormatDuration(summary.TotalOutageDuration)})";
        PeriodVoltageSummaryText = summary.MinInputVoltage.HasValue && summary.MaxInputVoltage.HasValue && summary.AvgInputVoltage.HasValue
            ? $"Min {summary.MinInputVoltage.Value:0.#}V  Avg {summary.AvgInputVoltage.Value:0.#}V  Max {summary.MaxInputVoltage.Value:0.#}V"
            : "-";
        PeriodPowerSummaryText = summary.AvgActivePowerWatts.HasValue
            ? $"Avg {summary.AvgActivePowerWatts.Value:0.#}W  (Peak {summary.PeakActivePowerWatts.GetValueOrDefault():0.#}W / {summary.PeakLoadPercent.GetValueOrDefault():0.#}%)"
            : "-";
        PeriodEnergySummaryText = summary.TotalEnergyKwh.HasValue
            ? $"{summary.TotalEnergyKwh.Value:0.##} kWh  (Min Bat {summary.MinBatteryPercent.GetValueOrDefault():0.#}%)"
            : "-";

        if (summary.TotalEnergyKwh is { } kwh)
        {
            var cost = kwh * _configuration.Monitoring.ElectricityRatePerKwh;
            PeriodCostSummaryText = UiTextFormatting.PeriodCost(cost, _configuration.Monitoring.ElectricityRatePerKwh);
            var co2 = kwh * 0.457;
            PeriodCo2SummaryText = $"{co2:0.##} kg-CO₂";
        }
        else
        {
            PeriodCostSummaryText = "-";
            PeriodCo2SummaryText = "-";
        }
    }

    private static HistoryEventMarker ToHistoryEventMarker(UpsEvent upsEvent) => new(
        upsEvent.Timestamp,
        LocalizeEventType(upsEvent.Type),
        upsEvent.Type switch
        {
            UpsEventType.PowerLost or UpsEventType.BatteryLow or UpsEventType.RuntimeLow => "#F59E0B",
            UpsEventType.BatteryCritical or UpsEventType.OverloadDetected => "#EF4444",
            UpsEventType.PowerRestored => "#22C55E",
            UpsEventType.UpsReconnected => "#3B82F6",
            _ => "#94A3B8",
        });

    private static string LocalizeEventType(UpsEventType type) => L(type switch
    {
        UpsEventType.PowerLost => "EventPowerLost",
        UpsEventType.PowerRestored => "EventPowerRestored",
        UpsEventType.BatteryLow => "EventBatteryLow",
        UpsEventType.BatteryCritical => "EventBatteryCritical",
        UpsEventType.RuntimeLow => "EventRuntimeLow",
        UpsEventType.OverloadDetected => "EventOverload",
        UpsEventType.UpsDisconnected => "EventUpsDisconnected",
        UpsEventType.UpsReconnected => "EventUpsReconnected",
        _ => "Unknown",
    });

    private void RefreshHistoryRangeOptions()
    {
        var selectedKey = SelectedHistoryRange?.Key ?? "1h";
        HistoryRangeOptions = LocalizedOption.UpdateLabels(HistoryRangeOptions,
        [
            new("1h", TimeSpan.FromHours(1), L("HistoryRange1Hour")),
            new("6h", TimeSpan.FromHours(6), L("HistoryRange6Hours")),
            new("24h", TimeSpan.FromHours(24), L("HistoryRange24Hours")),
            new("7d", TimeSpan.FromDays(7), L("HistoryRange7Days")),
            new("30d", TimeSpan.FromDays(30), L("HistoryRange30Days")),
        ]);
        SelectedHistoryRange = HistoryRangeOptions.First(item => item.Key == selectedKey);
    }

    private static string FormatDuration(TimeSpan? value)
    {
        if (value is not { } duration)
        {
            return "N/A";
        }

        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";
    }

    private static string L(string key) => LocalizationManager.Get(key);

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
