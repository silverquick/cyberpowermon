using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;
namespace UpsMonitor.App;
internal sealed class SettingsHealthTestAdapter : INotifyPropertyChanged
{
    private readonly AppConfiguration _configuration;
    private readonly SqliteTelemetryStore? _historyStore;
    private readonly Func<AppConfiguration, Task> _save;
    private readonly SettingsEffects _effects;
    private readonly Func<Action, Task> _dispatch;
    private UpsSnapshot? _lastSnapshot;
    private UpsTelemetry? _lastTelemetry;
    public string LastError { get; private set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    public SettingsHealthTestAdapter(AppConfiguration config, Func<AppConfiguration,Task> save, SettingsEffects effects, SqliteTelemetryStore? store = null)
    {
        _configuration=config; _save=save; _effects=effects; _historyStore=store;
        _dispatch=action=>{action();return Task.CompletedTask;};
        _pollIntervalMs=config.Monitoring.PollIntervalMs; _runtimeLowSeconds=config.Monitoring.RuntimeLowSeconds;
        _selectedLanguageCode=LocalizationManager.Language;
        _batteryWarningThresholdPercent=config.BatteryHealth.WarningThresholdPercent;
        _batteryCriticalThresholdPercent=config.BatteryHealth.CriticalThresholdPercent;
        _comparableLoadTolerancePercent=config.BatteryHealth.ComparableLoadTolerancePercent;
        RefreshThemeOptions(); RefreshBaselineModeOptions(); RefreshVendorHealthCategoryOptions();
        SaveSettingsCommand=new AsyncRelayCommand(SaveSettingsAsync,SetCommandError);
        RecordRuntimeBaselineCommand=new AsyncRelayCommand(RecordRuntimeBaselineAsync,SetCommandError);
        ClearRuntimeBaselineCommand=new AsyncRelayCommand(ClearRuntimeBaselineAsync,SetCommandError);
        RecordOfficialHealthCommand=new AsyncRelayCommand(RecordOfficialHealthAsync,SetCommandError);
    }
    public void SetSnapshot(UpsSnapshot snapshot) { _lastSnapshot=snapshot; _lastTelemetry=UpsTelemetryValidator.Normalize(snapshot); }
    private void ApplySnapshot(UpsSnapshot snapshot) => _effects.ReapplySnapshot();
    public Task Save() => SaveSettingsAsync();
    public Task Record() => RecordOfficialHealthAsync();
    public Task Delete(OfficialHealthRecord record) => DeleteOfficialRecordAsync(record);
    public Task Baseline() => RecordRuntimeBaselineAsync();
    public Task ClearBaseline() => ClearRuntimeBaselineAsync();
    public Task Backfill() => BackfillOfficialContextsAsync();
    public Task Installed(DateTime? date) => SaveBatteryInstalledDateAsync(date);
    public void Language() { RefreshThemeOptions(); RefreshBaselineModeOptions(); RefreshVendorHealthCategoryOptions(); RefreshOfficialEditor(FindHealthProfile(_lastSnapshot?.Device)); }
    // Original member: _settingsStatus
    private string _settingsStatus = string.Empty;
    // Original member: _pollIntervalMs
    private int _pollIntervalMs;
    // Original member: _runtimeLowSeconds
    private int _runtimeLowSeconds;
    // Original member: _selectedLanguageCode
    private string _selectedLanguageCode;
    // Original member: _officialRecordPercent
    private double? _officialRecordPercent;
    // Original member: _officialRecordDate
    private DateTime? _officialRecordDate = DateTime.Today;
    // Original member: _officialRecordTime
    private string _officialRecordTime = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
    // Original member: _officialRecordSource
    private VendorBatteryHealthCategory _officialRecordSource = VendorBatteryHealthCategory.Unknown;
    // Original member: _officialRecordNote
    private string _officialRecordNote = string.Empty;
    // Original member: _batteryInstalledDate
    private DateTime? _batteryInstalledDate;
    // Original member: _officialErrorText
    private string _officialErrorText = "—";
    // Original member: _officialEditorLoading
    private bool _officialEditorLoading;
    // Original member: _batteryWarningThresholdPercent
    private double _batteryWarningThresholdPercent;
    // Original member: _batteryCriticalThresholdPercent
    private double _batteryCriticalThresholdPercent;
    // Original member: _comparableLoadTolerancePercent
    private double _comparableLoadTolerancePercent;
    // Original member: _baselineModeOptions
    private IReadOnlyList<BatteryBaselineModeOption> _baselineModeOptions = [];
    // Original member: _vendorHealthCategoryOptions
    private IReadOnlyList<VendorHealthCategoryOption> _vendorHealthCategoryOptions = [];
    // Original member: _selectedBaselineKind
    private BatteryRuntimeBaselineKind _selectedBaselineKind = BatteryRuntimeBaselineKind.CurrentRelative;
    // Original member: _selectedVendorHealthCategory
    private VendorBatteryHealthCategory _selectedVendorHealthCategory = VendorBatteryHealthCategory.Unknown;
    // Original member: _knownHealthPercent
    private double _knownHealthPercent = 59;
    // Original member: _knownHealthSource
    private string _knownHealthSource = "CyberPower BHI";
    // Original member: _baselineEditorInitialized
    private bool _baselineEditorInitialized;
    // Original member: _themeOptions
    private IReadOnlyList<ThemeOption> _themeOptions = [];
    // Original member: _webhookStatus
    private string _webhookStatus = string.Empty;
    // Original member: _loadSimulationTargetWatts
    private double _loadSimulationTargetWatts = 200.0;
    // Original member: _simulatedRuntimeText
    private string _simulatedRuntimeText = "-";
    // Original member: _standardLoadEstimates
    private IReadOnlyList<RuntimeEstimateTableItem> _standardLoadEstimates = [];

    // Original member: _lastOfficialModelRefresh
    private DateTimeOffset _lastOfficialModelRefresh = DateTimeOffset.MinValue;
    // Original member: SelectedTheme
    public string SelectedTheme
    {
        get => _configuration.Ui.Theme;
        set
        {
            if (_configuration.Ui.Theme != value)
            {
                _configuration.Ui.Theme = value;
                _effects.ApplyTheme(value);
                OnPropertyChanged();
            }
        }
    }

    // Original member: ThemeOptions
    public IReadOnlyList<ThemeOption> ThemeOptions
    {
        get => _themeOptions;
        private set => SetField(ref _themeOptions, value);
    }

    // Original member: EnableSoundAlerts
    public bool EnableSoundAlerts
    {
        get => _configuration.Alerts.EnableSoundAlerts;
        set { _configuration.Alerts.EnableSoundAlerts = value; OnPropertyChanged(); }
    }

    // Original member: HighLoadWarningPercent
    public double HighLoadWarningPercent
    {
        get => _configuration.Alerts.HighLoadWarningPercent;
        set { _configuration.Alerts.HighLoadWarningPercent = value; OnPropertyChanged(); }
    }

    // Original member: LowVoltageWarningThreshold
    public double LowVoltageWarningThreshold
    {
        get => _configuration.Alerts.LowVoltageWarningThreshold;
        set { _configuration.Alerts.LowVoltageWarningThreshold = value; OnPropertyChanged(); }
    }

    // Original member: HighVoltageWarningThreshold
    public double HighVoltageWarningThreshold
    {
        get => _configuration.Alerts.HighVoltageWarningThreshold;
        set { _configuration.Alerts.HighVoltageWarningThreshold = value; OnPropertyChanged(); }
    }

    // Original member: WebhookEnabled
    public bool WebhookEnabled
    {
        get => _configuration.Webhook.Enabled;
        set { _configuration.Webhook.Enabled = value; OnPropertyChanged(); }
    }

    // Original member: WebhookUrl
    public string WebhookUrl
    {
        get => _configuration.Webhook.Url;
        set { _configuration.Webhook.Url = value; OnPropertyChanged(); }
    }

    // Original member: WebhookNotifyOnPowerLost
    public bool WebhookNotifyOnPowerLost
    {
        get => _configuration.Webhook.NotifyOnPowerLost;
        set { _configuration.Webhook.NotifyOnPowerLost = value; OnPropertyChanged(); }
    }

    // Original member: WebhookNotifyOnPowerRestored
    public bool WebhookNotifyOnPowerRestored
    {
        get => _configuration.Webhook.NotifyOnPowerRestored;
        set { _configuration.Webhook.NotifyOnPowerRestored = value; OnPropertyChanged(); }
    }

    // Original member: WebhookNotifyOnBatteryLow
    public bool WebhookNotifyOnBatteryLow
    {
        get => _configuration.Webhook.NotifyOnBatteryLow;
        set { _configuration.Webhook.NotifyOnBatteryLow = value; OnPropertyChanged(); }
    }

    // Original member: WebhookNotifyOnHighLoad
    public bool WebhookNotifyOnHighLoad
    {
        get => _configuration.Webhook.NotifyOnHighLoad;
        set { _configuration.Webhook.NotifyOnHighLoad = value; OnPropertyChanged(); }
    }

    // Original member: WebhookStatus
    public string WebhookStatus { get => _webhookStatus; private set => SetField(ref _webhookStatus, value); }

    // Original member: ExternalCommandEnabled
    public bool ExternalCommandEnabled
    {
        get => _configuration.ExternalCommand.Enabled;
        set { _configuration.ExternalCommand.Enabled = value; OnPropertyChanged(); }
    }

    // Original member: CommandOnPowerLost
    public string CommandOnPowerLost
    {
        get => _configuration.ExternalCommand.CommandOnPowerLost;
        set { _configuration.ExternalCommand.CommandOnPowerLost = value; OnPropertyChanged(); }
    }

    // Original member: CommandOnPowerRestored
    public string CommandOnPowerRestored
    {
        get => _configuration.ExternalCommand.CommandOnPowerRestored;
        set { _configuration.ExternalCommand.CommandOnPowerRestored = value; OnPropertyChanged(); }
    }

    // Original member: CommandOnBatteryLow
    public string CommandOnBatteryLow
    {
        get => _configuration.ExternalCommand.CommandOnBatteryLow;
        set { _configuration.ExternalCommand.CommandOnBatteryLow = value; OnPropertyChanged(); }
    }

    // Original member: CommandOnHighLoad
    public string CommandOnHighLoad
    {
        get => _configuration.ExternalCommand.CommandOnHighLoad;
        set { _configuration.ExternalCommand.CommandOnHighLoad = value; OnPropertyChanged(); }
    }

    // Original member: CommandOnVoltageAbnormal
    public string CommandOnVoltageAbnormal
    {
        get => _configuration.ExternalCommand.CommandOnVoltageAbnormal;
        set { _configuration.ExternalCommand.CommandOnVoltageAbnormal = value; OnPropertyChanged(); }
    }

    // Original member: LoadSimulationTargetWatts
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

    // Original member: SimulatedRuntimeText
    public string SimulatedRuntimeText { get => _simulatedRuntimeText; private set => SetField(ref _simulatedRuntimeText, value); }
    // Original member: StandardLoadEstimates
    public IReadOnlyList<RuntimeEstimateTableItem> StandardLoadEstimates { get => _standardLoadEstimates; private set => SetField(ref _standardLoadEstimates, value); }
    // Original member: LanguageOptions
    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new("ja-JP", "日本語"),
        new("en-US", "English"),
    ];

    // Original member: SaveSettingsCommand
    public ICommand SaveSettingsCommand { get; }

    // Original member: RecordRuntimeBaselineCommand
    public ICommand RecordRuntimeBaselineCommand { get; }

    // Original member: ClearRuntimeBaselineCommand
    public ICommand ClearRuntimeBaselineCommand { get; }

    // Original member: RecordOfficialHealthCommand
    public ICommand RecordOfficialHealthCommand { get; }

    // Original member: OfficialRecords
    public ObservableCollection<OfficialRecordRow> OfficialRecords { get; } = [];

    // Original member: OfficialModelRows
    public ObservableCollection<OfficialModelRow> OfficialModelRows { get; } = [];

    // Original member: OfficialErrorText
    public string OfficialErrorText
    {
        get => _officialErrorText;
        private set => SetField(ref _officialErrorText, value);
    }

    // Original member: _officialRecordCountText
    private string _officialRecordCountText = string.Empty;
    // Original member: OfficialRecordCountText
    public string OfficialRecordCountText
    {
        get => _officialRecordCountText;
        private set => SetField(ref _officialRecordCountText, value);
    }

    // Original member: OfficialRecordPercent
    public double? OfficialRecordPercent
    {
        get => _officialRecordPercent;
        set => SetField(ref _officialRecordPercent, value);
    }

    // Original member: OfficialRecordDateEnd
    public DateTime OfficialRecordDateEnd => DateTime.Today;

    // Original member: OfficialRecordDate
    public DateTime? OfficialRecordDate
    {
        get => _officialRecordDate;
        set => SetField(ref _officialRecordDate, value);
    }

    // Original member: OfficialRecordTime
    public string OfficialRecordTime
    {
        get => _officialRecordTime;
        set => SetField(ref _officialRecordTime, value ?? string.Empty);
    }

    // Original member: OfficialRecordSource
    public VendorBatteryHealthCategory OfficialRecordSource
    {
        get => _officialRecordSource;
        set => SetField(ref _officialRecordSource, value);
    }

    // Original member: OfficialRecordNote
    public string OfficialRecordNote
    {
        get => _officialRecordNote;
        set => SetField(ref _officialRecordNote, value ?? string.Empty);
    }

    // Original member: BatteryInstalledDate
    public DateTime? BatteryInstalledDate
    {
        get => _batteryInstalledDate;
        set
        {
            if (!SetField(ref _batteryInstalledDate, value?.Date) || _officialEditorLoading)
            {
                return;
            }

            _ = SaveBatteryInstalledDateAsync(_batteryInstalledDate);
        }
    }

    // Original member: MinimizeToTray
    public bool MinimizeToTray
    {
        get => _configuration.Ui.MinimizeToTray;
        set
        {
            _configuration.Ui.MinimizeToTray = value;
            OnPropertyChanged();
        }
    }

    // Original member: CloseToTray
    public bool CloseToTray
    {
        get => _configuration.Ui.CloseToTray;
        set
        {
            _configuration.Ui.CloseToTray = value;
            OnPropertyChanged();
        }
    }

    // Original member: StartMinimized
    public bool StartMinimized
    {
        get => _configuration.Ui.StartMinimized;
        set
        {
            _configuration.Ui.StartMinimized = value;
            OnPropertyChanged();
            if (RunOnStartup)
            {
                _effects.SetRunOnStartup(true, value);
            }
        }
    }

    // Original member: EnableNotifications
    public bool EnableNotifications
    {
        get => _configuration.Ui.EnableNotifications;
        set
        {
            _configuration.Ui.EnableNotifications = value;
            OnPropertyChanged();
        }
    }

    // Original member: RunOnStartup
    public bool RunOnStartup
    {
        get => _configuration.Ui.RunOnStartup;
        set
        {
            _configuration.Ui.RunOnStartup = value;
            _effects.SetRunOnStartup(value, StartMinimized);
            OnPropertyChanged();
        }
    }

    // Original member: ElectricityRatePerKwh
    public double ElectricityRatePerKwh
    {
        get => _configuration.Monitoring.ElectricityRatePerKwh;
        set
        {
            if (value is >= 0 and <= 1000)
            {
                _configuration.Monitoring.ElectricityRatePerKwh = value;
                OnPropertyChanged();
                _effects.RefreshPeriodCost();
            }
        }
    }

    // Original member: RuntimeLowSeconds
    public int RuntimeLowSeconds
    {
        get => _runtimeLowSeconds;
        set => SetField(ref _runtimeLowSeconds, value);
    }

    // Original member: PollIntervalMs
    public int PollIntervalMs
    {
        get => _pollIntervalMs;
        set => SetField(ref _pollIntervalMs, value);
    }

    // Original member: SelectedLanguageCode
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
            _effects.ApplyLanguage(value);
        }
    }

    // Original member: BatteryWarningThresholdPercent
    public double BatteryWarningThresholdPercent
    {
        get => _batteryWarningThresholdPercent;
        set => SetField(ref _batteryWarningThresholdPercent, value);
    }

    // Original member: BatteryCriticalThresholdPercent
    public double BatteryCriticalThresholdPercent
    {
        get => _batteryCriticalThresholdPercent;
        set => SetField(ref _batteryCriticalThresholdPercent, value);
    }

    // Original member: ComparableLoadTolerancePercent
    public double ComparableLoadTolerancePercent
    {
        get => _comparableLoadTolerancePercent;
        set => SetField(ref _comparableLoadTolerancePercent, value);
    }

    // Original member: BaselineModeOptions
    public IReadOnlyList<BatteryBaselineModeOption> BaselineModeOptions
    {
        get => _baselineModeOptions;
        private set => SetField(ref _baselineModeOptions, value);
    }

    // Original member: SelectedBaselineKind
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

    // Original member: KnownHealthPercent
    public double KnownHealthPercent
    {
        get => _knownHealthPercent;
        set => SetField(ref _knownHealthPercent, value);
    }

    // Original member: KnownHealthSource
    public string KnownHealthSource
    {
        get => _knownHealthSource;
        set => SetField(ref _knownHealthSource, value);
    }

    // Original member: VendorHealthCategoryOptions
    public IReadOnlyList<VendorHealthCategoryOption> VendorHealthCategoryOptions
    {
        get => _vendorHealthCategoryOptions;
        private set => SetField(ref _vendorHealthCategoryOptions, value);
    }

    // Original member: SelectedVendorHealthCategory
    public VendorBatteryHealthCategory SelectedVendorHealthCategory
    {
        get => _selectedVendorHealthCategory;
        set => SetField(ref _selectedVendorHealthCategory, value);
    }

    // Original member: BaselineInstructionText
    public string BaselineInstructionText => L(SelectedBaselineKind switch
    {
        BatteryRuntimeBaselineKind.NewBattery => "BaselineInstructionNew",
        BatteryRuntimeBaselineKind.KnownHealthAnchor => "BaselineInstructionKnown",
        _ => "BaselineInstructionRelative",
    });

    // Original member: SettingsStatus
    public string SettingsStatus { get => _settingsStatus; private set => SetField(ref _settingsStatus, value); }
    // Original member: RefreshThemeOptions
    private void RefreshThemeOptions()
    {
        ThemeOptions = LocalizedOption.UpdateLabels(ThemeOptions,
        [
            new("system", L("ThemeSystem")),
            new("dark", L("ThemeDark")),
            new("light", L("ThemeLight")),
        ]);
    }

    // Original member: MigrateLegacyOfficialRecords
    private void MigrateLegacyOfficialRecords()
    {
        var migrated = false;
        foreach (var profile in _configuration.BatteryHealth.Profiles)
        {
            migrated |= OfficialHealthEstimator.TryMigrateLegacyAnchor(profile);
        }

        if (migrated)
        {
            _ = SaveConfigurationQuietlyAsync();
        }
    }

    // 起動時に1回、履歴取得が未完了の記録を補完する。保存済みの現在値は維持する。
    // Original member: BackfillOfficialContextsAsync
    private async Task BackfillOfficialContextsAsync()
    {
        if (_historyStore is null)
        {
            return;
        }

        try
        {
            var changed = false;
            foreach (var profile in _configuration.BatteryHealth.Profiles.ToArray())
            {
                if (!profile.OfficialHealthRecords.Any(item => !item.ContextBackfillAttempted))
                {
                    continue;
                }

                changed |= await OfficialHealthContextService
                    .BackfillAsync(profile.OfficialHealthRecords, profile.DeviceId, _historyStore)
                    .ConfigureAwait(false) > 0;
            }

            if (changed)
            {
                await _save(_configuration).ConfigureAwait(false);
                await _dispatch(() =>
                {
                    if (_lastSnapshot?.Device is { } device)
                    {
                        RefreshOfficialEditor(FindHealthProfile(device));
                    }
                });
            }
        }
        catch (Exception exception)
        {
            SetCommandError(exception);
        }
    }

    // Original member: CaptureLiveContext
    private OfficialHealthContext? CaptureLiveContext()
    {
        var snapshot = _lastSnapshot;
        var telemetry = _lastTelemetry;
        if (snapshot is null || telemetry is not { IsConnected: true })
        {
            return null;
        }

        return new OfficialHealthContext
        {
            LoadPercent = telemetry.LoadPercent.IsValid ? telemetry.LoadPercent.Value : null,
            ActivePowerWatts = telemetry.ActivePowerWatts.IsValid ? telemetry.ActivePowerWatts.Value : null,
            RuntimeSeconds = telemetry.RuntimeRemaining.IsValid ? telemetry.RuntimeRemaining.Value?.TotalSeconds : null,
            BatteryVoltage = telemetry.BatteryVoltage.IsValid ? telemetry.BatteryVoltage.Value : null,
            InputVoltage = snapshot.InputVoltage,
            TemperatureCelsius = snapshot.Temperature,
            Charging = snapshot.Charging,
        };
    }

    // Original member: SaveConfigurationQuietlyAsync
    private async Task SaveConfigurationQuietlyAsync()
    {
        try
        {
            await _save(_configuration);
        }
        catch (Exception exception)
        {
            SetCommandError(exception);
        }
    }

    // Original member: RecordOfficialHealthAsync
    private async Task RecordOfficialHealthAsync()
    {
        if (_lastSnapshot?.Device is not { } device)
        {
            SettingsStatus = L("OfficialRecordRequiresUps");
            return;
        }

        if (OfficialRecordPercent is not { } percent || !double.IsFinite(percent) || percent is < 0 or > 100)
        {
            SettingsStatus = L("OfficialRecordValueInvalid");
            return;
        }

        var resolvedTime = OfficialRecordTimeResolver.Resolve(
            OfficialRecordDate, OfficialRecordTime, DateTimeOffset.Now);
        if (resolvedTime.RecordedAt is not { } recordedAt)
        {
            SettingsStatus = L(resolvedTime.Error == OfficialRecordTimeError.InvalidTimeFormat
                ? "OfficialRecordTimeInvalid"
                : "OfficialRecordFutureInvalid");
            return;
        }

        var profile = FindHealthProfile(device);
        if (profile is null)
        {
            profile = new BatteryHealthProfile { DeviceId = DeviceId(device) };
            _configuration.BatteryHealth.Profiles.Add(profile);
        }

        var newRecord = new OfficialHealthRecord
        {
            RecordedAt = recordedAt,
            HealthPercent = percent,
            Source = OfficialRecordSource,
            Note = string.IsNullOrWhiteSpace(OfficialRecordNote) ? null : OfficialRecordNote.Trim(),
        };
        // 履歴取得が未完了なら、現在値を保存できても次回起動時の補完対象に残す。
        await OfficialHealthContextService.CaptureForRecordAsync(
            newRecord,
            _historyStore,
            profile.DeviceId,
            DateTimeOffset.Now,
            CaptureLiveContext());
        var record = OfficialHealthEstimator.AddRecord(
            profile.OfficialHealthRecords,
            profile.BatteryInstalledAt,
            newRecord);

        await _save(_configuration);
        RefreshOfficialEditor(profile);
        SettingsStatus = LocalizationManager.Format(
            "OfficialRecordSavedFormat",
            record.HealthPercent,
            record.RecordedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.CurrentCulture));
        ApplySnapshot(_lastSnapshot);
    }

    // Original member: DeleteOfficialRecordAsync
    private async Task DeleteOfficialRecordAsync(OfficialHealthRecord record)
    {
        if (_lastSnapshot?.Device is not { } device || FindHealthProfile(device) is not { } profile)
        {
            return;
        }

        // 他の記録の PredictedPercent は再計算せず、記録時点の値を保持する。
        if (!profile.OfficialHealthRecords.Remove(record))
        {
            return;
        }

        await _save(_configuration);
        RefreshOfficialEditor(profile);
        SettingsStatus = L("OfficialRecordDeleted");
        ApplySnapshot(_lastSnapshot);
    }

    // Original member: SaveBatteryInstalledDateAsync
    private async Task SaveBatteryInstalledDateAsync(DateTime? date)
    {
        try
        {
            if (_lastSnapshot?.Device is not { } device)
            {
                SettingsStatus = L("OfficialRecordRequiresUps");
                return;
            }

            var profile = FindHealthProfile(device);
            if (profile is null)
            {
                if (date is null)
                {
                    return;
                }

                profile = new BatteryHealthProfile { DeviceId = DeviceId(device) };
                _configuration.BatteryHealth.Profiles.Add(profile);
            }

            profile.BatteryInstalledAt = date is { } value ? new DateTimeOffset(value.Date) : null;
            await _save(_configuration);
            SettingsStatus = L("OfficialInstalledSaved");
            ApplySnapshot(_lastSnapshot);
        }
        catch (Exception exception)
        {
            SetCommandError(exception);
        }
    }

    // Original member: RefreshOfficialEditor
    private void RefreshOfficialEditor(BatteryHealthProfile? profile)
    {
        _officialEditorLoading = true;
        try
        {
            BatteryInstalledDate = profile?.BatteryInstalledAt?.ToLocalTime().Date;
        }
        finally
        {
            _officialEditorLoading = false;
        }

        OfficialRecords.Clear();
        var records = profile?.OfficialHealthRecords ?? [];
        foreach (var record in records.OrderBy(item => item.RecordedAt))
        {
            var predicted = record.PredictedPercent;
            OfficialRecords.Add(new OfficialRecordRow(
                record,
                record.RecordedAt.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.CurrentCulture),
                $"{record.HealthPercent:0.#}%",
                predicted is { } value ? $"{value:0.#}%" : "—",
                FormatOfficialError(record.HealthPercent, predicted),
                FormatOfficialDetail(record),
                new AsyncRelayCommand(() => DeleteOfficialRecordAsync(record), SetCommandError)));
        }

        OfficialErrorText = OfficialHealthEstimator.MeanAbsoluteError(records) is { } mae
            ? LocalizationManager.Format("OfficialRecordMaeFormat", mae)
            : "—";
        RefreshOfficialModelRows(profile);
    }

    // Original member: RefreshOfficialModelRows
    private void RefreshOfficialModelRows(BatteryHealthProfile? profile)
    {
        _lastOfficialModelRefresh = DateTimeOffset.Now;
        OfficialModelRows.Clear();
        var records = profile?.OfficialHealthRecords ?? [];
        OfficialRecordCountText = LocalizationManager.Format("OfficialRecordCountFormat",
            records.Count, OfficialHealthEstimator.GroupRecords(records).Length);
        if (profile is null
            || OfficialHealthEstimator.Estimate(profile.OfficialHealthRecords, profile.BatteryInstalledAt, DateTimeOffset.Now)
                is not { } estimate)
        {
            return;
        }

        foreach (var result in estimate.Models)
        {
            OfficialModelRows.Add(new OfficialModelRow(
                LocalizeOfficialModel(result.Model),
                result.Percent is { } value ? $"{value:0.0}%" : L("OfficialModelUnavailable"),
                result.BacktestCount > 0 ? result.BacktestCount.ToString(CultureInfo.CurrentCulture) : "—",
                result.MeanAbsoluteError is { } mae ? $"{mae:0.00}" : "—",
                result.Model == estimate.Model ? L("OfficialModelSelected") : string.Empty));
        }
    }

    // Original member: LocalizeOfficialModel
    private static string LocalizeOfficialModel(OfficialHealthModel model) => L(model switch
    {
        OfficialHealthModel.InstallAnchoredLinear => "OfficialModelInstallAnchoredLinear",
        OfficialHealthModel.AcceleratingPower => "OfficialModelAcceleratingPower",
        _ => "OfficialModelRecentLinear",
    });

    // Original member: FormatOfficialError
    private static string FormatOfficialError(double actual, double? predicted)
    {
        if (predicted is not { } value)
        {
            return "—";
        }

        var error = actual - value;
        var text = error.ToString("+0.0;-0.0;0.0", CultureInfo.CurrentCulture);
        return OfficialHealthEstimator.IsWithinRounding(error)
            ? $"{text} ({L("OfficialRecordWithinRounding")})"
            : text;
    }

    // 記録一覧の補足1行: メモ + 記録時の状況の要約。無い項目は省略し、全部無ければ空。
    // Original member: FormatOfficialDetail
    private static string FormatOfficialDetail(OfficialHealthRecord record)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(record.Note))
        {
            parts.Add(record.Note.Trim());
        }

        if (record.Context is { } context)
        {
            var load = new List<string>();
            if (context.LoadPercent is { } loadPercent)
            {
                load.Add(LocalizationManager.Format("OfficialContextLoad", loadPercent));
            }

            if (context.ActivePowerWatts is { } watts)
            {
                load.Add(LocalizationManager.Format("OfficialContextPower", watts));
            }

            var items = new List<string>();
            if (load.Count > 0)
            {
                items.Add(string.Join(" / ", load));
            }

            if (context.RuntimeSeconds is { } runtimeSeconds && runtimeSeconds > 0)
            {
                items.Add(LocalizationManager.Format("OfficialContextRuntime", FormatDuration(TimeSpan.FromSeconds(runtimeSeconds))));
            }

            if (context.BatteryVoltage is { } batteryVoltage)
            {
                items.Add(LocalizationManager.Format("OfficialContextBatteryVoltage", batteryVoltage));
            }

            if (context.InputVoltage is { } inputVoltage)
            {
                items.Add(LocalizationManager.Format("OfficialContextInputVoltage", inputVoltage));
            }

            if (context.TemperatureCelsius is { } temperature)
            {
                items.Add(LocalizationManager.Format("OfficialContextTemperature", temperature));
            }

            if (context.Charging is true)
            {
                items.Add(L("OfficialContextCharging"));
            }

            if (context.Load24HourMean is { } dayMean)
            {
                items.Add(LocalizationManager.Format("OfficialContextLoad24h", dayMean));
            }

            if (context.DaysSinceLastDischarge is { } days)
            {
                items.Add(LocalizationManager.Format("OfficialContextLastDischarge", days));
            }
            else if (context.HistoryStartedAt is { } started && items.Count > 0)
            {
                items.Add(LocalizationManager.Format("OfficialContextNoDischarge", FormatOfficialDate(started)));
            }

            if (items.Count > 0)
            {
                parts.Add(string.Join(L("OfficialContextSeparator"), items));
            }
        }

        return string.Join("  |  ", parts);
    }

    // Original member: RecordRuntimeBaselineAsync
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

        await _save(_configuration);
        SettingsStatus = LocalizationManager.Format(
            "BaselineRecordedFormat",
            load,
            FormatDuration(runtime),
            LocalizeBaselineKind(SelectedBaselineKind));
        RefreshOfficialEditor(profile);
        ApplySnapshot(_lastSnapshot);
    }

    // Original member: ClearRuntimeBaselineAsync
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
        await _save(_configuration);
        SettingsStatus = L("BaselineCleared");
        ApplySnapshot(_lastSnapshot);
    }

    // Original member: RecalculateSimulation
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

    // Original member: SaveSettingsAsync
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

        await _save(_configuration);
        _effects.SetPollInterval(PollIntervalMs);
        _effects.SetRuntimeLowThreshold(TimeSpan.FromSeconds(RuntimeLowSeconds));
        _effects.SetAlertThresholds(_configuration.Alerts.ToAlertThresholds());
        SettingsStatus = LocalizationManager.Format("SettingsSavedFormat", DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        if (_lastSnapshot is { } snapshot)
        {
            ApplySnapshot(snapshot);
        }
    }

    // Original member: SetCommandError
    private void SetCommandError(Exception exception)
    {
        SettingsStatus = LocalizationManager.Format("SettingsSaveErrorFormat", exception.Message);
        LastError = exception.Message;
    }

    // Original member: L
    private static string L(string key) => LocalizationManager.Get(key);

    // Original member: FormatDuration
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

    // Original member: FindHealthProfile
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

    // Original member: DeviceId
    private static string DeviceId(UpsDeviceInfo device) =>
        UpsDeviceIdentity.Create(device);

    // Original member: CreateHealthOptions
    private BatteryHealthOptions CreateHealthOptions() => AreHealthSettingsValid()
        ? new BatteryHealthOptions
        {
            WarningThresholdPercent = BatteryWarningThresholdPercent,
            CriticalThresholdPercent = BatteryCriticalThresholdPercent,
            ComparableLoadTolerancePercent = ComparableLoadTolerancePercent,
        }
        : new BatteryHealthOptions();

    // Original member: AreHealthSettingsValid
    private bool AreHealthSettingsValid() =>
        BatteryCriticalThresholdPercent is >= 1 and < 100
        && BatteryWarningThresholdPercent > BatteryCriticalThresholdPercent
        && BatteryWarningThresholdPercent <= 100
        && ComparableLoadTolerancePercent is >= 0 and <= 25;

    // Original member: LocalizeVendorHealthCategory
    private static string LocalizeVendorHealthCategory(VendorBatteryHealthCategory category) => L(category switch
    {
        VendorBatteryHealthCategory.Good => "VendorHealthCategoryGood",
        VendorBatteryHealthCategory.Average => "VendorHealthCategoryAverage",
        VendorBatteryHealthCategory.BelowAverage => "VendorHealthCategoryBelowAverage",
        VendorBatteryHealthCategory.Poor => "VendorHealthCategoryPoor",
        _ => "VendorHealthCategoryUnknown",
    });

    // Original member: FormatOfficialDate
    private static string FormatOfficialDate(DateTimeOffset value) =>
        value.ToLocalTime().ToString("yyyy/MM/dd", CultureInfo.CurrentCulture);

    // Original member: RefreshBaselineModeOptions
    private void RefreshBaselineModeOptions()
    {
        BaselineModeOptions = LocalizedOption.UpdateLabels(BaselineModeOptions,
        [
            new(BatteryRuntimeBaselineKind.CurrentRelative, L("BaselineModeRelative")),
            new(BatteryRuntimeBaselineKind.KnownHealthAnchor, L("BaselineModeKnown")),
            new(BatteryRuntimeBaselineKind.NewBattery, L("BaselineModeNew")),
        ]);
    }

    // Original member: RefreshVendorHealthCategoryOptions
    private void RefreshVendorHealthCategoryOptions()
    {
        VendorHealthCategoryOptions = LocalizedOption.UpdateLabels(VendorHealthCategoryOptions,
        [
            new(VendorBatteryHealthCategory.Unknown, LocalizeVendorHealthCategory(VendorBatteryHealthCategory.Unknown)),
            new(VendorBatteryHealthCategory.Good, LocalizeVendorHealthCategory(VendorBatteryHealthCategory.Good)),
            new(VendorBatteryHealthCategory.Average, LocalizeVendorHealthCategory(VendorBatteryHealthCategory.Average)),
            new(VendorBatteryHealthCategory.BelowAverage, LocalizeVendorHealthCategory(VendorBatteryHealthCategory.BelowAverage)),
            new(VendorBatteryHealthCategory.Poor, LocalizeVendorHealthCategory(VendorBatteryHealthCategory.Poor)),
        ]);
    }

    // Original member: InitializeBaselineEditor
    private void InitializeBaselineEditor(BatteryHealthProfile? profile)
    {
        if (_baselineEditorInitialized || profile is null)
        {
            return;
        }

        RefreshOfficialEditor(profile);

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

    // Original member: LocalizeBaselineKind
    private static string LocalizeBaselineKind(BatteryRuntimeBaselineKind kind) => L(kind switch
    {
        BatteryRuntimeBaselineKind.NewBattery => "BaselineModeNew",
        BatteryRuntimeBaselineKind.KnownHealthAnchor => "BaselineModeKnown",
        BatteryRuntimeBaselineKind.CurrentRelative => "BaselineModeRelative",
        _ => "BaselineSummaryModeLegacy",
    });

    // Original member: SetField
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

    // Original member: SetField
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

    // Original member: OnPropertyChanged
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));}
