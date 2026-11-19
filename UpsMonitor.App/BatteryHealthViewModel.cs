using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

namespace UpsMonitor.App;

/// <summary>Battery records, baselines and model editor. Uses raw telemetry for recording.</summary>
public sealed class BatteryHealthViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AppConfiguration _configuration;
    private readonly SqliteTelemetryStore? _historyStore;
    private readonly Func<AppConfiguration,Task> _save;
    private readonly Func<Action,Task> _dispatch;
    private readonly Action<string> _setStatus;
    private readonly Action<Exception> _onError;
    private readonly Action _reapply;
    private bool _disposed;
    private UpsSnapshot? _lastSnapshot;
    private UpsTelemetry? _lastTelemetry;
    private string SettingsStatus { set { if (!_disposed) _setStatus(value); } }
    public BatteryHealthViewModel(AppConfiguration configuration, Func<AppConfiguration,Task> save, SqliteTelemetryStore? historyStore,
        Func<Action,Task> dispatch, Action<string> setStatus, Action<Exception> onError, Action reapply)
    {
        _configuration=configuration; _save=save; _historyStore=historyStore; _dispatch=dispatch;
        _setStatus=setStatus; _onError=onError; _reapply=reapply;
        _batteryWarningThresholdPercent=configuration.BatteryHealth.WarningThresholdPercent;
        _batteryCriticalThresholdPercent=configuration.BatteryHealth.CriticalThresholdPercent;
        _comparableLoadTolerancePercent=configuration.BatteryHealth.ComparableLoadTolerancePercent;
        RefreshBaselineModeOptions(); RefreshVendorHealthCategoryOptions();
        RecordRuntimeBaselineCommand=new AsyncRelayCommand(RecordRuntimeBaselineAsync,SetCommandError);
        ClearRuntimeBaselineCommand=new AsyncRelayCommand(ClearRuntimeBaselineAsync,SetCommandError);
        RecordOfficialHealthCommand=new AsyncRelayCommand(RecordOfficialHealthAsync,SetCommandError);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Initialize() { if (_disposed) return; MigrateLegacyOfficialRecords(); _ = BackfillOfficialContextsAsync(); }
    public void SetSnapshot(UpsSnapshot snapshot, UpsTelemetry telemetry)
    { _lastSnapshot=snapshot; _lastTelemetry=telemetry; OnPropertyChanged(nameof(OfficialRecordDateEnd)); }
    public void RefreshModelIfDue()
    {
        var profile=FindHealthProfile(_lastSnapshot?.Device);
        if (profile is not null && profile.OfficialHealthRecords.Count>0 && DateTimeOffset.Now-_lastOfficialModelRefresh>=TimeSpan.FromMinutes(1))
            RefreshOfficialModelRows(profile);
    }
    public void RefreshLanguage()
    {
        if (_disposed) return;
        RefreshBaselineModeOptions(); RefreshVendorHealthCategoryOptions();
        OnPropertyChanged(nameof(BaselineInstructionText));
        RefreshOfficialEditor(FindHealthProfile(_lastSnapshot?.Device));
    }
    public void Dispose() => _disposed=true;
    private void SetCommandError(Exception exception) { if (!_disposed) _onError(exception); }
    private void ApplySnapshot(UpsSnapshot snapshot) { if (!_disposed) _reapply(); }
    private double? _officialRecordPercent;
    private DateTime? _officialRecordDate = DateTime.Today;
    private string _officialRecordTime = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture);
    private VendorBatteryHealthCategory _officialRecordSource = VendorBatteryHealthCategory.Unknown;
    private string _officialRecordNote = string.Empty;
    private DateTime? _batteryInstalledDate;
    private string _officialErrorText = "—";
    private bool _officialEditorLoading;
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
    private double _loadSimulationTargetWatts = 200.0;
    private string _simulatedRuntimeText = "-";
    private IReadOnlyList<RuntimeEstimateTableItem> _standardLoadEstimates = [];

    private DateTimeOffset _lastOfficialModelRefresh = DateTimeOffset.MinValue;
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
    public ICommand RecordRuntimeBaselineCommand { get; }

    public ICommand ClearRuntimeBaselineCommand { get; }

    public ICommand RecordOfficialHealthCommand { get; }

    public ObservableCollection<OfficialRecordRow> OfficialRecords { get; } = [];

    public ObservableCollection<OfficialModelRow> OfficialModelRows { get; } = [];

    public string OfficialErrorText
    {
        get => _officialErrorText;
        private set => SetField(ref _officialErrorText, value);
    }

    private string _officialRecordCountText = string.Empty;
    public string OfficialRecordCountText
    {
        get => _officialRecordCountText;
        private set => SetField(ref _officialRecordCountText, value);
    }

    public double? OfficialRecordPercent
    {
        get => _officialRecordPercent;
        set => SetField(ref _officialRecordPercent, value);
    }

    public DateTime OfficialRecordDateEnd => DateTime.Today;

    public DateTime? OfficialRecordDate
    {
        get => _officialRecordDate;
        set => SetField(ref _officialRecordDate, value);
    }

    public string OfficialRecordTime
    {
        get => _officialRecordTime;
        set => SetField(ref _officialRecordTime, value ?? string.Empty);
    }

    public VendorBatteryHealthCategory OfficialRecordSource
    {
        get => _officialRecordSource;
        set => SetField(ref _officialRecordSource, value);
    }

    public string OfficialRecordNote
    {
        get => _officialRecordNote;
        set => SetField(ref _officialRecordNote, value ?? string.Empty);
    }

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
    public async Task BackfillOfficialContextsAsync()
    {
        if (_disposed) return;
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

    public async Task RecordOfficialHealthAsync()
    {
        if (_disposed) return;
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

    public async Task DeleteOfficialRecordAsync(OfficialHealthRecord record)
    {
        if (_disposed) return;
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

    public async Task SaveBatteryInstalledDateAsync(DateTime? date)
    {
        if (_disposed) return;
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

    public void RefreshOfficialEditor(BatteryHealthProfile? profile)
    {
        if (_disposed) return;
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

    private void RefreshOfficialModelRows(BatteryHealthProfile? profile)
    {
        if (_disposed) return;
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

    public static string LocalizeOfficialModel(OfficialHealthModel model) => L(model switch
    {
        OfficialHealthModel.InstallAnchoredLinear => "OfficialModelInstallAnchoredLinear",
        OfficialHealthModel.AcceleratingPower => "OfficialModelAcceleratingPower",
        _ => "OfficialModelRecentLinear",
    });

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

    public async Task RecordRuntimeBaselineAsync()
    {
        if (_disposed) return;
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

    public async Task ClearRuntimeBaselineAsync()
    {
        if (_disposed) return;
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

    public void RecalculateSimulation()
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

    private static string L(string key) => LocalizationManager.Get(key);

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

    public BatteryHealthProfile? FindHealthProfile(UpsDeviceInfo? device)
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

    public BatteryHealthOptions CreateHealthOptions() => AreHealthSettingsValid()
        ? new BatteryHealthOptions
        {
            WarningThresholdPercent = BatteryWarningThresholdPercent,
            CriticalThresholdPercent = BatteryCriticalThresholdPercent,
            ComparableLoadTolerancePercent = ComparableLoadTolerancePercent,
        }
        : new BatteryHealthOptions();

    public bool AreHealthSettingsValid() =>
        BatteryCriticalThresholdPercent is >= 1 and < 100
        && BatteryWarningThresholdPercent > BatteryCriticalThresholdPercent
        && BatteryWarningThresholdPercent <= 100
        && ComparableLoadTolerancePercent is >= 0 and <= 25;

    public static string LocalizeVendorHealthCategory(VendorBatteryHealthCategory category) => L(category switch
    {
        VendorBatteryHealthCategory.Good => "VendorHealthCategoryGood",
        VendorBatteryHealthCategory.Average => "VendorHealthCategoryAverage",
        VendorBatteryHealthCategory.BelowAverage => "VendorHealthCategoryBelowAverage",
        VendorBatteryHealthCategory.Poor => "VendorHealthCategoryPoor",
        _ => "VendorHealthCategoryUnknown",
    });

    private static string FormatOfficialDate(DateTimeOffset value) =>
        value.ToLocalTime().ToString("yyyy/MM/dd", CultureInfo.CurrentCulture);

    private void RefreshBaselineModeOptions()
    {
        BaselineModeOptions = LocalizedOption.UpdateLabels(BaselineModeOptions,
        [
            new(BatteryRuntimeBaselineKind.CurrentRelative, L("BaselineModeRelative")),
            new(BatteryRuntimeBaselineKind.KnownHealthAnchor, L("BaselineModeKnown")),
            new(BatteryRuntimeBaselineKind.NewBattery, L("BaselineModeNew")),
        ]);
    }

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

    public void InitializeBaselineEditor(BatteryHealthProfile? profile)
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

    public static string LocalizeBaselineKind(BatteryRuntimeBaselineKind kind) => L(kind switch
    {
        BatteryRuntimeBaselineKind.NewBattery => "BaselineModeNew",
        BatteryRuntimeBaselineKind.KnownHealthAnchor => "BaselineModeKnown",
        BatteryRuntimeBaselineKind.CurrentRelative => "BaselineModeRelative",
        _ => "BaselineSummaryModeLegacy",
    });

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

    private void Notify(string? propertyName) { if (!_disposed) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName)); }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        Notify(propertyName);
}
