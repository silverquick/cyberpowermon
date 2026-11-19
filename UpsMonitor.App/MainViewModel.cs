using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
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
    private readonly AppConfiguration _configuration;
    private readonly SqliteTelemetryStore? _historyStore;
    private readonly Dispatcher _dispatcher;
    private string _connectionText = string.Empty;
    private string _stateText = string.Empty;
    private string _statusMessage = string.Empty;
    private string _statusAccent = "#64748B";
    private string _lastUpdateText = string.Empty;
    private string _lastError = string.Empty;
    private string _telemetryCountText = string.Empty;
    private IReadOnlyList<UpsTelemetryViewModel> _telemetryItems = [];
    private UpsSnapshot? _lastSnapshot;
    private UpsTelemetry? _lastTelemetry;
    // 健全性計算専用の定常化済みテレメトリ。画面表示・基準記録は生の _lastTelemetry を使う。
    private UpsTelemetry? _lastHealthTelemetry;
    private readonly Dictionary<string, RuntimeSteadyStateFilter> _steadyStateFilters = new(StringComparer.OrdinalIgnoreCase);
    private bool _isWindowVisible;
    private int _selectedNavigationIndex;
    private UpsSnapshot? _pendingSnapshot;
    private List<UpsTelemetryViewModel> _telemetryItemsList = [];
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
        _configuration = configuration;
        _historyStore = historyStore;
        ConfigurationFile = paths.ConfigurationFile;
        TelemetryDatabaseFile = paths.TelemetryDatabaseFile;
        LogsDirectory = paths.LogsDirectory;
        _dispatcher = Application.Current.Dispatcher;
        Func<AppConfiguration,Task> save = config => configurationStore.SaveAsync(config);
        BatteryHealth = new BatteryHealthViewModel(configuration, save, historyStore,
            async action => await _dispatcher.InvokeAsync(action), SetSettingsStatus,
            SetCommandError, ReapplySnapshot);
        Settings = new SettingsViewModel(configuration, save,
            new SettingsEffects(theme => ThemeManager.ApplyTheme(Application.Current, theme),
                language => LocalizationManager.ApplyLanguage(Application.Current, language),
                StartupManager.SetRunOnStartup, _engine.SetPollInterval, _engine.SetRuntimeLowThreshold,
                _engine.SetAlertThresholds, RefreshPeriodCost, ReapplySnapshot),
            BatteryHealth, exception => LastError = exception.Message, LocalizationManager.CurrentLanguageCode,
            url => WebhookNotifier.SendTestNotificationAsync(url));
        Settings.TestNotificationRequested += severity => TestNotificationRequested?.Invoke(severity);
        Logs = new LogsViewModel(historyStore);
        Exports = new ExportViewModel(historyStore, ExportFilePicker.Pick, Logs.CreateExportQuery,
            exception => LastError = exception.Message, SetCommandError);
        History = new HistoryViewModel(historyStore, configuration, Exports,
            exception => LastError = exception.Message, SetCommandError);
        Analytics = new AnalyticsViewModel(historyStore, exception => LastError = exception.Message, SetCommandError);
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
        BatteryHealth.Initialize();
        ApplyWaitingState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event Action<string, string, UpsEventSeverity>? NotificationRequested;

    public event Action<string>? TooltipUpdated;

    public string AppVersion => typeof(MainViewModel).Assembly.GetName().Version is { } v
        ? VersionFormatter.Format(v)
        : "0.1.0";

    public string WindowTitle => $"PowerGuard v{AppVersion}";

    public event Action<UpsPowerState, double?, bool>? DynamicTrayIconUpdated;

    public event Action<UpsEventSeverity>? TestNotificationRequested;

    public event Action? ShowMiniMonitorRequested;

    public SettingsViewModel Settings { get; }
    public BatteryHealthViewModel BatteryHealth { get; }
    public string SelectedLanguageCode => Settings.SelectedLanguageCode;
    public LogsViewModel Logs { get; }
    private void SetCommandError(Exception exception) => Settings.SetCommandError(exception);
    private void SetSettingsStatus(string status) => Settings.SetStatus(status);
    private void RefreshPeriodCost() => History.RefreshPeriodCost();
    private void ReapplySnapshot() { if (_lastSnapshot is { } snapshot) ApplySnapshot(snapshot); }

    public ICommand OpenMiniMonitorCommand { get; }

    public HistoryViewModel History { get; }
    public ExportViewModel Exports { get; }

    public AnalyticsViewModel Analytics { get; }

    public bool IsExiting { get; set; }

    public string ConfigurationFile { get; }

    public string TelemetryDatabaseFile { get; }

    public string LogsDirectory { get; }

    public string PowerMarginText { get => _powerMarginText; private set => SetField(ref _powerMarginText, value); }
    public string VoltageMarginText { get => _voltageMarginText; private set => SetField(ref _voltageMarginText, value); }
    public string AvrBoostText { get => _avrBoostText; private set => SetField(ref _avrBoostText, value); }
    public string AvrBoostAccent { get => _avrBoostAccent; private set => SetField(ref _avrBoostAccent, value); }
    public string CellVoltageText { get => _cellVoltageText; private set => SetField(ref _cellVoltageText, value); }

    public bool IsWindowVisible
    {
        get => _isWindowVisible;
        set
        {
            if (SetField(ref _isWindowVisible, value))
            {
                Logs.SetActive(value && _selectedNavigationIndex == LogsIndex);
                if (value && _pendingSnapshot is { } pending)
                {
                    _pendingSnapshot = null;
                    ApplySnapshotCore(pending);
                }
                History.SetActive(value && IsHistoryRefreshTarget(_selectedNavigationIndex));
                Analytics.SetActive(value && IsAnalyticsRefreshTarget(_selectedNavigationIndex));
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
                Logs.SetActive(_isWindowVisible && value == LogsIndex);
                History.SetActive(_isWindowVisible && IsHistoryRefreshTarget(value));
                Analytics.SetActive(_isWindowVisible && IsAnalyticsRefreshTarget(value));
            }
        }
    }

    public string ConnectionText { get => _connectionText; private set => SetField(ref _connectionText, value); }
    public string StateText { get => _stateText; private set => SetField(ref _stateText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string StatusAccent { get => _statusAccent; private set => SetField(ref _statusAccent, value); }
    public string LastUpdateText { get => _lastUpdateText; private set => SetField(ref _lastUpdateText, value); }
    public string LastError { get => _lastError; private set => SetField(ref _lastError, value); }
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
        await Exports.DisposeAsync().ConfigureAwait(false);
        Settings.Dispose();
        BatteryHealth.Dispose();
        Logs.Dispose();
        Analytics.Dispose();
        History.Dispose();
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
            Logs.AddLive(upsEvent);
            NotificationRequested?.Invoke(eventVm.Type, eventVm.Message, upsEvent.Severity);

            if (Settings.EnableSoundAlerts)
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
        Settings.RefreshLanguage(LocalizationManager.CurrentLanguageCode);
        OnPropertyChanged(nameof(SelectedLanguageCode));
        History.RefreshLanguage(refresh: false);
        Logs.RefreshLanguage();
        Analytics.RefreshLanguage(refresh: false);
        // 記録時の誤差・詳細・有効件数・比較表も、保存せず即時に翻訳し直す。
        BatteryHealth.RefreshLanguage();
        _telemetryItemsList.Clear();
        TelemetryItems = [];
        Settings.SetStatus(string.Empty);

        if (_lastSnapshot is { } snapshot)
        {
            ApplySnapshot(snapshot);
        }
        else
        {
            ApplyWaitingState();
        }

        History.RefreshIfActive();
        Analytics.RefreshIfActive();
    }

    private void ApplyWaitingState()
    {
        _steadyStateFilters.Clear();
        ConnectionText = L("SearchingConnection");
        StateText = L("StateUnknown");
        StatusMessage = L("SearchingStatus");
        LastUpdateText = L("NeverUpdated");
        Product = L("NoUpsDetected");
        TelemetryCountText = LocalizationManager.Format("TelemetryCountFormat", 0, 0, 0, 0);
        History.SetWaitingState();
        RaiseSnapshotProperties();
    }

    private void ApplySnapshot(UpsSnapshot snapshot)
    {
        // 日付をまたいだ後も、記録欄のカレンダー上限を今日に更新する。
        _lastSnapshot = snapshot;
        Analytics.SetSnapshot(snapshot);
        History.SetSnapshot(snapshot);
        var telemetry = UpsTelemetryValidator.Normalize(snapshot);
        _lastTelemetry = telemetry;
        BatteryHealth.SetSnapshot(snapshot, telemetry);
        _lastHealthTelemetry = ApplySteadyStateFilter(snapshot, telemetry);
        var healthProfile = BatteryHealth.FindHealthProfile(snapshot.Device);
        var health = BatteryHealthCalculator.Calculate(_lastHealthTelemetry, healthProfile, BatteryHealth.CreateHealthOptions());
        BatteryHealth.RefreshModelIfDue();

        var state =UpsPowerStateEvaluator.Evaluate(snapshot);
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
        BatteryHealth.RecalculateSimulation();

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

        History.RefreshForSnapshot();
    }

    private void ApplySnapshotCore(UpsSnapshot snapshot)
    {
        var telemetry = _lastTelemetry ?? UpsTelemetryValidator.Normalize(snapshot);
        var healthProfile = BatteryHealth.FindHealthProfile(snapshot.Device);
        var health = BatteryHealthCalculator.Calculate(_lastHealthTelemetry ?? telemetry, healthProfile, BatteryHealth.CreateHealthOptions());
        ApplySnapshotCore(snapshot, telemetry, healthProfile, health);
    }

    private void ApplySnapshotCore(
        UpsSnapshot snapshot,
        UpsTelemetry telemetry,
        BatteryHealthProfile? healthProfile,
        BatteryHealthResult health)
    {
        BatteryHealth.InitializeBaselineEditor(healthProfile);
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
        BatteryHealthReasons = LocalizeHealthReasons(health);
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
                PowerMarginText = UiTextFormatting.PowerMargin(remainW, remainVa);
            }
            else
            {
                PowerMarginText = UiTextFormatting.PowerMargin(remainW, null);
            }
        }
        else
        {
            PowerMarginText = "N/A";
        }

        if (snapshot.InputVoltage is { } inV && snapshot.LowVoltageTransfer is { } lowV)
        {
            var margin = inV - lowV;
            VoltageMarginText = UiTextFormatting.VoltageMargin(lowV, margin);
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
            CellVoltageText = UiTextFormatting.CellVoltage(cellV, cellCount);
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

    private UpsTelemetry ApplySteadyStateFilter(UpsSnapshot snapshot, UpsTelemetry telemetry)
    {
        // 切断・デバイス不明時はフィルタを破棄し、再接続後は新しい窓で始める。
        if (snapshot.Device is not { } device || !telemetry.IsConnected)
        {
            _steadyStateFilters.Clear();
            return telemetry;
        }

        var id = UpsDeviceIdentity.Create(device);
        if (!_steadyStateFilters.TryGetValue(id, out var filter))
        {
            filter = new RuntimeSteadyStateFilter();
            _steadyStateFilters[id] = filter;
        }

        return filter.Apply(telemetry);
    }

    private void RaiseSnapshotProperties()
    {
        History.NotifySnapshotProperties();
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
        BatteryHealthMethod.OfficialHealthTrend => "HealthMethodOfficialTrend",
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

        if (health.PrimaryMethod == BatteryHealthMethod.OfficialHealthTrend)
        {
            return health.OfficialEstimate?.LastRecord is { Source: not VendorBatteryHealthCategory.Unknown } last
                ? LocalizationManager.Format("OfficialHealthDetailFormat", BatteryHealthViewModel.LocalizeVendorHealthCategory(last.Source))
                : L("OfficialHealthDetailUnknown");
        }

        var source = string.IsNullOrWhiteSpace(health.AnchorSource)
            ? L("Unknown")
            : health.AnchorSource;
        return health.VendorHealthCategory == VendorBatteryHealthCategory.Unknown
            ? LocalizationManager.Format("VendorHealthDetailUnknownFormat", source)
            : LocalizationManager.Format(
                "VendorHealthDetailFormat",
                source,
                BatteryHealthViewModel.LocalizeVendorHealthCategory(health.VendorHealthCategory));
    }

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
        BatteryReplacementReasonCode.OfficialHealthTrendBelowReference => LocalizationManager.Format(
            "ReplacementReasonOfficialTrend", reason.Observed, reason.Reference),
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

    private static IReadOnlyList<string> LocalizeHealthReasons(BatteryHealthResult health)
    {
        var reasons = health.Reasons
            .Select(reason => LocalizeHealthReason(reason, health))
            .ToList();
        if (health.PrimaryMethod == BatteryHealthMethod.OfficialHealthTrend
            && health.RelativePerformancePercent is { } relative)
        {
            // 相対性能は健全性に反映しない参考値。根拠の直後に並べる。
            reasons.Insert(
                Math.Min(1, reasons.Count),
                LocalizationManager.Format("HealthReasonRelativePerformance", relative));
        }

        if (health.PrimaryMethod == BatteryHealthMethod.OfficialHealthTrend && health.OfficialEstimate is { } estimate)
        {
            // 採用モデルと、利用可能な全モデルの値の幅。
            reasons.Insert(
                Math.Min(1, reasons.Count),
                estimate.SpreadPercent >= 0.05
                    ? LocalizationManager.Format(
                        "HealthReasonOfficialModelRange",
                        BatteryHealthViewModel.LocalizeOfficialModel(estimate.Model),
                        estimate.Percent,
                        estimate.MinPercent,
                        estimate.MaxPercent)
                    : LocalizationManager.Format(
                        "HealthReasonOfficialModel",
                        BatteryHealthViewModel.LocalizeOfficialModel(estimate.Model)));
        }

        return reasons.Distinct().ToArray();
    }

    private static string LocalizeOfficialTrendReason(BatteryHealthResult? health)
    {
        var estimate = health?.OfficialEstimate;
        if (estimate?.LastRecord is not { } last)
        {
            return L("HealthMethodOfficialTrend");
        }

        var days = estimate.DaysSinceLastRecord ?? 0;
        return estimate.PreviousRecord is { } previous
            ? LocalizationManager.Format(
                "HealthReasonOfficialTwoRecords",
                previous.HealthPercent,
                FormatOfficialDate(previous.RecordedAt),
                last.HealthPercent,
                FormatOfficialDate(last.RecordedAt),
                days)
            : LocalizationManager.Format(
                "HealthReasonOfficialOneRecord",
                last.HealthPercent,
                FormatOfficialDate(last.RecordedAt),
                days);
    }

    private static string FormatOfficialDate(DateTimeOffset value) =>
        value.ToLocalTime().ToString("yyyy/MM/dd", CultureInfo.CurrentCulture);

    private static string LocalizeHealthReason(BatteryHealthReason reason, BatteryHealthResult? health = null) => reason.Code switch
    {
        BatteryHealthReasonCode.OfficialHealthTrendEstimated => LocalizeOfficialTrendReason(health),
        BatteryHealthReasonCode.RuntimeDeclineDetectedRerecord => LocalizationManager.Format(
            "HealthReasonRuntimeDeclineRerecord", reason.Observed ?? 0),
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
