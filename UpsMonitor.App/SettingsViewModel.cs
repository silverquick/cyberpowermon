using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

namespace UpsMonitor.App;

/// <summary>Settings editing keeps existing immediate effects; save applies engine changes only after persistence.</summary>
public sealed class SettingsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly AppConfiguration _configuration;
    private readonly Func<AppConfiguration, Task> _save;
    private readonly SettingsEffects _effects;
    private readonly Action<Exception> _onError;
    private readonly Func<string, Task<bool>> _testWebhook;
    private bool _disposed;
    public BatteryHealthViewModel BatteryHealth { get; }
    public SettingsViewModel(AppConfiguration configuration, Func<AppConfiguration, Task> save, SettingsEffects effects,
        BatteryHealthViewModel batteryHealth, Action<Exception> onError, string language, Func<string, Task<bool>> testWebhook)
    {
        _configuration=configuration; _save=save; _effects=effects; BatteryHealth=batteryHealth;
        _onError=onError; _testWebhook=testWebhook;
        _pollIntervalMs=configuration.Monitoring.PollIntervalMs; _runtimeLowSeconds=configuration.Monitoring.RuntimeLowSeconds;
        _selectedLanguageCode=language;
        RefreshThemeOptions();
        SaveSettingsCommand=new AsyncRelayCommand(SaveSettingsAsync,SetCommandError);
        TestNotificationCommand=new AsyncRelayCommand(() => { if (!_disposed) TestNotificationRequested?.Invoke(UpsEventSeverity.Warning); return Task.CompletedTask; },SetCommandError);
        TestWebhookCommand=new AsyncRelayCommand(TestWebhookAsync,SetCommandError);
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<UpsEventSeverity>? TestNotificationRequested;
    public ICommand TestNotificationCommand { get; }
    public ICommand TestWebhookCommand { get; }
    public void SetStatus(string status) { if (!_disposed) SettingsStatus=status; }
    public void RefreshLanguage(string language)
    {
        if (_disposed) return;
        _selectedLanguageCode=language;
        OnPropertyChanged(nameof(SelectedLanguageCode));
        RefreshThemeOptions();
    }
    public void Dispose() => _disposed=true;
    private async Task TestWebhookAsync()
    {
        if (_disposed) return;
        if (string.IsNullOrWhiteSpace(WebhookUrl)) { WebhookStatus=L("WebhookUrlRequired"); return; }
        WebhookStatus=L("WebhookTesting");
        var success=await _testWebhook(WebhookUrl);
        if (!_disposed) WebhookStatus=success ? L("WebhookTestSuccess") : L("WebhookTestFailed");
    }
    private string _settingsStatus = string.Empty;
    private int _pollIntervalMs;
    private int _runtimeLowSeconds;
    private string _selectedLanguageCode;
    private IReadOnlyList<ThemeOption> _themeOptions = [];
    private string _webhookStatus = string.Empty;
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

    public IReadOnlyList<LanguageOption> LanguageOptions { get; } =
    [
        new("ja-JP", "日本語"),
        new("en-US", "English"),
    ];

    public ICommand SaveSettingsCommand { get; }

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
                _effects.SetRunOnStartup(true, value);
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
            _effects.SetRunOnStartup(value, StartMinimized);
            OnPropertyChanged();
        }
    }

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
            _effects.ApplyLanguage(value);
        }
    }

    public string SettingsStatus { get => _settingsStatus; private set => SetField(ref _settingsStatus, value); }
    private void RefreshThemeOptions()
    {
        ThemeOptions = LocalizedOption.UpdateLabels(ThemeOptions,
        [
            new("system", L("ThemeSystem")),
            new("dark", L("ThemeDark")),
            new("light", L("ThemeLight")),
        ]);
    }

    public async Task SaveSettingsAsync()
    {
        if (_disposed) return;
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

        if (!BatteryHealth.AreHealthSettingsValid())
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
        _configuration.BatteryHealth.WarningThresholdPercent = BatteryHealth.BatteryWarningThresholdPercent;
        _configuration.BatteryHealth.CriticalThresholdPercent = BatteryHealth.BatteryCriticalThresholdPercent;
        _configuration.BatteryHealth.ComparableLoadTolerancePercent = BatteryHealth.ComparableLoadTolerancePercent;
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
        if (_disposed) return;
        _effects.SetPollInterval(PollIntervalMs);
        _effects.SetRuntimeLowThreshold(TimeSpan.FromSeconds(RuntimeLowSeconds));
        _effects.SetAlertThresholds(_configuration.Alerts.ToAlertThresholds());
        SettingsStatus = LocalizationManager.Format("SettingsSavedFormat", DateTimeOffset.Now.ToString("HH:mm:ss", CultureInfo.CurrentCulture));
        _effects.ReapplySnapshot();
    }

    public void SetCommandError(Exception exception)
    {
        if (_disposed) return;
        SettingsStatus = LocalizationManager.Format("SettingsSaveErrorFormat", exception.Message);
        _onError(exception);
    }

    private static string L(string key) => LocalizationManager.Get(key);

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
