using System.Collections.ObjectModel;
using System.Windows.Input;
using UpsMonitor.App;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;
namespace UpsMonitor.App;
// Composition harness only; all behavior runs in the source-linked production VMs.
internal sealed class SettingsHealthTestAdapter : IDisposable
{
    public SettingsViewModel Settings { get; }
    public BatteryHealthViewModel BatteryHealth { get; }
    public string LastError { get; private set; } = "";
    public SettingsHealthTestAdapter(AppConfiguration config, Func<AppConfiguration,Task> save, SettingsEffects effects, SqliteTelemetryStore? store = null)
    {
        BatteryHealth=new BatteryHealthViewModel(config,save,store,action=>{action();return Task.CompletedTask;},
            SetStatus,OnError,effects.ReapplySnapshot);
        Settings=new SettingsViewModel(config,save,effects,BatteryHealth,exception=>LastError=exception.Message,
            LocalizationManager.Language,_=>Task.FromResult(true));
    }
    private void SetStatus(string status) => Settings.SetStatus(status);
    private void OnError(Exception error) => Settings.SetCommandError(error);
    public void SetSnapshot(UpsSnapshot snapshot) => BatteryHealth.SetSnapshot(snapshot,UpsTelemetryValidator.Normalize(snapshot));
    public Task Save() => Settings.SaveSettingsAsync();
    public Task Record() => BatteryHealth.RecordOfficialHealthAsync();
    public Task Delete(OfficialHealthRecord record) => BatteryHealth.DeleteOfficialRecordAsync(record);
    public Task Baseline() => BatteryHealth.RecordRuntimeBaselineAsync();
    public Task ClearBaseline() => BatteryHealth.ClearRuntimeBaselineAsync();
    public Task Backfill() => BatteryHealth.BackfillOfficialContextsAsync();
    public Task Installed(DateTime? date) => BatteryHealth.SaveBatteryInstalledDateAsync(date);
    public void Language() { Settings.RefreshLanguage(LocalizationManager.Language); BatteryHealth.RefreshLanguage(); }
    public void Dispose() { Settings.Dispose(); BatteryHealth.Dispose(); }
    public string WebhookStatus { get => Settings.WebhookStatus; }

    public string SimulatedRuntimeText { get => BatteryHealth.SimulatedRuntimeText; }

    public IReadOnlyList<RuntimeEstimateTableItem> StandardLoadEstimates { get => BatteryHealth.StandardLoadEstimates; }

    public IReadOnlyList<LanguageOption> LanguageOptions { get => Settings.LanguageOptions; }

    public ICommand SaveSettingsCommand { get => Settings.SaveSettingsCommand; }

    public ICommand RecordRuntimeBaselineCommand { get => BatteryHealth.RecordRuntimeBaselineCommand; }

    public ICommand ClearRuntimeBaselineCommand { get => BatteryHealth.ClearRuntimeBaselineCommand; }

    public ICommand RecordOfficialHealthCommand { get => BatteryHealth.RecordOfficialHealthCommand; }

    public ObservableCollection<OfficialRecordRow> OfficialRecords { get => BatteryHealth.OfficialRecords; }

    public ObservableCollection<OfficialModelRow> OfficialModelRows { get => BatteryHealth.OfficialModelRows; }

    public DateTime OfficialRecordDateEnd { get => BatteryHealth.OfficialRecordDateEnd; }

    public string BaselineInstructionText { get => BatteryHealth.BaselineInstructionText; }

    public string SettingsStatus { get => Settings.SettingsStatus; }

    public string SelectedTheme { get => Settings.SelectedTheme; set => Settings.SelectedTheme=value; }

    public IReadOnlyList<ThemeOption> ThemeOptions { get => Settings.ThemeOptions; }

    public bool EnableSoundAlerts { get => Settings.EnableSoundAlerts; set => Settings.EnableSoundAlerts=value; }

    public double HighLoadWarningPercent { get => Settings.HighLoadWarningPercent; set => Settings.HighLoadWarningPercent=value; }

    public double LowVoltageWarningThreshold { get => Settings.LowVoltageWarningThreshold; set => Settings.LowVoltageWarningThreshold=value; }

    public double HighVoltageWarningThreshold { get => Settings.HighVoltageWarningThreshold; set => Settings.HighVoltageWarningThreshold=value; }

    public bool WebhookEnabled { get => Settings.WebhookEnabled; set => Settings.WebhookEnabled=value; }

    public string WebhookUrl { get => Settings.WebhookUrl; set => Settings.WebhookUrl=value; }

    public bool WebhookNotifyOnPowerLost { get => Settings.WebhookNotifyOnPowerLost; set => Settings.WebhookNotifyOnPowerLost=value; }

    public bool WebhookNotifyOnPowerRestored { get => Settings.WebhookNotifyOnPowerRestored; set => Settings.WebhookNotifyOnPowerRestored=value; }

    public bool WebhookNotifyOnBatteryLow { get => Settings.WebhookNotifyOnBatteryLow; set => Settings.WebhookNotifyOnBatteryLow=value; }

    public bool WebhookNotifyOnHighLoad { get => Settings.WebhookNotifyOnHighLoad; set => Settings.WebhookNotifyOnHighLoad=value; }

    public bool ExternalCommandEnabled { get => Settings.ExternalCommandEnabled; set => Settings.ExternalCommandEnabled=value; }

    public string CommandOnPowerLost { get => Settings.CommandOnPowerLost; set => Settings.CommandOnPowerLost=value; }

    public string CommandOnPowerRestored { get => Settings.CommandOnPowerRestored; set => Settings.CommandOnPowerRestored=value; }

    public string CommandOnBatteryLow { get => Settings.CommandOnBatteryLow; set => Settings.CommandOnBatteryLow=value; }

    public string CommandOnHighLoad { get => Settings.CommandOnHighLoad; set => Settings.CommandOnHighLoad=value; }

    public string CommandOnVoltageAbnormal { get => Settings.CommandOnVoltageAbnormal; set => Settings.CommandOnVoltageAbnormal=value; }

    public double LoadSimulationTargetWatts { get => BatteryHealth.LoadSimulationTargetWatts; set => BatteryHealth.LoadSimulationTargetWatts=value; }

    public string OfficialErrorText { get => BatteryHealth.OfficialErrorText; }

    public string OfficialRecordCountText { get => BatteryHealth.OfficialRecordCountText; }

    public double? OfficialRecordPercent { get => BatteryHealth.OfficialRecordPercent; set => BatteryHealth.OfficialRecordPercent=value; }

    public DateTime? OfficialRecordDate { get => BatteryHealth.OfficialRecordDate; set => BatteryHealth.OfficialRecordDate=value; }

    public string OfficialRecordTime { get => BatteryHealth.OfficialRecordTime; set => BatteryHealth.OfficialRecordTime=value; }

    public VendorBatteryHealthCategory OfficialRecordSource { get => BatteryHealth.OfficialRecordSource; set => BatteryHealth.OfficialRecordSource=value; }

    public string OfficialRecordNote { get => BatteryHealth.OfficialRecordNote; set => BatteryHealth.OfficialRecordNote=value; }

    public DateTime? BatteryInstalledDate { get => BatteryHealth.BatteryInstalledDate; set => BatteryHealth.BatteryInstalledDate=value; }

    public bool MinimizeToTray { get => Settings.MinimizeToTray; set => Settings.MinimizeToTray=value; }

    public bool CloseToTray { get => Settings.CloseToTray; set => Settings.CloseToTray=value; }

    public bool StartMinimized { get => Settings.StartMinimized; set => Settings.StartMinimized=value; }

    public bool EnableNotifications { get => Settings.EnableNotifications; set => Settings.EnableNotifications=value; }

    public bool RunOnStartup { get => Settings.RunOnStartup; set => Settings.RunOnStartup=value; }

    public double ElectricityRatePerKwh { get => Settings.ElectricityRatePerKwh; set => Settings.ElectricityRatePerKwh=value; }

    public int RuntimeLowSeconds { get => Settings.RuntimeLowSeconds; set => Settings.RuntimeLowSeconds=value; }

    public int PollIntervalMs { get => Settings.PollIntervalMs; set => Settings.PollIntervalMs=value; }

    public string SelectedLanguageCode { get => Settings.SelectedLanguageCode; set => Settings.SelectedLanguageCode=value; }

    public double BatteryWarningThresholdPercent { get => BatteryHealth.BatteryWarningThresholdPercent; set => BatteryHealth.BatteryWarningThresholdPercent=value; }

    public double BatteryCriticalThresholdPercent { get => BatteryHealth.BatteryCriticalThresholdPercent; set => BatteryHealth.BatteryCriticalThresholdPercent=value; }

    public double ComparableLoadTolerancePercent { get => BatteryHealth.ComparableLoadTolerancePercent; set => BatteryHealth.ComparableLoadTolerancePercent=value; }

    public IReadOnlyList<BatteryBaselineModeOption> BaselineModeOptions { get => BatteryHealth.BaselineModeOptions; }

    public BatteryRuntimeBaselineKind SelectedBaselineKind { get => BatteryHealth.SelectedBaselineKind; set => BatteryHealth.SelectedBaselineKind=value; }

    public double KnownHealthPercent { get => BatteryHealth.KnownHealthPercent; set => BatteryHealth.KnownHealthPercent=value; }

    public string KnownHealthSource { get => BatteryHealth.KnownHealthSource; set => BatteryHealth.KnownHealthSource=value; }

    public IReadOnlyList<VendorHealthCategoryOption> VendorHealthCategoryOptions { get => BatteryHealth.VendorHealthCategoryOptions; }

    public VendorBatteryHealthCategory SelectedVendorHealthCategory { get => BatteryHealth.SelectedVendorHealthCategory; set => BatteryHealth.SelectedVendorHealthCategory=value; }
}
