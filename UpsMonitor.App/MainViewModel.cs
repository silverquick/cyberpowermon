using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

namespace UpsMonitor.App;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    public const int DashboardIndex = 0;
    public const int HistoryIndex = 1;
    public const int UpsIndex = 2;
    public const int AnalyticsIndex = 3;
    public const int DevicesIndex = 4;
    public const int ActionsIndex = 5;
    public const int LogsIndex = 6;
    public const int SettingsIndex = 7;

    public static bool IsHistoryRefreshTarget(int index) => index is DashboardIndex or HistoryIndex;
    public static bool IsAnalyticsRefreshTarget(int index) => index is AnalyticsIndex;

    private readonly UpsMonitorEngine _engine;
    private readonly JsonConfigurationStore _configurationStore;
    private readonly AppConfiguration _configuration;
    private readonly SqliteTelemetryStore? _historyStore;
    private readonly Dispatcher _dispatcher;
    private string _connectionText = string.Empty;
    private string _stateText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _statusAccent = "#64748B";
    private string _lastUpdateText = string.Empty;
    private string _lastError = string.Empty;
    private string _settingsStatus = string.Empty;
    private string _telemetryCountText = string.Empty;
    private IReadOnlyList<UpsTelemetryViewModel> _telemetryItems = [];
    private int _pollIntervalMs;
    private int _runtimeLowSeconds;
    private string _selectedLanguageCode;
    private UpsSnapshot? _lastSnapshot;
    private UpsTelemetry? _lastTelemetry;
    private double _batteryWarningThresholdPercent;
    private double _batteryCriticalThresholdPercent;
    private double _comparableLoadTolerancePercent;
    private IReadOnlyList<BatteryBaselineModeOption> _baselineModeOptions = [];
    private IReadOnlyList<VendorHealthCategoryOption> _vendorHealthCategoryOptions = [];
    private BatteryRuntimeBaselineKind _selectedBaselineKind = BatteryRuntimeBaselineKind.CurrentRelative;
    private VendorBatteryHealthCategory _selectedVendorHealthCategory = VendorBatteryHealthCategory.Unknown;
    private double _knownHealthPercent = 59;
    private string _knownHealthSource = "CyberPower BHI";
    private bool _baselineEditorInitialized;
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
    private bool _isWindowVisible;
    private int _selectedNavigationIndex;
    private UpsSnapshot? _pendingSnapshot;
    private List<UpsTelemetryViewModel> _telemetryItemsList = [];
    private string _logSearchText = string.Empty;
    private LogSeverityFilterKind _selectedLogSeverityFilter = LogSeverityFilterKind.All;
    private IReadOnlyList<LogSeverityFilterOption> _logSeverityFilterOptions = [];
    private IReadOnlyList<ThemeOption> _themeOptions = [];
    private string _webhookStatus = string.Empty;
    private double _loadSimulationTargetWatts = 200.0;
    private string _simulatedRuntimeText = "-";
    private IReadOnlyList<RuntimeEstimateTableItem> _standardLoadEstimates = [];
    private PowerTroubleSummary? _troubleSummary;
    private string _troubleSummaryText = "-";
    private EnergyReportPeriod _energyReportGranularity = EnergyReportPeriod.Day;

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

    private string _manufacturer = "N/A";
    private string _product = "No UPS detected";
    private string _serialNumber = "N/A";
    private string _vidPid = "N/A";
    private string _usage = "N/A";
    private string _devicePath = "N/A";
    private string _inputReportLength = "N/A";
    private string _featureReportLength = "N/A";
    private string _powerText = "N/A";
    private string _batteryText = "N/A";
    private string _batteryHealthText = "N/A";
    private string _batteryHealthDetailText = string.Empty;
    private string _batteryHealthConfidenceText = "N/A";
    private string _batteryHealthMethodText = "N/A";
    private string _batteryHealthAccent = "#64748B";
    private string _batteryReplacementText = "N/A";
    private string _batteryReplacementDetailText = string.Empty;
    private string _batteryReplacementAccent = "#64748B";
    private string _batteryHealthBaselineText = string.Empty;
    private string _batteryHealthDataQualityText = string.Empty;
    private string _batteryRelativeTrendText = "N/A";
    private string _batteryHealthAnchorText = "N/A";
    private IReadOnlyList<string> _batteryHealthReasons = [];
    private double _batteryProgress;
    private string _runtimeText = "N/A";
    private string _overloadText = "N/A";
    private string _chargingText = "N/A";
    private string _dischargingText = "N/A";
    private string _lowBatteryText = "N/A";
    private string _criticalText = "N/A";
    private string _acPresentText = "N/A";
    private string _voltageText = "N/A";
    private string _currentText = "N/A";
    private string _frequencyText = "N/A";
    private string _temperatureText = "N/A";
    private string _remainingTimeLimitText = "N/A";
    private string _designCapacityText = "N/A";
    private string _fullChargeCapacityText = "N/A";
    private string _batteryVoltageText = "N/A";
    private string _nominalBatteryVoltageText = "N/A";
    private string _cycleCountText = "N/A";
    private string _needReplacementText = "N/A";
    private string _inputVoltageText = "N/A";
    private string _outputVoltageText = "N/A";
    private string _percentLoadText = "N/A";
    private string _activePowerText = "N/A";
    private string _apparentPowerText = "N/A";
    private string _fullyChargedText = "N/A";
    private string _rechargeableText = "N/A";
    private string _remainingTimeExpiredText = "N/A";
    private string _boostText = "N/A";
    private string _audibleAlarmText = "N/A";
    private string _selfTestText = "N/A";
    private string _transferRangeText = "N/A";
    private string _ratedPowerText = "N/A";
    private string _batteryChemistryText = "N/A";
    private string _oemInformationText = "N/A";
    private string _inputVoltageSummaryText = "N/A";
    private string _inputOutputText = "N/A";
    private string _reportBytesText = "N/A";
    private string _powerMarginText = "N/A";
    private string _voltageMarginText = "N/A";
    private string _avrBoostText = "N/A";
    private string _avrBoostAccent = "#94A3B8";
    private string _cellVoltageText = "N/A";

    public MainViewModel(
        UpsMonitorEngine engine,
        JsonConfigurationStore configurationStore,
        AppConfiguration configuration,
        AppPaths paths,
        SqliteTelemetryStore? historyStore)
    {
        _engine = engine;
        _configurationStore = configurationStore;
        _configuration = configuration;
        _historyStore = historyStore;
        _pollIntervalMs = configuration.Monitoring.PollIntervalMs;
        _runtimeLowSeconds = configuration.Monitoring.RuntimeLowSeconds;
        _selectedLanguageCode = LocalizationManager.CurrentLanguageCode;
        _batteryWarningThresholdPercent = configuration.BatteryHealth.WarningThresholdPercent;
        _batteryCriticalThresholdPercent = configuration.BatteryHealth.CriticalThresholdPercent;
        _comparableLoadTolerancePercent = configuration.BatteryHealth.ComparableLoadTolerancePercent;
        ConfigurationFile = paths.ConfigurationFile;
        TelemetryDatabaseFile = paths.TelemetryDatabaseFile;
        LogsDirectory = paths.LogsDirectory;
        _dispatcher = Application.Current.Dispatcher;
        FilteredEvents = CollectionViewSource.GetDefaultView(Events);
        FilteredEvents.Filter = FilterEventItem;
        RefreshThemeOptions();
        RefreshLogSeverityOptions();
        RefreshBaselineModeOptions();
        RefreshVendorHealthCategoryOptions();
        RefreshHistoryRangeOptions();
        RefreshAnalyticsOptions();
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, SetCommandError);
        RecordRuntimeBaselineCommand = new AsyncRelayCommand(RecordRuntimeBaselineAsync, SetCommandError);
        ClearRuntimeBaselineCommand = new AsyncRelayCommand(ClearRuntimeBaselineAsync, SetCommandError);
        RefreshHistoryCommand = new AsyncRelayCommand(RefreshHistoryAsync, SetCommandError);
        RefreshAnalyticsCommand = new AsyncRelayCommand(RefreshAnalyticsAsync, SetCommandError);
        ExportTelemetryCsvCommand = new AsyncRelayCommand(ExportTelemetryCsvAsync, SetCommandError);
        ExportTelemetryJsonCommand = new AsyncRelayCommand(ExportTelemetryJsonAsync, SetCommandError);
        ExportEventsCsvCommand = new AsyncRelayCommand(ExportEventsCsvAsync, SetCommandError);
        TestNotificationCommand = new AsyncRelayCommand(() =>
        {
            TestNotificationRequested?.Invoke(UpsEventSeverity.Warning);
            return Task.CompletedTask;
        }, SetCommandError);
        TestWebhookCommand = new AsyncRelayCommand(async () =>
        {
            if (string.IsNullOrWhiteSpace(WebhookUrl))
            {
                WebhookStatus = L("WebhookUrlRequired");
                return;
            }
            WebhookStatus = L("WebhookTesting");
            var success = await WebhookNotifier.SendTestNotificationAsync(WebhookUrl);
            WebhookStatus = success ? L("WebhookTestSuccess") : L("WebhookTestFailed");
        }, SetCommandError);
        OpenMiniMonitorCommand = new AsyncRelayCommand(() =>
        {
            ShowMiniMonitorRequested?.Invoke();
            return Task.CompletedTask;
        }, SetCommandError);

        _engine.SnapshotUpdated += OnSnapshotUpdated;
        _engine.EventDetected += OnEventDetected;
        _engine.MonitorError += OnMonitorError;
        LocalizationManager.LanguageChanged += OnLanguageChanged;
        if (_historyStore is not null)
        {
            _historyStore.StorageError += OnHistoryStorageError;
        }
        ApplyWaitingState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<string, string, UpsEventSeverity>? NotificationRequested;

    public event Action<string>? TooltipUpdated;

    public string AppVersion => typeof(MainViewModel).Assembly.GetName().Version is { } v
        ? $"{v.Major}.{v.Minor}.{v.Build}"
        : "0.1.0";

    public string WindowTitle => $"PowerGuard v{AppVersion}";

    public event Action<UpsPowerState, double?, bool>? DynamicTrayIconUpdated;

    public event Action<UpsEventSeverity>? TestNotificationRequested;

    public event Action? ShowMiniMonitorRequested;

    public ObservableCollection<UpsEventViewModel> Events { get; } = [];

    public ICollectionView FilteredEvents { get; }

    public ICommand OpenMiniMonitorCommand { get; }

    public ICommand TestNotificationCommand { get; }

    public ICommand TestWebhookCommand { get; }

    public string SelectedTheme
    {
        get => _configuration.Ui.Theme;
        set
        {
            if (_configuration.Ui.Theme != value)
            {
                _configuration.Ui.Theme = value;
                ThemeManager.ApplyTheme(Application.Current, value);
                OnPropertyChanged();
            }
        }
    }

    public IReadOnlyList<ThemeOption> ThemeOptions
    {
        get => _themeOptions;
        private set => SetField(ref _themeOptions, value);
    }

    public bool EnableSoundAlerts
    {
        get => _configuration.Alerts.EnableSoundAlerts;
        set { _configuration.Alerts.EnableSoundAlerts = value; OnPropertyChanged(); }
    }

    public double HighLoadWarningPercent
    {
        get => _configuration.Alerts.HighLoadWarningPercent;
        set { _configuration.Alerts.HighLoadWarningPercent = value; OnPropertyChanged(); }
    }

    public double LowVoltageWarningThreshold
    {
        get => _configuration.Alerts.LowVoltageWarningThreshold;
        set { _configuration.Alerts.LowVoltageWarningThreshold = value; OnPropertyChanged(); }
    }

    public double HighVoltageWarningThreshold
    {
        get => _configuration.Alerts.HighVoltageWarningThreshold;
        set { _configuration.Alerts.HighVoltageWarningThreshold = value; OnPropertyChanged(); }
    }

    public bool WebhookEnabled
    {
        get => _configuration.Webhook.Enabled;
        set { _configuration.Webhook.Enabled = value; OnPropertyChanged(); }
    }

    public string WebhookUrl
    {
        get => _configuration.Webhook.Url;
        set { _configuration.Webhook.Url = value; OnPropertyChanged(); }
    }

    public bool WebhookNotifyOnPowerLost
    {
        get => _configuration.Webhook.NotifyOnPowerLost;
        set { _configuration.Webhook.NotifyOnPowerLost = value; OnPropertyChanged(); }
    }

    public bool WebhookNotifyOnPowerRestored
    {
        get => _configuration.Webhook.NotifyOnPowerRestored;
        set { _configuration.Webhook.NotifyOnPowerRestored = value; OnPropertyChanged(); }
    }

    public bool WebhookNotifyOnBatteryLow
    {
        get => _configuration.Webhook.NotifyOnBatteryLow;
        set { _configuration.Webhook.NotifyOnBatteryLow = value; OnPropertyChanged(); }
    }

    public bool WebhookNotifyOnHighLoad
    {
        get => _configuration.Webhook.NotifyOnHighLoad;
        set { _configuration.Webhook.NotifyOnHighLoad = value; OnPropertyChanged(); }
    }

    public string WebhookStatus { get => _webhookStatus; private set => SetField(ref _webhookStatus, value); }

    public bool ExternalCommandEnabled
    {
        get => _configuration.ExternalCommand.Enabled;
        set { _configuration.ExternalCommand.Enabled = value; OnPropertyChanged(); }
    }

    public string CommandOnPowerLost
    {
        get => _configuration.ExternalCommand.CommandOnPowerLost;
        set { _configuration.ExternalCommand.CommandOnPowerLost = value; OnPropertyChanged(); }
    }

    public string CommandOnPowerRestored
    {
        get => _configuration.ExternalCommand.CommandOnPowerRestored;
        set { _configuration.ExternalCommand.CommandOnPowerRestored = value; OnPropertyChanged(); }
    }

    public string CommandOnBatteryLow
    {
        get => _configuration.ExternalCommand.CommandOnBatteryLow;
        set { _configuration.ExternalCommand.CommandOnBatteryLow = value; OnPropertyChanged(); }
    }

    public string CommandOnHighLoad
    {
        get => _configuration.ExternalCommand.CommandOnHighLoad;
        set { _configuration.ExternalCommand.CommandOnHighLoad = value; OnPropertyChanged(); }
    }

    public string CommandOnVoltageAbnormal
    {
        get => _configuration.ExternalCommand.CommandOnVoltageAbnormal;
        set { _configuration.ExternalCommand.CommandOnVoltageAbnormal = value; OnPropertyChanged(); }
    }

    public double LoadSimulationTargetWatts
    {
        get => _loadSimulationTargetWatts;
        set
        {
            if (SetField(ref _loadSimulationTargetWatts, Math.Clamp(value, 10.0, 3000.0)))
            {
                RecalculateSimulation();
            }
        }
    }

    public string SimulatedRuntimeText { get => _simulatedRuntimeText; private set => SetField(ref _simulatedRuntimeText, value); }
    public IReadOnlyList<RuntimeEstimateTableItem> StandardLoadEstimates { get => _standardLoadEstimates; private set => SetField(ref _standardLoadEstimates, value); }
    public ObservableCollection<DailyEnergyReportItem> DailyEnergyReports { get; } = [];
    public ObservableCollection<EnergyReportItem> EnergyReports { get; } = [];

    public EnergyReportPeriod EnergyReportGranularity
    {
        get => _energyReportGranularity;
        set
        {
            if (SetField(ref _energyReportGranularity, value))
            {
                OnPropertyChanged(nameof(IsEnergyReportDay));
                OnPropertyChanged(nameof(IsEnergyReportMonth));
                if (_lastSnapshot is not null && IsHistoryRefreshTarget(_selectedNavigationIndex) && _isWindowVisible)
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

    public string LogSearchText
    {
        get => _logSearchText;
        set
        {
            if (SetField(ref _logSearchText, value))
            {
                FilteredEvents.Refresh();
                OnPropertyChanged(nameof(LogCountText));
            }
        }
    }

    public LogSeverityFilterKind SelectedLogSeverityFilter
    {
        get => _selectedLogSeverityFilter;
        set
        {
            if (SetField(ref _selectedLogSeverityFilter, value))
            {
                FilteredEvents.Refresh();
                OnPropertyChanged(nameof(LogCountText));
            }
        }
    }

    public IReadOnlyList<LogSeverityFilterOption> LogSeverityFilterOptions
    {
        get => _logSeverityFilterOptions;
        private set => SetField(ref _logSeverityFilterOptions, value);
    }

    public string LogCountText => LocalizationManager.Format("LogCountFormat", FilteredEvents.Cast<object>().Count(), Events.Count);

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new("ja-JP", "日本語"),
        new("en-US", "English"),
    ];

    public ICommand SaveSettingsCommand { get; }

    public ICommand RecordRuntimeBaselineCommand { get; }

    public ICommand ClearRuntimeBaselineCommand { get; }

    public ICommand RefreshHistoryCommand { get; }

    public ICommand RefreshAnalyticsCommand { get; }

    public ICommand ExportTelemetryCsvCommand { get; }

    public ICommand ExportTelemetryJsonCommand { get; }

    public ICommand ExportEventsCsvCommand { get; }

    public bool IsExiting { get; set; }

    public bool MinimizeToTray
    {
        get => _configuration.Ui.MinimizeToTray;
        set
        {
            _configuration.Ui.MinimizeToTray = value;
            OnPropertyChanged();
        }
    }

    public bool CloseToTray
    {
        get => _configuration.Ui.CloseToTray;
        set
        {
            _configuration.Ui.CloseToTray = value;
            OnPropertyChanged();
        }
    }

    public bool StartMinimized
    {
        get => _configuration.Ui.StartMinimized;
        set
        {
            _configuration.Ui.StartMinimized = value;
            OnPropertyChanged();
            if (RunOnStartup)
            {
                StartupManager.SetRunOnStartup(true, value);
            }
        }
    }

    public bool EnableNotifications
    {
        get => _configuration.Ui.EnableNotifications;
        set
        {
            _configuration.Ui.EnableNotifications = value;
            OnPropertyChanged();
        }
    }

    public bool RunOnStartup
    {
        get => _configuration.Ui.RunOnStartup;
        set
        {
            _configuration.Ui.RunOnStartup = value;
            StartupManager.SetRunOnStartup(value, StartMinimized);
            OnPropertyChanged();
        }
    }

    public string ConfigurationFile { get; }

    public string TelemetryDatabaseFile { get; }

    public string LogsDirectory { get; }

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

    public double ElectricityRatePerKwh
    {
        get => _configuration.Monitoring.ElectricityRatePerKwh;
        set
        {
            if (value is >= 0 and <= 1000)
            {
                _configuration.Monitoring.ElectricityRatePerKwh = value;
                OnPropertyChanged();
                UpdatePeriodSummaries(_lastPeriodSummary);
            }
        }
    }

    public string PowerMarginText { get => _powerMarginText; private set => SetField(ref _powerMarginText, value); }
    public string VoltageMarginText { get => _voltageMarginText; private set => SetField(ref _voltageMarginText, value); }
    public string AvrBoostText { get => _avrBoostText; private set => SetField(ref _avrBoostText, value); }
    public string AvrBoostAccent { get => _avrBoostAccent; private set => SetField(ref _avrBoostAccent, value); }
    public string CellVoltageText { get => _cellVoltageText; private set => SetField(ref _cellVoltageText, value); }

    public bool HasFrequencySensor => _lastSnapshot?.Frequency != null || (_lastSnapshot?.Telemetry.Any(t => t.UsagePage == 0x84 && t.Usage == 0x32) ?? false);
    public bool HasTemperatureSensor => _lastSnapshot?.Temperature != null || (_lastSnapshot?.Telemetry.Any(t => t.UsagePage == 0x84 && t.Usage == 0x36) ?? false);
    public string FrequencyEmptyText => HasFrequencySensor ? L("HistoryNoData") : L("SensorUnsupported");
    public string TemperatureEmptyText => HasTemperatureSensor ? L("HistoryNoData") : L("SensorUnsupported");

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

    public bool IsWindowVisible
    {
        get => _isWindowVisible;
        set
        {
            if (SetField(ref _isWindowVisible, value))
            {
                if (value)
                {
                    if (_pendingSnapshot is { } pending)
                    {
                        _pendingSnapshot = null;
                        ApplySnapshotCore(pending);
                    }

                    if (IsHistoryRefreshTarget(_selectedNavigationIndex) && _lastSnapshot is not null && DateTimeOffset.Now - _lastHistoryRefresh >= TimeSpan.FromSeconds(5))
                    {
                        _ = RefreshHistoryAsync();
                    }
                    else if (IsAnalyticsRefreshTarget(_selectedNavigationIndex) && _lastSnapshot is not null && DateTimeOffset.Now - _lastAnalyticsRefresh >= TimeSpan.FromSeconds(5))
                    {
                        _ = RefreshAnalyticsAsync();
                    }
                }
            }
        }
    }

    public int SelectedNavigationIndex
    {
        get => _selectedNavigationIndex;
        set
        {
            if (SetField(ref _selectedNavigationIndex, value))
            {
                if (IsHistoryRefreshTarget(value) && _isWindowVisible && _lastSnapshot is not null && DateTimeOffset.Now - _lastHistoryRefresh >= TimeSpan.FromSeconds(5))
                {
                    _ = RefreshHistoryAsync();
                }
                else if (IsAnalyticsRefreshTarget(value) && _isWindowVisible && _lastSnapshot is not null && DateTimeOffset.Now - _lastAnalyticsRefresh >= TimeSpan.FromSeconds(5))
                {
                    _ = RefreshAnalyticsAsync();
                }
            }
        }
    }

    public int RuntimeLowSeconds
    {
        get => _runtimeLowSeconds;
        set => SetField(ref _runtimeLowSeconds, value);
    }

    public int PollIntervalMs
    {
        get => _pollIntervalMs;
        set => SetField(ref _pollIntervalMs, value);
    }

    public string SelectedLanguageCode
    {
        get => _selectedLanguageCode;
        set
        {
            if (!SetField(ref _selectedLanguageCode, value))
            {
                return;
            }

            _configuration.Ui.Language = value;
            LocalizationManager.ApplyLanguage(Application.Current, value);
        }
    }

    public double BatteryWarningThresholdPercent
    {
        get => _batteryWarningThresholdPercent;
        set => SetField(ref _batteryWarningThresholdPercent, value);
    }

    public double BatteryCriticalThresholdPercent
    {
        get => _batteryCriticalThresholdPercent;
        set => SetField(ref _batteryCriticalThresholdPercent, value);
    }

    public double ComparableLoadTolerancePercent
    {
        get => _comparableLoadTolerancePercent;
        set => SetField(ref _comparableLoadTolerancePercent, value);
    }

    public IReadOnlyList<BatteryBaselineModeOption> BaselineModeOptions
    {
        get => _baselineModeOptions;
        private set => SetField(ref _baselineModeOptions, value);
    }

    public BatteryRuntimeBaselineKind SelectedBaselineKind
    {
        get => _selectedBaselineKind;
        set
        {
            if (SetField(ref _selectedBaselineKind, value))
            {
                OnPropertyChanged(nameof(BaselineInstructionText));
            }
        }
    }

    public double KnownHealthPercent
    {
        get => _knownHealthPercent;
        set => SetField(ref _knownHealthPercent, value);
    }

    public string KnownHealthSource
    {
        get => _knownHealthSource;
        set => SetField(ref _knownHealthSource, value);
    }

    public IReadOnlyList<VendorHealthCategoryOption> VendorHealthCategoryOptions
    {
        get => _vendorHealthCategoryOptions;
        private set => SetField(ref _vendorHealthCategoryOptions, value);
    }

    public VendorBatteryHealthCategory SelectedVendorHealthCategory
    {
        get => _selectedVendorHealthCategory;
        set => SetField(ref _selectedVendorHealthCategory, value);
    }

    public string BaselineInstructionText => L(SelectedBaselineKind switch
    {
        BatteryRuntimeBaselineKind.NewBattery => "BaselineInstructionNew",
        BatteryRuntimeBaselineKind.KnownHealthAnchor => "BaselineInstructionKnown",
        _ => "BaselineInstructionRelative",
    });

    public string ConnectionText { get => _connectionText; private set => SetField(ref _connectionText, value); }
    public string StateText { get => _stateText; private set => SetField(ref _stateText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string StatusAccent { get => _statusAccent; private set => SetField(ref _statusAccent, value); }
    public string LastUpdateText { get => _lastUpdateText; private set => SetField(ref _lastUpdateText, value); }
    public string LastError { get => _lastError; private set => SetField(ref _lastError, value); }
    public string SettingsStatus { get => _settingsStatus; private set => SetField(ref _settingsStatus, value); }
    public string TelemetryCountText { get => _telemetryCountText; private set => SetField(ref _telemetryCountText, value); }
    public IReadOnlyList<UpsTelemetryViewModel> TelemetryItems { get => _telemetryItems; private set => SetField(ref _telemetryItems, value); }

    public string Manufacturer { get => _manufacturer; private set => SetField(ref _manufacturer, value); }
    public string Product { get => _product; private set => SetField(ref _product, value); }
    public string SerialNumber { get => _serialNumber; private set => SetField(ref _serialNumber, value); }
    public string VidPid { get => _vidPid; private set => SetField(ref _vidPid, value); }
    public string Usage { get => _usage; private set => SetField(ref _usage, value); }
    public string DevicePath { get => _devicePath; private set => SetField(ref _devicePath, value); }
    public string InputReportLength { get => _inputReportLength; private set => SetField(ref _inputReportLength, value); }
    public string FeatureReportLength { get => _featureReportLength; private set => SetField(ref _featureReportLength, value); }
    public string PowerText { get => _powerText; private set => SetField(ref _powerText, value); }
    public string BatteryText { get => _batteryText; private set => SetField(ref _batteryText, value); }
    public string BatteryHealthText { get => _batteryHealthText; private set => SetField(ref _batteryHealthText, value); }
    public string BatteryHealthDetailText { get => _batteryHealthDetailText; private set => SetField(ref _batteryHealthDetailText, value); }
    public string BatteryHealthConfidenceText { get => _batteryHealthConfidenceText; private set => SetField(ref _batteryHealthConfidenceText, value); }
    public string BatteryHealthMethodText { get => _batteryHealthMethodText; private set => SetField(ref _batteryHealthMethodText, value); }
    public string BatteryHealthAccent { get => _batteryHealthAccent; private set => SetField(ref _batteryHealthAccent, value); }
    public string BatteryReplacementText { get => _batteryReplacementText; private set => SetField(ref _batteryReplacementText, value); }
    public string BatteryReplacementDetailText { get => _batteryReplacementDetailText; private set => SetField(ref _batteryReplacementDetailText, value); }
    public string BatteryReplacementAccent { get => _batteryReplacementAccent; private set => SetField(ref _batteryReplacementAccent, value); }
    public string BatteryHealthBaselineText { get => _batteryHealthBaselineText; private set => SetField(ref _batteryHealthBaselineText, value); }
    public string BatteryHealthDataQualityText { get => _batteryHealthDataQualityText; private set => SetField(ref _batteryHealthDataQualityText, value); }
    public string BatteryRelativeTrendText { get => _batteryRelativeTrendText; private set => SetField(ref _batteryRelativeTrendText, value); }
    public string BatteryHealthAnchorText { get => _batteryHealthAnchorText; private set => SetField(ref _batteryHealthAnchorText, value); }
    public IReadOnlyList<string> BatteryHealthReasons { get => _batteryHealthReasons; private set => SetField(ref _batteryHealthReasons, value); }
    public double BatteryProgress { get => _batteryProgress; private set => SetField(ref _batteryProgress, value); }
    public string RuntimeText { get => _runtimeText; private set => SetField(ref _runtimeText, value); }
    public string OverloadText { get => _overloadText; private set => SetField(ref _overloadText, value); }
    public string ChargingText { get => _chargingText; private set => SetField(ref _chargingText, value); }
    public string DischargingText { get => _dischargingText; private set => SetField(ref _dischargingText, value); }
    public string LowBatteryText { get => _lowBatteryText; private set => SetField(ref _lowBatteryText, value); }
    public string CriticalText { get => _criticalText; private set => SetField(ref _criticalText, value); }
    public string AcPresentText { get => _acPresentText; private set => SetField(ref _acPresentText, value); }
    public string VoltageText { get => _voltageText; private set => SetField(ref _voltageText, value); }
    public string CurrentText { get => _currentText; private set => SetField(ref _currentText, value); }
    public string FrequencyText { get => _frequencyText; private set => SetField(ref _frequencyText, value); }
    public string TemperatureText { get => _temperatureText; private set => SetField(ref _temperatureText, value); }
    public string RemainingTimeLimitText { get => _remainingTimeLimitText; private set => SetField(ref _remainingTimeLimitText, value); }
    public string DesignCapacityText { get => _designCapacityText; private set => SetField(ref _designCapacityText, value); }
    public string FullChargeCapacityText { get => _fullChargeCapacityText; private set => SetField(ref _fullChargeCapacityText, value); }
    public string BatteryVoltageText { get => _batteryVoltageText; private set => SetField(ref _batteryVoltageText, value); }
    public string NominalBatteryVoltageText { get => _nominalBatteryVoltageText; private set => SetField(ref _nominalBatteryVoltageText, value); }
    public string CycleCountText { get => _cycleCountText; private set => SetField(ref _cycleCountText, value); }
    public string NeedReplacementText { get => _needReplacementText; private set => SetField(ref _needReplacementText, value); }
    public string InputVoltageText { get => _inputVoltageText; private set => SetField(ref _inputVoltageText, value); }
    public string OutputVoltageText { get => _outputVoltageText; private set => SetField(ref _outputVoltageText, value); }
    public string PercentLoadText { get => _percentLoadText; private set => SetField(ref _percentLoadText, value); }
    public string ActivePowerText { get => _activePowerText; private set => SetField(ref _activePowerText, value); }
    public string ApparentPowerText { get => _apparentPowerText; private set => SetField(ref _apparentPowerText, value); }
    public string FullyChargedText { get => _fullyChargedText; private set => SetField(ref _fullyChargedText, value); }
    public string RechargeableText { get => _rechargeableText; private set => SetField(ref _rechargeableText, value); }
    public string RemainingTimeExpiredText { get => _remainingTimeExpiredText; private set => SetField(ref _remainingTimeExpiredText, value); }
    public string BoostText { get => _boostText; private set => SetField(ref _boostText, value); }
    public string AudibleAlarmText { get => _audibleAlarmText; private set => SetField(ref _audibleAlarmText, value); }
    public string SelfTestText { get => _selfTestText; private set => SetField(ref _selfTestText, value); }
    public string TransferRangeText { get => _transferRangeText; private set => SetField(ref _transferRangeText, value); }
    public string RatedPowerText { get => _ratedPowerText; private set => SetField(ref _ratedPowerText, value); }
    public string BatteryChemistryText { get => _batteryChemistryText; private set => SetField(ref _batteryChemistryText, value); }
    public string OemInformationText { get => _oemInformationText; private set => SetField(ref _oemInformationText, value); }
    public string InputVoltageSummaryText { get => _inputVoltageSummaryText; private set => SetField(ref _inputVoltageSummaryText, value); }
    public string InputOutputText { get => _inputOutputText; private set => SetField(ref _inputOutputText, value); }
    public string ReportBytesText { get => _reportBytesText; private set => SetField(ref _reportBytesText, value); }

    public void Start() => _engine.Start();

    public void NotifyDeviceChange() => _engine.NotifyDeviceChange();

    public void SetStartupError(string message) => LastError = message;

    public async ValueTask DisposeAsync()
    {
        _historyRefreshCancellation?.Cancel();
        _historyRefreshCancellation?.Dispose();
        _engine.SnapshotUpdated -= OnSnapshotUpdated;
        _engine.EventDetected -= OnEventDetected;
        _engine.MonitorError -= OnMonitorError;
        LocalizationManager.LanguageChanged -= OnLanguageChanged;
        if (_historyStore is not null)
        {
            _historyStore.StorageError -= OnHistoryStorageError;
        }

        await _engine.DisposeAsync();
    }

    private void OnSnapshotUpdated(UpsSnapshot snapshot) =>
        _ = _dispatcher.InvokeAsync(() => ApplySnapshot(snapshot));

    private void OnEventDetected(UpsEvent upsEvent) =>
        _ = _dispatcher.InvokeAsync(() =>
        {
            var eventVm = new UpsEventViewModel(upsEvent);
            Events.Insert(0, eventVm);
            while (Events.Count > 500)
            {
                Events.RemoveAt(Events.Count - 1);
            }

            OnPropertyChanged(nameof(LogCountText));
            NotificationRequested?.Invoke(eventVm.Type, eventVm.Message, upsEvent.Severity);

            if (EnableSoundAlerts)
            {
                try
                {
                    if (upsEvent.Severity == UpsEventSeverity.Critical)
                    {
                        System.Media.SystemSounds.Hand.Play();
                    }
                    else if (upsEvent.Severity == UpsEventSeverity.Warning)
                    {
                        System.Media.SystemSounds.Exclamation.Play();
                    }
                }
                catch { }
            }

            // Webhook 送信
            if (_configuration.Webhook.Enabled && _lastSnapshot is { } snapshot)
            {
                var shouldSend = upsEvent.Type switch
                {
                    UpsEventType.PowerLost => _configuration.Webhook.NotifyOnPowerLost,
                    UpsEventType.PowerRestored => _configuration.Webhook.NotifyOnPowerRestored,
                    UpsEventType.BatteryLow or UpsEventType.BatteryCritical => _configuration.Webhook.NotifyOnBatteryLow,
                    UpsEventType.OverloadDetected or UpsEventType.HighLoadWarning => _configuration.Webhook.NotifyOnHighLoad,
                    _ => false,
                };

                if (shouldSend)
                {
                    _ = WebhookNotifier.SendNotificationAsync(_configuration.Webhook.Url, upsEvent, snapshot);
                }
            }

            // 外部コマンド実行
            if (_configuration.ExternalCommand.Enabled && _lastSnapshot is { } cmdSnapshot)
            {
                var cmd = upsEvent.Type switch
                {
                    UpsEventType.PowerLost => _configuration.ExternalCommand.CommandOnPowerLost,
                    UpsEventType.PowerRestored => _configuration.ExternalCommand.CommandOnPowerRestored,
                    UpsEventType.BatteryLow or UpsEventType.BatteryCritical => _configuration.ExternalCommand.CommandOnBatteryLow,
                    UpsEventType.HighLoadWarning => _configuration.ExternalCommand.CommandOnHighLoad,
                    UpsEventType.VoltageAbnormal => _configuration.ExternalCommand.CommandOnVoltageAbnormal,
                    _ => string.Empty,
                };

                if (!string.IsNullOrWhiteSpace(cmd))
                {
                    _ = CommandRunner.RunCommandAsync(cmd, upsEvent, cmdSnapshot);
                }
            }
        });

    private void OnMonitorError(Exception exception) =>
        _ = _dispatcher.InvokeAsync(() => LastError = exception.Message);

    private void OnHistoryStorageError(Exception exception) =>
        _ = _dispatcher.InvokeAsync(() => LastError = LocalizationManager.Format("HistoryStorageErrorFormat", exception.Message));

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _selectedLanguageCode = LocalizationManager.CurrentLanguageCode;
        OnPropertyChanged(nameof(SelectedLanguageCode));
        RefreshThemeOptions();
        RefreshBaselineModeOptions();
        RefreshVendorHealthCategoryOptions();
        RefreshHistoryRangeOptions();
        RefreshAnalyticsOptions();
        RefreshLogSeverityOptions();
        OnPropertyChanged(nameof(BaselineInstructionText));
        OnPropertyChanged(nameof(LogCountText));
        _telemetryItemsList.Clear();
        TelemetryItems = [];
        SettingsStatus = string.Empty;

        if (_lastSnapshot is { } snapshot)
        {
            ApplySnapshot(snapshot);
        }
        else
        {
            ApplyWaitingState();
        }

        foreach (var upsEvent in Events)
        {
            upsEvent.RefreshLanguage();
        }

        if (_lastSnapshot is not null && IsHistoryRefreshTarget(_selectedNavigationIndex) && _isWindowVisible)
        {
            _ = RefreshHistoryAsync();
        }
        else if (_lastSnapshot is not null && IsAnalyticsRefreshTarget(_selectedNavigationIndex) && _isWindowVisible)
        {
            _ = RefreshAnalyticsAsync();
        }
    }

    private void RefreshThemeOptions()
    {
        ThemeOptions =
        [
            new("system", L("ThemeSystem")),
            new("dark", L("ThemeDark")),
            new("light", L("ThemeLight")),
        ];
    }

    private void ApplyWaitingState()
    {
        ConnectionText = L("SearchingConnection");
        StateText = L("StateUnknown");
        StatusMessage = L("SearchingStatus");
        LastUpdateText = L("NeverUpdated");
        Product = L("NoUpsDetected");
        TelemetryCountText = LocalizationManager.Format("TelemetryCountFormat", 0, 0, 0, 0);
        HistoryStatus = _historyStore is null ? L("HistoryUnavailable") : L("HistoryWaitingForUps");
        RaiseSnapshotProperties();
    }

    private void ApplySnapshot(UpsSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        var telemetry = UpsTelemetryValidator.Normalize(snapshot);
        _lastTelemetry = telemetry;
        var healthProfile = FindHealthProfile(snapshot.Device);
        var health = BatteryHealthCalculator.Calculate(telemetry, healthProfile, CreateHealthOptions());

        var state = UpsPowerStateEvaluator.Evaluate(snapshot);
        StateText = state switch
        {
            UpsPowerState.Online => L("StateOnline"),
            UpsPowerState.OnBattery => L("StateOnBattery"),
            UpsPowerState.LowBattery => L("StateLowBattery"),
            UpsPowerState.Critical => L("StateCritical"),
            _ => L("StateUnknown"),
        };
        Product = snapshot.Device?.DisplayName ?? L("NoUpsDetected");
        BatteryText = FormatValidatedPercent(telemetry.BatteryChargePercent);
        RuntimeText = FormatValidatedDuration(telemetry.RuntimeRemaining);

        TooltipUpdated?.Invoke($"{Product}\n{StateText} - {BatteryText} ({RuntimeText})");
        DynamicTrayIconUpdated?.Invoke(state, telemetry.BatteryChargePercent.Value, snapshot.AcPresent ?? false);
        RecalculateSimulation();

        if (_historyStore is not null && snapshot.Device is { } historyDevice)
        {
            _ = _historyStore.RecordBatteryHealthAsync(new BatteryHealthObservation
            {
                DeviceId = UpsDeviceIdentity.Create(historyDevice),
                Timestamp = snapshot.Timestamp,
                HealthPercent = health.HealthPercent,
                RelativePerformancePercent = health.RelativePerformancePercent,
                Status = health.Status,
                Method = health.PrimaryMethod,
                Confidence = health.Confidence,
                AnchorSource = health.AnchorSource,
                VendorCategory = health.VendorHealthCategory,
                ReplacementStatus = health.Replacement.Status,
            });
        }

        if (!_isWindowVisible)
        {
            _pendingSnapshot = snapshot;
            return;
        }

        ApplySnapshotCore(snapshot, telemetry, healthProfile, health);

        if (_historyStore is not null && IsHistoryRefreshTarget(_selectedNavigationIndex) && snapshot.Timestamp - _lastHistoryRefresh >= TimeSpan.FromSeconds(10))
        {
            _ = RefreshHistoryAsync();
        }
    }

    private void ApplySnapshotCore(UpsSnapshot snapshot)
    {
        var telemetry = _lastTelemetry ?? UpsTelemetryValidator.Normalize(snapshot);
        var healthProfile = FindHealthProfile(snapshot.Device);
        var health = BatteryHealthCalculator.Calculate(telemetry, healthProfile, CreateHealthOptions());
        ApplySnapshotCore(snapshot, telemetry, healthProfile, health);
    }

    private void ApplySnapshotCore(
        UpsSnapshot snapshot,
        UpsTelemetry telemetry,
        BatteryHealthProfile? healthProfile,
        BatteryHealthResult health)
    {
        InitializeBaselineEditor(healthProfile);
        LastUpdateText = LocalizationManager.Format("LastUpdateFormat", snapshot.Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        ConnectionText = snapshot.IsConnected ? L("Connected") : L("Disconnected");

        var state = UpsPowerStateEvaluator.Evaluate(snapshot);
        (StatusMessage, StatusAccent) = state switch
        {
            UpsPowerState.Online => (L("StatusOnline"), "#22C55E"),
            UpsPowerState.OnBattery => (L("StatusOnBattery"), "#F59E0B"),
            UpsPowerState.LowBattery => (L("StatusLowBattery"), "#F97316"),
            UpsPowerState.Critical => (L("StatusCritical"), "#EF4444"),
            _ when snapshot.IsConnected => (L("StatusConnectedUnknown"), "#64748B"),
            _ => (L("StatusNoUps"), "#64748B"),
        };

        if (snapshot.Device is { } device)
        {
            Manufacturer = TextOrNa(device.Manufacturer);
            Product = device.DisplayName;
            SerialNumber = TextOrNa(device.SerialNumber);
            VidPid = $"{device.VendorId:X4} / {device.ProductId:X4}";
            Usage = $"0x{device.UsagePage:X2} / 0x{device.Usage:X2}";
            DevicePath = device.DevicePath;
            InputReportLength = device.InputReportByteLength.ToString(CultureInfo.InvariantCulture);
            FeatureReportLength = device.FeatureReportByteLength.ToString(CultureInfo.InvariantCulture);
        }
        else
        {
            Manufacturer = "N/A";
            Product = L("NoUpsDetected");
            SerialNumber = VidPid = Usage = DevicePath = InputReportLength = FeatureReportLength = "N/A";
        }

        PowerText = snapshot.AcPresent switch { true => L("PowerAc"), false => L("PowerBattery"), _ => "N/A" };
        BatteryHealthText = health.HealthPercent is { } healthPercent ? $"{healthPercent:0.#}%" : L("HealthUnknown");
        BatteryHealthDetailText = LocalizeHealthDetail(health);
        BatteryHealthConfidenceText = LocalizeHealthConfidence(health.Confidence);
        BatteryHealthMethodText = LocalizeHealthMethod(health.PrimaryMethod);
        BatteryHealthAccent = HealthAccent(health.Status);
        BatteryReplacementText = LocalizeReplacementStatus(health.Replacement.Status);
        BatteryReplacementDetailText = LocalizeReplacementDetail(health);
        BatteryReplacementAccent = ReplacementAccent(health.Replacement.Status);
        BatteryHealthReasons = health.Reasons.Select(LocalizeHealthReason).Distinct().ToArray();
        BatteryHealthBaselineText = FormatBaselineSummary(healthProfile);
        BatteryRelativeTrendText = health.RelativePerformancePercent is { } relative
            ? $"{relative:0.#}%"
            : "N/A";
        BatteryHealthAnchorText = health.BaselineKind == BatteryRuntimeBaselineKind.NewBattery
            ? L("HealthNewBatteryAnchor")
            : health.AnchorHealthPercent is { } anchor
            ? LocalizationManager.Format(
                "HealthAnchorFormat",
                anchor,
                string.IsNullOrWhiteSpace(health.AnchorSource) ? L("Unknown") : health.AnchorSource)
            : "N/A";
        BatteryHealthDataQualityText = telemetry.Issues.Count == 0
            ? L("TelemetryQualityValid")
            : LocalizationManager.Format("TelemetryQualityIssuesFormat", telemetry.Issues.Count);
        BatteryProgress = telemetry.BatteryChargePercent.IsValid
            ? telemetry.BatteryChargePercent.Value ?? 0
            : 0;
        OverloadText = FormatBoolean(snapshot.Overload);
        ChargingText = FormatBoolean(snapshot.Charging);
        DischargingText = FormatBoolean(snapshot.Discharging);
        LowBatteryText = FormatBoolean(snapshot.LowBattery);
        CriticalText = FormatBoolean(snapshot.ShutdownImminent);
        AcPresentText = FormatBoolean(snapshot.AcPresent);
        VoltageText = FormatNumber(snapshot.Voltage, "V");
        CurrentText = FormatNumber(snapshot.Current, "A");
        FrequencyText = FormatNumber(snapshot.Frequency, "Hz");
        TemperatureText = FormatNumber(snapshot.Temperature, "°C");
        RemainingTimeLimitText = FormatDuration(snapshot.RemainingTimeLimit);
        DesignCapacityText = FormatNumber(snapshot.DesignCapacity, null);
        FullChargeCapacityText = FormatNumber(snapshot.FullChargeCapacity, null);
        BatteryVoltageText = FormatValidatedNumber(telemetry.BatteryVoltage, "V");
        NominalBatteryVoltageText = FormatValidatedNumber(telemetry.NominalBatteryVoltage, "V");
        CycleCountText = FormatValidatedNumber(telemetry.CycleCount, null);
        NeedReplacementText = FormatValidatedBoolean(telemetry.NeedReplacement);
        InputVoltageText = FormatNumber(snapshot.InputVoltage, "V");
        OutputVoltageText = FormatNumber(snapshot.OutputVoltage, "V");
        PercentLoadText = FormatValidatedNumber(telemetry.LoadPercent, "%");
        ActivePowerText = FormatValidatedNumber(telemetry.ActivePowerWatts, "W");
        ApparentPowerText = FormatNumber(snapshot.ApparentPower, "VA");
        FullyChargedText = FormatBoolean(snapshot.FullyCharged);
        RechargeableText = FormatBoolean(snapshot.Rechargeable);
        RemainingTimeExpiredText = FormatBoolean(snapshot.RemainingTimeLimitExpired);
        BoostText = FormatBoolean(snapshot.Boost);
        if (snapshot.Boost == true)
        {
            AvrBoostText = L("AvrBoostActive");
            AvrBoostAccent = "#F59E0B";
        }
        else if (snapshot.AcPresent == true)
        {
            AvrBoostText = L("AvrNormal");
            AvrBoostAccent = "#22C55E";
        }
        else
        {
            AvrBoostText = "-";
            AvrBoostAccent = "#94A3B8";
        }

        if (snapshot.ConfigActivePower is { } ratedW && snapshot.ActivePower is { } curW)
        {
            var remainW = Math.Max(0, ratedW - curW);
            if (snapshot.ConfigApparentPower is { } ratedVa && snapshot.ApparentPower is { } curVa)
            {
                var remainVa = Math.Max(0, ratedVa - curVa);
                PowerMarginText = $"残り {remainW:0.#} W / {remainVa:0.#} VA";
            }
            else
            {
                PowerMarginText = $"残り {remainW:0.#} W";
            }
        }
        else
        {
            PowerMarginText = "N/A";
        }

        if (snapshot.InputVoltage is { } inV && snapshot.LowVoltageTransfer is { } lowV)
        {
            var margin = inV - lowV;
            VoltageMarginText = margin >= 0 ? $"下限({lowV:0.#}V)まで +{margin:0.#} V" : $"下限({lowV:0.#}V)超過 {margin:0.#} V";
        }
        else
        {
            VoltageMarginText = "N/A";
        }

        if (snapshot.BatteryVoltage is { } batV)
        {
            var nominalV = snapshot.NominalBatteryVoltage ?? 24.0;
            var cellCount = (int)Math.Max(1, Math.Round(nominalV / 2.0));
            var cellV = batV / cellCount;
            CellVoltageText = $"{cellV:0.00} V/cell ({cellCount}セル)";
        }
        else
        {
            CellVoltageText = "N/A";
        }

        AudibleAlarmText = LocalizeTextOrNa(snapshot.AudibleAlarmState);
        SelfTestText = LocalizeTextOrNa(snapshot.SelfTestState);
        TransferRangeText = snapshot.LowVoltageTransfer is { } low && snapshot.HighVoltageTransfer is { } high
            ? $"{low:0.###} – {high:0.###} V"
            : "N/A";
        RatedPowerText = snapshot.ConfigActivePower is { } watts && snapshot.ConfigApparentPower is { } voltAmperes
            ? $"{watts:0.###} W / {voltAmperes:0.###} VA"
            : "N/A";
        BatteryChemistryText = TelemetryText(snapshot.Telemetry, 0x85, 0x89);
        OemInformationText = TelemetryText(snapshot.Telemetry, 0x85, 0x8F);
        UpdateTelemetryItems(snapshot.Telemetry);
        var readableCount = snapshot.Telemetry.Count(item => item.IsReadable);
        var valueCount = snapshot.Telemetry.Count(item => item.HasValue);
        var vendorCount = snapshot.Telemetry.Count(item => item.IsVendorDefined);
        TelemetryCountText = LocalizationManager.Format(
            "TelemetryCountFormat",
            snapshot.Telemetry.Count,
            readableCount,
            valueCount,
            vendorCount);
        InputVoltageSummaryText = LocalizationManager.Format("InputSummaryFormat", InputVoltageText);
        InputOutputText = $"{InputVoltageText} / {OutputVoltageText}";
        ReportBytesText = LocalizationManager.Format("ReportBytesFormat", InputReportLength, FeatureReportLength);
    }

    private void UpdateTelemetryItems(IReadOnlyList<UpsTelemetryItem> items)
    {
        var count = items.Count;
        if (_telemetryItemsList.Count != count)
        {
            _telemetryItemsList = items.Select(item => new UpsTelemetryViewModel(item)).ToList();
            TelemetryItems = _telemetryItemsList;
            return;
        }

        for (var i = 0; i < count; i++)
        {
            var item = items[i];
            var vm = _telemetryItemsList[i];
            if (vm.Key != item.Key)
            {
                _telemetryItemsList = items.Select(x => new UpsTelemetryViewModel(x)).ToList();
                TelemetryItems = _telemetryItemsList;
                return;
            }

            vm.Update(item);
        }
    }

    private async Task RecordRuntimeBaselineAsync()
    {
        if (_lastSnapshot?.Device is not { } device || _lastTelemetry is not { IsConnected: true } telemetry)
        {
            SettingsStatus = L("BaselineRequiresUps");
            return;
        }

        var fullyCharged = telemetry.FullyCharged.Value is true
            || (telemetry.BatteryChargePercent.IsValid && telemetry.BatteryChargePercent.Value >= 95);
        if (!fullyCharged)
        {
            SettingsStatus = L("BaselineRequiresFullCharge");
            return;
        }

        if (!telemetry.LoadPercent.IsValid
            || telemetry.LoadPercent.Value is not { } load
            || load is <= 0 or > 100
            || !telemetry.RuntimeRemaining.IsValid
            || telemetry.RuntimeRemaining.Value is not { } runtime
            || runtime <= TimeSpan.Zero)
        {
            SettingsStatus = L("BaselineRequiresTelemetry");
            return;
        }

        if (SelectedBaselineKind == BatteryRuntimeBaselineKind.KnownHealthAnchor
            && (KnownHealthPercent is <= 0 or > 100 || !double.IsFinite(KnownHealthPercent)))
        {
            SettingsStatus = L("KnownHealthValidation");
            return;
        }

        var profile = FindHealthProfile(device);
        if (profile is null)
        {
            profile = new BatteryHealthProfile { DeviceId = DeviceId(device) };
            _configuration.BatteryHealth.Profiles.Add(profile);
        }

        var recordedAt = DateTimeOffset.Now;
        profile.RuntimeBaselineKind = SelectedBaselineKind;
        profile.BaselineRecordedAt = recordedAt;
        switch (SelectedBaselineKind)
        {
            case BatteryRuntimeBaselineKind.NewBattery:
                profile.AnchorHealthPercent = 100;
                profile.AnchorSource = null;
                profile.VendorHealthCategory = VendorBatteryHealthCategory.Unknown;
                profile.BatteryInstalledAt ??= recordedAt;
                break;

            case BatteryRuntimeBaselineKind.KnownHealthAnchor:
                profile.AnchorHealthPercent = KnownHealthPercent;
                profile.AnchorSource = string.IsNullOrWhiteSpace(KnownHealthSource)
                    ? "CyberPower BHI"
                    : KnownHealthSource.Trim();
                profile.VendorHealthCategory = SelectedVendorHealthCategory;
                break;

            default:
                profile.AnchorHealthPercent = null;
                profile.AnchorSource = null;
                profile.VendorHealthCategory = VendorBatteryHealthCategory.Unknown;
                break;
        }

        profile.RuntimeBaselines.RemoveAll(item => Math.Abs(item.LoadPercent - load) < 1);
        profile.RuntimeBaselines.Add(new BatteryRuntimeBaselinePoint
        {
            LoadPercent = load,
            Runtime = runtime,
            MeasuredAt = recordedAt,
        });

        await _configurationStore.SaveAsync(_configuration);
        SettingsStatus = LocalizationManager.Format(
            "BaselineRecordedFormat",
            load,
            FormatDuration(runtime),
            LocalizeBaselineKind(SelectedBaselineKind));
        ApplySnapshot(_lastSnapshot);
    }

    private async Task ClearRuntimeBaselineAsync()
    {
        if (_lastSnapshot?.Device is not { } device || FindHealthProfile(device) is not { } profile)
        {
            SettingsStatus = L("BaselineNothingToClear");
            return;
        }

        profile.RuntimeBaselines.Clear();
        profile.RuntimeBaselineKind = BatteryRuntimeBaselineKind.Unspecified;
        profile.AnchorHealthPercent = null;
        profile.AnchorSource = null;
        profile.VendorHealthCategory = VendorBatteryHealthCategory.Unknown;
        profile.BaselineRecordedAt = null;
        await _configurationStore.SaveAsync(_configuration);
        SettingsStatus = L("BaselineCleared");
        ApplySnapshot(_lastSnapshot);
    }

    private void RecalculateSimulation()
    {
        var batPercent = _lastTelemetry?.BatteryChargePercent.Value ?? 100.0;
        var soh = (_lastSnapshot is not null) ? FindHealthProfile(_lastSnapshot.Device)?.AnchorHealthPercent ?? 100.0 : 100.0;
        var nomVolt = _lastTelemetry?.NominalBatteryVoltage.Value ?? 24.0;
        var ratedWatts = _lastSnapshot?.ConfigActivePower ?? 780.0;
        var currentRuntime = _lastTelemetry?.RuntimeRemaining.Value;
        var currentLoad = _lastTelemetry?.ActivePowerWatts.Value;

        var simulatedTime = RuntimeEstimator.EstimateRuntime(
            LoadSimulationTargetWatts,
            batPercent,
            soh,
            nomVolt,
            batteryCapacityAh: null,
            baselineRuntimeAtCurrentLoad: currentRuntime,
            currentActiveLoadWatts: currentLoad,
            ratedActivePowerWatts: ratedWatts);

        SimulatedRuntimeText = $"{simulatedTime.TotalMinutes:0.#} {L("MinutesUnit")}";

        StandardLoadEstimates = RuntimeEstimator.GenerateStandardLoadEstimates(
            batPercent,
            soh,
            nomVolt,
            ratedWatts,
            currentRuntime,
            currentLoad);
    }

    private async Task SaveSettingsAsync()
    {
        if (PollIntervalMs is < 250 or > 60_000)
        {
            SettingsStatus = L("PollIntervalValidation");
            return;
        }

        if (RuntimeLowSeconds is < 0 or > 86_400)
        {
            SettingsStatus = "Invalid runtime low threshold (0-86400).";
            return;
        }

        if (!AreHealthSettingsValid())
        {
            SettingsStatus = L("BatteryHealthThresholdValidation");
            return;
        }

        _configuration.Monitoring.PollIntervalMs = PollIntervalMs;
        _configuration.Monitoring.RuntimeLowSeconds = RuntimeLowSeconds;
        _configuration.Monitoring.ElectricityRatePerKwh = ElectricityRatePerKwh;
        _configuration.Ui.Language = SelectedLanguageCode;
        _configuration.Ui.Theme = SelectedTheme;
        _configuration.Ui.MinimizeToTray = MinimizeToTray;
        _configuration.Ui.CloseToTray = CloseToTray;
        _configuration.Ui.StartMinimized = StartMinimized;
        _configuration.Ui.EnableNotifications = EnableNotifications;
        _configuration.Ui.RunOnStartup = RunOnStartup;
        _configuration.BatteryHealth.WarningThresholdPercent = BatteryWarningThresholdPercent;
        _configuration.BatteryHealth.CriticalThresholdPercent = BatteryCriticalThresholdPercent;
        _configuration.BatteryHealth.ComparableLoadTolerancePercent = ComparableLoadTolerancePercent;
        _configuration.Alerts.EnableSoundAlerts = EnableSoundAlerts;
        _configuration.Alerts.HighLoadWarningPercent = HighLoadWarningPercent;
        _configuration.Alerts.LowVoltageWarningThreshold = LowVoltageWarningThreshold;
        _configuration.Alerts.HighVoltageWarningThreshold = HighVoltageWarningThreshold;
        _configuration.Webhook.Enabled = WebhookEnabled;
        _configuration.Webhook.Url = WebhookUrl;
        _configuration.Webhook.NotifyOnPowerLost = WebhookNotifyOnPowerLost;
        _configuration.Webhook.NotifyOnPowerRestored = WebhookNotifyOnPowerRestored;
        _configuration.Webhook.NotifyOnBatteryLow = WebhookNotifyOnBatteryLow;
        _configuration.Webhook.NotifyOnHighLoad = WebhookNotifyOnHighLoad;
        _configuration.ExternalCommand.Enabled = ExternalCommandEnabled;
        _configuration.ExternalCommand.CommandOnPowerLost = CommandOnPowerLost;
        _configuration.ExternalCommand.CommandOnPowerRestored = CommandOnPowerRestored;
        _configuration.ExternalCommand.CommandOnBatteryLow = CommandOnBatteryLow;
        _configuration.ExternalCommand.CommandOnHighLoad = CommandOnHighLoad;
        _configuration.ExternalCommand.CommandOnVoltageAbnormal = CommandOnVoltageAbnormal;

        await _configurationStore.SaveAsync(_configuration);
        _engine.SetPollInterval(PollIntervalMs);
        _engine.SetRuntimeLowThreshold(TimeSpan.FromSeconds(RuntimeLowSeconds));
        _engine.SetAlertThresholds(_configuration.Alerts.ToAlertThresholds());
        SettingsStatus = LocalizationManager.Format("SettingsSavedFormat", DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        if (_lastSnapshot is { } snapshot)
        {
            ApplySnapshot(snapshot);
        }
    }

    private async Task ExportTelemetryCsvAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"ups-telemetry-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExt = ".csv",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var range = SelectedHistoryRange?.Duration ?? TimeSpan.FromDays(7);
            var from = DateTimeOffset.Now - range;
            var to = DateTimeOffset.Now;
            await TelemetryExporter.ExportTelemetryCsvAsync(TelemetryDatabaseFile, dialog.FileName, from, to);
            SettingsStatus = LocalizationManager.Format("ExportSuccess", System.IO.Path.GetFileName(dialog.FileName));
        }
        catch (Exception ex)
        {
            SettingsStatus = LocalizationManager.Format("ExportFailed", ex.Message);
            LastError = ex.Message;
        }
    }

    private async Task ExportTelemetryJsonAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            FileName = $"ups-telemetry-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            DefaultExt = ".json",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var range = SelectedHistoryRange?.Duration ?? TimeSpan.FromDays(7);
            var from = DateTimeOffset.Now - range;
            var to = DateTimeOffset.Now;
            await TelemetryExporter.ExportTelemetryJsonAsync(TelemetryDatabaseFile, dialog.FileName, from, to);
            SettingsStatus = LocalizationManager.Format("ExportSuccess", System.IO.Path.GetFileName(dialog.FileName));
        }
        catch (Exception ex)
        {
            SettingsStatus = LocalizationManager.Format("ExportFailed", ex.Message);
            LastError = ex.Message;
        }
    }

    private async Task ExportEventsCsvAsync()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"ups-events-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            DefaultExt = ".csv",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var range = SelectedHistoryRange?.Duration ?? TimeSpan.FromDays(30);
            var from = DateTimeOffset.Now - range;
            var to = DateTimeOffset.Now;
            await TelemetryExporter.ExportEventsCsvAsync(TelemetryDatabaseFile, dialog.FileName, from, to);
            SettingsStatus = LocalizationManager.Format("ExportSuccess", System.IO.Path.GetFileName(dialog.FileName));
        }
        catch (Exception ex)
        {
            SettingsStatus = LocalizationManager.Format("ExportFailed", ex.Message);
            LastError = ex.Message;
        }
    }

    private async Task RefreshHistoryAsync()
    {
        if (_historyStore is null)
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
        previousCancellation?.Dispose();

        IsHistoryLoading = true;
        HistoryStatus = L("HistoryLoading");
        var to = DateTimeOffset.Now;
        var from = to - range.Duration;
        _lastHistoryRefresh = to;

        try
        {
            var history = await _historyStore.QueryHistoryAsync(
                UpsDeviceIdentity.Create(device),
                from,
                to,
                Enum.GetValues<TelemetryMetric>(),
                cancellationToken: refreshCancellation.Token);
            var statistics = await _historyStore.GetStatisticsAsync(refreshCancellation.Token);
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

            // 日別集計レポートの取得
            var dailyReports = await _historyStore.QueryDailyEnergyReportsAsync(
                UpsDeviceIdentity.Create(device),
                7,
                _configuration.Monitoring.ElectricityRatePerKwh,
                refreshCancellation.Token);

            if (!DailyEnergyReports.SequenceEqual(dailyReports))
            {
                DailyEnergyReports.Clear();
                foreach (var item in dailyReports)
                {
                    DailyEnergyReports.Add(item);
                }
            }

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

            var energyReports = await _historyStore.QueryEnergyReportsAsync(
                UpsDeviceIdentity.Create(device),
                energyFrom,
                to,
                EnergyReportGranularity,
                _configuration.Monitoring.ElectricityRatePerKwh,
                refreshCancellation.Token);

            if (!EnergyReports.SequenceEqual(energyReports))
            {
                EnergyReports.Clear();
                foreach (var item in energyReports)
                {
                    EnergyReports.Add(item);
                }
            }

            // 停電・電圧サマリーの取得
            TroubleSummary = await _historyStore.QueryPowerTroubleSummaryAsync(
                UpsDeviceIdentity.Create(device),
                from,
                to,
                _configuration.Alerts.LowVoltageWarningThreshold,
                _configuration.Alerts.HighVoltageWarningThreshold,
                refreshCancellation.Token);

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
            HistoryStatus = LocalizationManager.Format("HistoryLoadErrorFormat", exception.Message);
            LastError = HistoryStatus;
        }
        finally
        {
            if (ReferenceEquals(_historyRefreshCancellation, refreshCancellation))
            {
                IsHistoryLoading = false;
            }
        }

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
        var healthPoints = history.BatteryHealth
            .Where(item => item.HealthPercent.HasValue)
            .Select(item => Point(item.Timestamp, item.HealthPercent!.Value))
            .ToArray();
        var relativePoints = history.BatteryHealth
            .Where(item => item.RelativePerformancePercent.HasValue)
            .Select(item => Point(item.Timestamp, item.RelativePerformancePercent!.Value))
            .ToArray();
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
            PeriodCostSummaryText = $"約 {cost:N0} {L("CurrencyUnit")} ({_configuration.Monitoring.ElectricityRatePerKwh:0.#}{L("CurrencyUnit")}/kWh)";
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

    private void SetCommandError(Exception exception)
    {
        SettingsStatus = LocalizationManager.Format("SettingsSaveErrorFormat", exception.Message);
        LastError = exception.Message;
    }

    private void RaiseSnapshotProperties()
    {
        var names = new[]
        {
            nameof(Manufacturer), nameof(Product), nameof(SerialNumber), nameof(VidPid), nameof(Usage),
            nameof(DevicePath), nameof(InputReportLength), nameof(FeatureReportLength), nameof(PowerText),
            nameof(BatteryText), nameof(BatteryHealthText), nameof(BatteryHealthDetailText),
            nameof(BatteryHealthConfidenceText), nameof(BatteryHealthMethodText), nameof(BatteryHealthAccent),
            nameof(BatteryReplacementText), nameof(BatteryReplacementDetailText), nameof(BatteryReplacementAccent),
            nameof(BatteryHealthBaselineText), nameof(BatteryHealthDataQualityText), nameof(BatteryHealthReasons),
            nameof(BatteryRelativeTrendText), nameof(BatteryHealthAnchorText),
            nameof(BatteryProgress), nameof(RuntimeText), nameof(OverloadText),
            nameof(ChargingText), nameof(DischargingText), nameof(LowBatteryText), nameof(CriticalText),
            nameof(AcPresentText), nameof(VoltageText), nameof(CurrentText), nameof(FrequencyText),
            nameof(TemperatureText), nameof(RemainingTimeLimitText), nameof(DesignCapacityText),
            nameof(FullChargeCapacityText), nameof(BatteryVoltageText), nameof(NominalBatteryVoltageText),
            nameof(CycleCountText), nameof(NeedReplacementText), nameof(InputVoltageText),
            nameof(OutputVoltageText), nameof(PercentLoadText), nameof(ActivePowerText),
            nameof(ApparentPowerText), nameof(FullyChargedText), nameof(RechargeableText),
            nameof(RemainingTimeExpiredText), nameof(BoostText), nameof(AudibleAlarmText),
            nameof(SelfTestText), nameof(TransferRangeText), nameof(RatedPowerText),
            nameof(BatteryChemistryText), nameof(OemInformationText), nameof(InputVoltageSummaryText),
            nameof(InputOutputText), nameof(ReportBytesText),
            nameof(FrequencyEmptyText), nameof(TemperatureEmptyText),
            nameof(PowerMarginText), nameof(VoltageMarginText), nameof(AvrBoostText), nameof(AvrBoostAccent),
            nameof(CellVoltageText),
        };

        foreach (var name in names)
        {
            OnPropertyChanged(name);
        }
    }

    private static string L(string key) => LocalizationManager.Get(key);

    private static string TextOrNa(string? value) => string.IsNullOrWhiteSpace(value) ? "N/A" : value;

    private static string LocalizeTextOrNa(string? value) => string.IsNullOrWhiteSpace(value)
        ? "N/A"
        : LocalizationManager.LocalizeTelemetryValue(value);

    private static string TelemetryText(IReadOnlyList<UpsTelemetryItem> items, ushort page, ushort usage) =>
        items.FirstOrDefault(item => item.UsagePage == page && item.Usage == usage && item.HasValue) is { } item
            ? LocalizationManager.LocalizeTelemetryValue(item.DisplayValue)
            : "N/A";

    private static string FormatBoolean(bool? value) => value switch
    {
        true => L("Yes"),
        false => L("No"),
        _ => "N/A",
    };

    private static string FormatNumber(double? value, string? unit) => value is { } number
        ? string.IsNullOrWhiteSpace(unit) ? $"{number:0.###}" : $"{number:0.###} {unit}"
        : "N/A";

    private static string FormatValidatedPercent(ValidatedTelemetryValue<double> value) =>
        value.IsValid && value.Value is { } number ? $"{number:0.#}%" : "N/A";

    private static string FormatValidatedNumber(ValidatedTelemetryValue<double> value, string? unit) =>
        value.IsValid ? FormatNumber(value.Value, unit) : "N/A";

    private static string FormatValidatedDuration(ValidatedTelemetryValue<TimeSpan> value) =>
        value.IsValid ? FormatDuration(value.Value) : "N/A";

    private static string FormatValidatedBoolean(ValidatedTelemetryValue<bool> value) =>
        value.IsValid ? FormatBoolean(value.Value) : "N/A";

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

    private BatteryHealthProfile? FindHealthProfile(UpsDeviceInfo? device)
    {
        if (device is null)
        {
            return null;
        }

        var id = DeviceId(device);
        return _configuration.BatteryHealth.Profiles.FirstOrDefault(
            item => string.Equals(item.DeviceId, id, StringComparison.OrdinalIgnoreCase));
    }

    private static string DeviceId(UpsDeviceInfo device) =>
        UpsDeviceIdentity.Create(device);

    private BatteryHealthOptions CreateHealthOptions() => AreHealthSettingsValid()
        ? new BatteryHealthOptions
        {
            WarningThresholdPercent = BatteryWarningThresholdPercent,
            CriticalThresholdPercent = BatteryCriticalThresholdPercent,
            ComparableLoadTolerancePercent = ComparableLoadTolerancePercent,
        }
        : new BatteryHealthOptions();

    private bool AreHealthSettingsValid() =>
        BatteryCriticalThresholdPercent is >= 1 and < 100
        && BatteryWarningThresholdPercent > BatteryCriticalThresholdPercent
        && BatteryWarningThresholdPercent <= 100
        && ComparableLoadTolerancePercent is >= 0 and <= 25;

    private static string LocalizeHealthStatus(BatteryHealthStatus status) => L(status switch
    {
        BatteryHealthStatus.VendorReported => "HealthStatusVendorReported",
        BatteryHealthStatus.Excellent => "HealthStatusExcellent",
        BatteryHealthStatus.Good => "HealthStatusGood",
        BatteryHealthStatus.Fair => "HealthStatusFair",
        BatteryHealthStatus.Degraded => "HealthStatusDegraded",
        BatteryHealthStatus.Poor => "HealthStatusPoor",
        BatteryHealthStatus.Critical => "HealthStatusCritical",
        _ => "HealthStatusUnknown",
    });

    private static string LocalizeHealthConfidence(BatteryHealthConfidence confidence) => L(confidence switch
    {
        BatteryHealthConfidence.Low => "HealthConfidenceLow",
        BatteryHealthConfidence.Medium => "HealthConfidenceMedium",
        BatteryHealthConfidence.High => "HealthConfidenceHigh",
        _ => "HealthConfidenceUnknown",
    });

    private static string LocalizeHealthMethod(BatteryHealthMethod method) => L(method switch
    {
        BatteryHealthMethod.ControlledRuntimeTest => "HealthMethodControlledRuntime",
        BatteryHealthMethod.DischargedEnergy => "HealthMethodDischargedEnergy",
        BatteryHealthMethod.CapacityRatio => "HealthMethodCapacityRatio",
        BatteryHealthMethod.RuntimeBaseline => "HealthMethodRuntimeBaseline",
        BatteryHealthMethod.RelativeRuntimeTrend => "HealthMethodRelativeRuntime",
        BatteryHealthMethod.VendorAnchoredRuntime => "HealthMethodVendorAnchored",
        _ => "HealthMethodNone",
    });

    private static string HealthAccent(BatteryHealthStatus status) => status switch
    {
        BatteryHealthStatus.VendorReported => "#38BDF8",
        BatteryHealthStatus.Excellent or BatteryHealthStatus.Good => "#22C55E",
        BatteryHealthStatus.Fair => "#84CC16",
        BatteryHealthStatus.Degraded => "#F59E0B",
        BatteryHealthStatus.Poor or BatteryHealthStatus.Critical => "#EF4444",
        _ => "#64748B",
    };

    private static string LocalizeHealthDetail(BatteryHealthResult health)
    {
        if (health.Status != BatteryHealthStatus.VendorReported)
        {
            return LocalizeHealthStatus(health.Status);
        }

        var source = string.IsNullOrWhiteSpace(health.AnchorSource)
            ? L("Unknown")
            : health.AnchorSource;
        return health.VendorHealthCategory == VendorBatteryHealthCategory.Unknown
            ? LocalizationManager.Format("VendorHealthDetailUnknownFormat", source)
            : LocalizationManager.Format(
                "VendorHealthDetailFormat",
                source,
                LocalizeVendorHealthCategory(health.VendorHealthCategory));
    }

    private static string LocalizeVendorHealthCategory(VendorBatteryHealthCategory category) => L(category switch
    {
        VendorBatteryHealthCategory.Good => "VendorHealthCategoryGood",
        VendorBatteryHealthCategory.Average => "VendorHealthCategoryAverage",
        VendorBatteryHealthCategory.BelowAverage => "VendorHealthCategoryBelowAverage",
        VendorBatteryHealthCategory.Poor => "VendorHealthCategoryPoor",
        _ => "VendorHealthCategoryUnknown",
    });

    private static string LocalizeReplacementStatus(BatteryReplacementStatus status) => L(status switch
    {
        BatteryReplacementStatus.CheckRequired => "ReplacementStatusCheckRequired",
        BatteryReplacementStatus.ConsiderReplacement => "ReplacementStatusConsiderReplacement",
        BatteryReplacementStatus.ReplacementRequested => "ReplacementStatusRequested",
        BatteryReplacementStatus.NoSignal => "ReplacementStatusNoSignal",
        _ => "ReplacementStatusUnknown",
    });

    private static string LocalizeReplacementReason(BatteryReplacementReason reason) => reason.Code switch
    {
        BatteryReplacementReasonCode.NeedReplacementReported => L("ReplacementReasonNeedReplacement"),
        BatteryReplacementReasonCode.SelfTestFailed => L("ReplacementReasonSelfTestFailed"),
        BatteryReplacementReasonCode.PhysicalCapacityBelowReference => LocalizationManager.Format(
            "ReplacementReasonPhysicalCapacity", reason.Observed, reason.Reference),
        BatteryReplacementReasonCode.ControlledMeasurementBelowReference => LocalizationManager.Format(
            "ReplacementReasonControlledMeasurement", reason.Observed, reason.Reference),
        BatteryReplacementReasonCode.NewBatteryRuntimeBelowReference => LocalizationManager.Format(
            "ReplacementReasonNewBatteryRuntime", reason.Observed, reason.Reference),
        BatteryReplacementReasonCode.RelativeRuntimeDeclined => LocalizationManager.Format(
            "ReplacementReasonRelativeRuntime", reason.Observed, reason.Reference),
        _ => L("ReplacementReasonNoSignal"),
    };

    private static string LocalizeReplacementDetail(BatteryHealthResult health)
    {
        if (health.Replacement.Reasons.FirstOrDefault() is { } reason)
        {
            return LocalizeReplacementReason(reason);
        }

        if (health.Replacement.Status == BatteryReplacementStatus.Unknown)
        {
            return L("ReplacementReasonUnknown");
        }

        return health.PrimaryMethod == BatteryHealthMethod.VendorAnchoredRuntime
            ? L("ReplacementReasonBhiNoSignal")
            : L("ReplacementReasonNoSignal");
    }

    private static string ReplacementAccent(BatteryReplacementStatus status) => status switch
    {
        BatteryReplacementStatus.CheckRequired => "#F59E0B",
        BatteryReplacementStatus.ConsiderReplacement => "#F97316",
        BatteryReplacementStatus.ReplacementRequested => "#EF4444",
        BatteryReplacementStatus.NoSignal => "#22C55E",
        _ => "#64748B",
    };

    private static string LocalizeHealthReason(BatteryHealthReason reason) => reason.Code switch
    {
        BatteryHealthReasonCode.NotConnected => L("HealthReasonNotConnected"),
        BatteryHealthReasonCode.NeedReplacementReported => L("HealthReasonNeedReplacement"),
        BatteryHealthReasonCode.SelfTestFailed => L("HealthReasonSelfTestFailed"),
        BatteryHealthReasonCode.SelfTestPassed => L("HealthReasonSelfTestPassed"),
        BatteryHealthReasonCode.ControlledMeasurementCompared => LocalizationManager.Format(
            "HealthReasonControlledMeasurement", reason.Observed, reason.Reference),
        BatteryHealthReasonCode.CapacityCompared => LocalizationManager.Format(
            "HealthReasonCapacityCompared", reason.Observed, reason.Reference),
        BatteryHealthReasonCode.RuntimeComparedWithBaseline => LocalizationManager.Format(
            "HealthReasonRuntimeCompared",
            FormatDuration(TimeSpan.FromSeconds(reason.Observed ?? 0)),
            FormatDuration(TimeSpan.FromSeconds(reason.Reference ?? 0))),
        BatteryHealthReasonCode.NewBatteryBaselineApplied => L("HealthReasonNewBatteryBaseline"),
        BatteryHealthReasonCode.CurrentRelativeBaselineApplied => L("HealthReasonRelativeBaseline"),
        BatteryHealthReasonCode.KnownHealthAnchorApplied => LocalizationManager.Format(
            "HealthReasonKnownAnchor", reason.Observed ?? 0, reason.Reference ?? 0),
        BatteryHealthReasonCode.KnownHealthAnchorInvalid => L("HealthReasonKnownAnchorInvalid"),
        BatteryHealthReasonCode.MeasurementAboveReference => L("HealthReasonAboveBaseline"),
        BatteryHealthReasonCode.CapacityDataIsPercentageOnly => L("HealthReasonPercentageCapacity"),
        BatteryHealthReasonCode.BatteryNotFullyCharged => L("HealthReasonNotFullyCharged"),
        BatteryHealthReasonCode.NoComparableRuntimeBaseline => L("HealthReasonNoBaseline"),
        BatteryHealthReasonCode.BatteryAgeKnown => L("HealthReasonAgeKnown"),
        BatteryHealthReasonCode.InvalidTelemetryIgnored => LocalizationManager.Format(
            "HealthReasonInvalidTelemetry", reason.Observed ?? 0),
        _ => L("HealthReasonInsufficientData"),
    };

    private static string FormatBaselineSummary(BatteryHealthProfile? profile)
    {
        var points = profile?.RuntimeBaselines;
        if (points is null || points.Count == 0)
        {
            return L("BaselineNone");
        }

        var latest = points.MaxBy(item => item.MeasuredAt)!;
        var measurement = LocalizationManager.Format(
            "BaselineSummaryFormat",
            points.Count,
            latest.LoadPercent,
            FormatDuration(latest.Runtime),
            latest.MeasuredAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture));
        return LocalizationManager.Format(
            "BaselineSummaryWithModeFormat",
            measurement,
            FormatBaselineModeDetails(profile!));
    }

    private void RefreshBaselineModeOptions()
    {
        BaselineModeOptions =
        [
            new(BatteryRuntimeBaselineKind.CurrentRelative, L("BaselineModeRelative")),
            new(BatteryRuntimeBaselineKind.KnownHealthAnchor, L("BaselineModeKnown")),
            new(BatteryRuntimeBaselineKind.NewBattery, L("BaselineModeNew")),
        ];
    }

    private void RefreshHistoryRangeOptions()
    {
        var selectedKey = SelectedHistoryRange?.Key ?? "1h";
        HistoryRangeOptions =
        [
            new("1h", TimeSpan.FromHours(1), L("HistoryRange1Hour")),
            new("6h", TimeSpan.FromHours(6), L("HistoryRange6Hours")),
            new("24h", TimeSpan.FromHours(24), L("HistoryRange24Hours")),
            new("7d", TimeSpan.FromDays(7), L("HistoryRange7Days")),
            new("30d", TimeSpan.FromDays(30), L("HistoryRange30Days")),
        ];
        SelectedHistoryRange = HistoryRangeOptions.First(item => item.Key == selectedKey);
    }

    private void RefreshAnalyticsOptions()
    {
        var currentMetric = SelectedAnalyticsMetric?.Metric ?? TelemetryMetric.ActivePowerWatts;
        AnalyticsMetricOptions =
        [
            new(TelemetryMetric.ActivePowerWatts, L("AnalyticsMetricPower"), "W"),
            new(TelemetryMetric.InputVoltage, L("AnalyticsMetricVoltage"), "V"),
            new(TelemetryMetric.LoadPercent, L("AnalyticsMetricLoad"), "%"),
        ];
        SelectedAnalyticsMetric = AnalyticsMetricOptions.FirstOrDefault(item => item.Metric == currentMetric) ?? AnalyticsMetricOptions[0];

        var currentDuration = SelectedAnalyticsRange?.Duration ?? TimeSpan.FromDays(30);
        AnalyticsRangeOptions =
        [
            new(TimeSpan.FromDays(7), L("AnalyticsRange7D")),
            new(TimeSpan.FromDays(30), L("AnalyticsRange30D")),
            new(TimeSpan.FromDays(90), L("AnalyticsRange90D")),
            new(null, L("AnalyticsRangeAll")),
        ];
        SelectedAnalyticsRange = AnalyticsRangeOptions.FirstOrDefault(item => item.Duration == currentDuration) ?? AnalyticsRangeOptions[1];
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

        var metric = SelectedAnalyticsMetric?.Metric ?? TelemetryMetric.ActivePowerWatts;
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
            AnalyticsSampleCountText = $"{result.TotalSamples:N0} 分 ({result.TotalSamples / 60.0 / 24.0:0.1} 日分)";

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

    private void RefreshVendorHealthCategoryOptions()
    {
        VendorHealthCategoryOptions =
        [
            new(VendorBatteryHealthCategory.Unknown, LocalizeVendorHealthCategory(VendorBatteryHealthCategory.Unknown)),
            new(VendorBatteryHealthCategory.Good, LocalizeVendorHealthCategory(VendorBatteryHealthCategory.Good)),
            new(VendorBatteryHealthCategory.Average, LocalizeVendorHealthCategory(VendorBatteryHealthCategory.Average)),
            new(VendorBatteryHealthCategory.BelowAverage, LocalizeVendorHealthCategory(VendorBatteryHealthCategory.BelowAverage)),
            new(VendorBatteryHealthCategory.Poor, LocalizeVendorHealthCategory(VendorBatteryHealthCategory.Poor)),
        ];
    }

    private void InitializeBaselineEditor(BatteryHealthProfile? profile)
    {
        if (_baselineEditorInitialized || profile is null)
        {
            return;
        }

        if (profile.RuntimeBaselineKind != BatteryRuntimeBaselineKind.Unspecified)
        {
            SelectedBaselineKind = profile.RuntimeBaselineKind;
        }

        if (profile.AnchorHealthPercent is { } anchor && anchor is > 0 and <= 100)
        {
            KnownHealthPercent = anchor;
        }

        if (!string.IsNullOrWhiteSpace(profile.AnchorSource))
        {
            KnownHealthSource = profile.AnchorSource;
        }

        SelectedVendorHealthCategory = profile.VendorHealthCategory;

        _baselineEditorInitialized = true;
    }

    private static string LocalizeBaselineKind(BatteryRuntimeBaselineKind kind) => L(kind switch
    {
        BatteryRuntimeBaselineKind.NewBattery => "BaselineModeNew",
        BatteryRuntimeBaselineKind.KnownHealthAnchor => "BaselineModeKnown",
        BatteryRuntimeBaselineKind.CurrentRelative => "BaselineModeRelative",
        _ => "BaselineSummaryModeLegacy",
    });

    private static string FormatBaselineModeDetails(BatteryHealthProfile profile) => profile.RuntimeBaselineKind switch
    {
        BatteryRuntimeBaselineKind.NewBattery => L("BaselineSummaryModeNew"),
        BatteryRuntimeBaselineKind.CurrentRelative => L("BaselineSummaryModeRelative"),
        BatteryRuntimeBaselineKind.KnownHealthAnchor when profile.AnchorHealthPercent is { } anchor =>
            LocalizationManager.Format(
                "BaselineSummaryModeKnownFormat",
                anchor,
                string.IsNullOrWhiteSpace(profile.AnchorSource) ? L("Unknown") : profile.AnchorSource),
        BatteryRuntimeBaselineKind.KnownHealthAnchor => L("BaselineSummaryModeKnownInvalid"),
        _ => L("BaselineSummaryModeLegacy"),
    };

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private bool SetField(ref IReadOnlyList<string> field, IReadOnlyList<string> value, [CallerMemberName] string? propertyName = null)
    {
        if (field.SequenceEqual(value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void RefreshLogSeverityOptions()
    {
        LogSeverityFilterOptions =
        [
            new(LogSeverityFilterKind.All, L("FilterAll")),
            new(LogSeverityFilterKind.Information, L("FilterInformation")),
            new(LogSeverityFilterKind.Warning, L("FilterWarning")),
            new(LogSeverityFilterKind.Critical, L("FilterCritical")),
        ];
    }

    private bool FilterEventItem(object obj)
    {
        if (obj is not UpsEventViewModel evt)
        {
            return false;
        }

        if (SelectedLogSeverityFilter != LogSeverityFilterKind.All)
        {
            var matchesSeverity = SelectedLogSeverityFilter switch
            {
                LogSeverityFilterKind.Information => evt.Severity == UpsEventSeverity.Information,
                LogSeverityFilterKind.Warning => evt.Severity == UpsEventSeverity.Warning,
                LogSeverityFilterKind.Critical => evt.Severity == UpsEventSeverity.Critical,
                _ => true,
            };

            if (!matchesSeverity)
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(_logSearchText))
        {
            return true;
        }

        var query = _logSearchText.Trim();
        return evt.Message.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || evt.Type.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || evt.StateTransition.Contains(query, StringComparison.CurrentCultureIgnoreCase)
            || evt.Timestamp.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public enum LogSeverityFilterKind
{
    All,
    Information,
    Warning,
    Critical,
}

public sealed record LogSeverityFilterOption(LogSeverityFilterKind Kind, string DisplayName);

public sealed class UpsEventViewModel : INotifyPropertyChanged
{
    private readonly UpsEvent _source;

    public UpsEventViewModel(UpsEvent upsEvent)
    {
        _source = upsEvent;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UpsEventSeverity Severity => _source.Severity;

    public string Timestamp => _source.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

    public string Type => _source.Type switch
    {
        UpsEventType.PowerLost => LocalizationManager.Get("EventPowerLost"),
        UpsEventType.PowerRestored => LocalizationManager.Get("EventPowerRestored"),
        UpsEventType.BatteryLow => LocalizationManager.Get("EventBatteryLow"),
        UpsEventType.BatteryCritical => LocalizationManager.Get("EventBatteryCritical"),
        UpsEventType.RuntimeLow => LocalizationManager.Get("EventRuntimeLow"),
        UpsEventType.OverloadDetected => LocalizationManager.Get("EventOverload"),
        UpsEventType.UpsDisconnected => LocalizationManager.Get("EventUpsDisconnected"),
        UpsEventType.UpsReconnected => LocalizationManager.Get("EventUpsReconnected"),
        _ => _source.Type.ToString(),
    };

    public string Message => LocalizationManager.Get(_source.Type switch
    {
        UpsEventType.PowerLost => "EventMessagePowerLost",
        UpsEventType.PowerRestored => "EventMessagePowerRestored",
        UpsEventType.BatteryLow => "EventMessageBatteryLow",
        UpsEventType.BatteryCritical => "EventMessageBatteryCritical",
        UpsEventType.RuntimeLow => "EventMessageRuntimeLow",
        UpsEventType.OverloadDetected => "EventMessageOverload",
        UpsEventType.UpsDisconnected => "EventMessageUpsDisconnected",
        UpsEventType.UpsReconnected => "EventMessageUpsReconnected",
        _ => _source.Message,
    });

    public string StateTransition => $"{LocalizeState(_source.PreviousState)} → {LocalizeState(_source.CurrentState)}";

    public void RefreshLanguage()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Timestamp)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateTransition)));
    }

    private static string LocalizeState(UpsPowerState state) => state switch
    {
        UpsPowerState.Online => LocalizationManager.Get("StateOnline"),
        UpsPowerState.OnBattery => LocalizationManager.Get("StateOnBattery"),
        UpsPowerState.LowBattery => LocalizationManager.Get("StateLowBattery"),
        UpsPowerState.Critical => LocalizationManager.Get("StateCritical"),
        _ => LocalizationManager.Get("StateUnknown"),
    };
}

public sealed class UpsTelemetryViewModel : INotifyPropertyChanged
{
    private string _value;
    private string _raw;

    public UpsTelemetryViewModel(UpsTelemetryItem item)
    {
        Key = item.Key;
        PageUsage = $"0x{item.UsagePage:X4}:0x{item.Usage:X4}";
        Page = item.UsagePage switch
        {
            0x84 => LocalizationManager.Get("HidPagePowerDevice"),
            0x85 => LocalizationManager.Get("HidPageBatterySystem"),
            >= 0xFF00 => LocalizationManager.Get("HidPageVendorDefined"),
            _ => item.UsagePageName,
        };
        Collection = LocalizationManager.LocalizeCollectionPath(item.CollectionPath);
        Name = LocalizationManager.LocalizeUsageName(item.UsagePage, item.Usage, item.UsageName);
        _value = LocalizationManager.LocalizeTelemetryValue(item.DisplayValue);
        _raw = item.RawValue?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
        var reportName = item.ReportType.ToString() switch
        {
            "Input" => LocalizationManager.Get("HidReportInput"),
            "Feature" => LocalizationManager.Get("HidReportFeature"),
            "Output" => LocalizationManager.Get("HidReportOutput"),
            var name => name,
        };
        Source = $"{reportName} #{item.ReportId}";
        Access = item.IsReadable
            ? LocalizationManager.Get("HidAccessRead")
            : LocalizationManager.Get("HidAccessWriteOnly");
        LogicalRange = $"{item.LogicalMinimum}..{item.LogicalMaximum}";
        PhysicalRange = $"{item.PhysicalMinimum}..{item.PhysicalMaximum}";
        Unit = item.HidUnit == 0
            ? LocalizationManager.Get("HidUnitNone")
            : $"0x{item.HidUnit:X8} exp {item.UnitExponent}";
        Layout = $"{item.BitSize} bit × {item.ReportCount}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Key { get; }
    public string PageUsage { get; }
    public string Page { get; }
    public string Collection { get; }
    public string Name { get; }

    public string Value
    {
        get => _value;
        private set
        {
            if (_value != value)
            {
                _value = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
            }
        }
    }

    public string Raw
    {
        get => _raw;
        private set
        {
            if (_raw != value)
            {
                _raw = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Raw)));
            }
        }
    }

    public string Source { get; }
    public string Access { get; }
    public string LogicalRange { get; }
    public string PhysicalRange { get; }
    public string Unit { get; }
    public string Layout { get; }

    public void Update(UpsTelemetryItem item)
    {
        Value = LocalizationManager.LocalizeTelemetryValue(item.DisplayValue);
        Raw = item.RawValue?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
    }
}

public sealed record LanguageOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record BatteryBaselineModeOption(
    BatteryRuntimeBaselineKind Kind,
    string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record VendorHealthCategoryOption(
    VendorBatteryHealthCategory Category,
    string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record AnalyticsMetricOption(
    TelemetryMetric Metric,
    string DisplayName,
    string Unit)
{
    public override string ToString() => DisplayName;
}

public sealed record AnalyticsRangeOption(
    TimeSpan? Duration,
    string DisplayName)
{
    public override string ToString() => DisplayName;
}

public sealed record ThemeOption(string Code, string DisplayName)
{
    public override string ToString() => DisplayName;
}
