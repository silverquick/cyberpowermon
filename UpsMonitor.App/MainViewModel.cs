using System.Collections.ObjectModel;
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
    private readonly UpsMonitorEngine _engine;
    private readonly JsonConfigurationStore _configurationStore;
    private readonly AppConfiguration _configuration;
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
    private string _selectedLanguageCode;
    private UpsSnapshot? _lastSnapshot;

    public MainViewModel(
        UpsMonitorEngine engine,
        JsonConfigurationStore configurationStore,
        AppConfiguration configuration,
        AppPaths paths)
    {
        _engine = engine;
        _configurationStore = configurationStore;
        _configuration = configuration;
        _pollIntervalMs = configuration.Monitoring.PollIntervalMs;
        _selectedLanguageCode = LocalizationManager.CurrentLanguageCode;
        RuntimeLowSeconds = configuration.Monitoring.RuntimeLowSeconds;
        ConfigurationFile = paths.ConfigurationFile;
        LogsDirectory = paths.LogsDirectory;
        _dispatcher = Application.Current.Dispatcher;
        SaveSettingsCommand = new AsyncRelayCommand(SaveSettingsAsync, SetCommandError);

        _engine.SnapshotUpdated += OnSnapshotUpdated;
        _engine.EventDetected += OnEventDetected;
        _engine.MonitorError += OnMonitorError;
        LocalizationManager.LanguageChanged += OnLanguageChanged;
        ApplyWaitingState();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<UpsEventViewModel> Events { get; } = [];

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new("ja-JP", "日本語"),
        new("en-US", "English"),
    ];

    public ICommand SaveSettingsCommand { get; }

    public string ConfigurationFile { get; }

    public string LogsDirectory { get; }

    public int RuntimeLowSeconds { get; }

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

    public string ConnectionText { get => _connectionText; private set => SetField(ref _connectionText, value); }
    public string StateText { get => _stateText; private set => SetField(ref _stateText, value); }
    public string StatusMessage { get => _statusMessage; private set => SetField(ref _statusMessage, value); }
    public string StatusAccent { get => _statusAccent; private set => SetField(ref _statusAccent, value); }
    public string LastUpdateText { get => _lastUpdateText; private set => SetField(ref _lastUpdateText, value); }
    public string LastError { get => _lastError; private set => SetField(ref _lastError, value); }
    public string SettingsStatus { get => _settingsStatus; private set => SetField(ref _settingsStatus, value); }
    public string TelemetryCountText { get => _telemetryCountText; private set => SetField(ref _telemetryCountText, value); }
    public IReadOnlyList<UpsTelemetryViewModel> TelemetryItems { get => _telemetryItems; private set => SetField(ref _telemetryItems, value); }

    public string Manufacturer { get; private set; } = "N/A";
    public string Product { get; private set; } = "No UPS detected";
    public string SerialNumber { get; private set; } = "N/A";
    public string VidPid { get; private set; } = "N/A";
    public string Usage { get; private set; } = "N/A";
    public string DevicePath { get; private set; } = "N/A";
    public string InputReportLength { get; private set; } = "N/A";
    public string FeatureReportLength { get; private set; } = "N/A";
    public string PowerText { get; private set; } = "N/A";
    public string BatteryText { get; private set; } = "N/A";
    public string BatteryHealthText { get; private set; } = "N/A";
    public string BatteryHealthDetailText { get; private set; } = string.Empty;
    public double BatteryProgress { get; private set; }
    public string RuntimeText { get; private set; } = "N/A";
    public string OverloadText { get; private set; } = "N/A";
    public string ChargingText { get; private set; } = "N/A";
    public string DischargingText { get; private set; } = "N/A";
    public string LowBatteryText { get; private set; } = "N/A";
    public string CriticalText { get; private set; } = "N/A";
    public string AcPresentText { get; private set; } = "N/A";
    public string VoltageText { get; private set; } = "N/A";
    public string CurrentText { get; private set; } = "N/A";
    public string FrequencyText { get; private set; } = "N/A";
    public string TemperatureText { get; private set; } = "N/A";
    public string RemainingTimeLimitText { get; private set; } = "N/A";
    public string DesignCapacityText { get; private set; } = "N/A";
    public string FullChargeCapacityText { get; private set; } = "N/A";
    public string BatteryVoltageText { get; private set; } = "N/A";
    public string InputVoltageText { get; private set; } = "N/A";
    public string OutputVoltageText { get; private set; } = "N/A";
    public string PercentLoadText { get; private set; } = "N/A";
    public string ActivePowerText { get; private set; } = "N/A";
    public string ApparentPowerText { get; private set; } = "N/A";
    public string FullyChargedText { get; private set; } = "N/A";
    public string RechargeableText { get; private set; } = "N/A";
    public string RemainingTimeExpiredText { get; private set; } = "N/A";
    public string BoostText { get; private set; } = "N/A";
    public string AudibleAlarmText { get; private set; } = "N/A";
    public string SelfTestText { get; private set; } = "N/A";
    public string TransferRangeText { get; private set; } = "N/A";
    public string RatedPowerText { get; private set; } = "N/A";
    public string BatteryChemistryText { get; private set; } = "N/A";
    public string OemInformationText { get; private set; } = "N/A";
    public string InputVoltageSummaryText { get; private set; } = "N/A";
    public string InputOutputText { get; private set; } = "N/A";
    public string ReportBytesText { get; private set; } = "N/A";

    public void Start() => _engine.Start();

    public void NotifyDeviceChange() => _engine.NotifyDeviceChange();

    public void SetStartupError(string message) => LastError = message;

    public async ValueTask DisposeAsync()
    {
        _engine.SnapshotUpdated -= OnSnapshotUpdated;
        _engine.EventDetected -= OnEventDetected;
        _engine.MonitorError -= OnMonitorError;
        LocalizationManager.LanguageChanged -= OnLanguageChanged;
        await _engine.DisposeAsync();
    }

    private void OnSnapshotUpdated(UpsSnapshot snapshot) =>
        _ = _dispatcher.InvokeAsync(() => ApplySnapshot(snapshot));

    private void OnEventDetected(UpsEvent upsEvent) =>
        _ = _dispatcher.InvokeAsync(() =>
        {
            Events.Insert(0, new UpsEventViewModel(upsEvent));
            while (Events.Count > 500)
            {
                Events.RemoveAt(Events.Count - 1);
            }
        });

    private void OnMonitorError(Exception exception) =>
        _ = _dispatcher.InvokeAsync(() => LastError = exception.Message);

    private void OnLanguageChanged(object? sender, EventArgs e)
    {
        _selectedLanguageCode = LocalizationManager.CurrentLanguageCode;
        OnPropertyChanged(nameof(SelectedLanguageCode));
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
    }

    private void ApplyWaitingState()
    {
        ConnectionText = L("SearchingConnection");
        StateText = L("StateUnknown");
        StatusMessage = L("SearchingStatus");
        LastUpdateText = L("NeverUpdated");
        Product = L("NoUpsDetected");
        TelemetryCountText = LocalizationManager.Format("TelemetryCountFormat", 0, 0, 0, 0);
        RaiseSnapshotProperties();
    }

    private void ApplySnapshot(UpsSnapshot snapshot)
    {
        _lastSnapshot = snapshot;
        LastUpdateText = LocalizationManager.Format("LastUpdateFormat", snapshot.Timestamp.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        ConnectionText = snapshot.IsConnected ? L("Connected") : L("Disconnected");

        var state = UpsPowerStateEvaluator.Evaluate(snapshot);
        StateText = state switch
        {
            UpsPowerState.Online => L("StateOnline"),
            UpsPowerState.OnBattery => L("StateOnBattery"),
            UpsPowerState.LowBattery => L("StateLowBattery"),
            UpsPowerState.Critical => L("StateCritical"),
            _ => L("StateUnknown"),
        };
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
        BatteryText = snapshot.BatteryPercent is { } battery ? $"{battery:0.#}%" : "N/A";
        BatteryHealthText = snapshot.BatteryHealthPercent is { } health ? $"{health:0.#}%" : "N/A";
        BatteryHealthDetailText = snapshot.BatteryHealthPercent is null
            ? L("BatteryHealthUnavailable")
            : string.Empty;
        BatteryProgress = snapshot.BatteryPercent ?? 0;
        RuntimeText = FormatDuration(snapshot.RuntimeRemaining);
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
        BatteryVoltageText = FormatNumber(snapshot.BatteryVoltage, "V");
        InputVoltageText = FormatNumber(snapshot.InputVoltage, "V");
        OutputVoltageText = FormatNumber(snapshot.OutputVoltage, "V");
        PercentLoadText = FormatNumber(snapshot.PercentLoad, "%");
        ActivePowerText = FormatNumber(snapshot.ActivePower, "W");
        ApparentPowerText = FormatNumber(snapshot.ApparentPower, "VA");
        FullyChargedText = FormatBoolean(snapshot.FullyCharged);
        RechargeableText = FormatBoolean(snapshot.Rechargeable);
        RemainingTimeExpiredText = FormatBoolean(snapshot.RemainingTimeLimitExpired);
        BoostText = FormatBoolean(snapshot.Boost);
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
        TelemetryItems = snapshot.Telemetry.Select(item => new UpsTelemetryViewModel(item)).ToArray();
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

        RaiseSnapshotProperties();
    }

    private async Task SaveSettingsAsync()
    {
        if (PollIntervalMs is < 250 or > 60_000)
        {
            SettingsStatus = L("PollIntervalValidation");
            return;
        }

        _configuration.Monitoring.PollIntervalMs = PollIntervalMs;
        _configuration.Ui.Language = SelectedLanguageCode;
        await _configurationStore.SaveAsync(_configuration);
        _engine.SetPollInterval(PollIntervalMs);
        SettingsStatus = LocalizationManager.Format("SettingsSavedFormat", DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
    }

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
            nameof(BatteryProgress), nameof(RuntimeText), nameof(OverloadText),
            nameof(ChargingText), nameof(DischargingText), nameof(LowBatteryText), nameof(CriticalText),
            nameof(AcPresentText), nameof(VoltageText), nameof(CurrentText), nameof(FrequencyText),
            nameof(TemperatureText), nameof(RemainingTimeLimitText), nameof(DesignCapacityText),
            nameof(FullChargeCapacityText), nameof(BatteryVoltageText), nameof(InputVoltageText),
            nameof(OutputVoltageText), nameof(PercentLoadText), nameof(ActivePowerText),
            nameof(ApparentPowerText), nameof(FullyChargedText), nameof(RechargeableText),
            nameof(RemainingTimeExpiredText), nameof(BoostText), nameof(AudibleAlarmText),
            nameof(SelfTestText), nameof(TransferRangeText), nameof(RatedPowerText),
            nameof(BatteryChemistryText), nameof(OemInformationText), nameof(InputVoltageSummaryText),
            nameof(InputOutputText), nameof(ReportBytesText),
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class UpsEventViewModel : INotifyPropertyChanged
{
    private readonly UpsEvent _source;

    public UpsEventViewModel(UpsEvent upsEvent)
    {
        _source = upsEvent;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

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

public sealed class UpsTelemetryViewModel
{
    public UpsTelemetryViewModel(UpsTelemetryItem item)
    {
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
        Value = LocalizationManager.LocalizeTelemetryValue(item.DisplayValue);
        Raw = item.RawValue?.ToString(CultureInfo.InvariantCulture) ?? "N/A";
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

    public string PageUsage { get; }
    public string Page { get; }
    public string Collection { get; }
    public string Name { get; }
    public string Value { get; }
    public string Raw { get; }
    public string Source { get; }
    public string Access { get; }
    public string LogicalRange { get; }
    public string PhysicalRange { get; }
    public string Unit { get; }
    public string Layout { get; }
}

public sealed record LanguageOption(string Code, string DisplayName);
