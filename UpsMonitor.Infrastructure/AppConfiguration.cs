using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public sealed class AppConfiguration
{
    public MonitoringConfiguration Monitoring { get; init; } = new();

    public UiConfiguration Ui { get; init; } = new();

    public AlertsConfiguration Alerts { get; init; } = new();

    public WebhookConfiguration Webhook { get; init; } = new();

    public ExternalCommandConfiguration ExternalCommand { get; init; } = new();

    public BatteryHealthConfiguration BatteryHealth { get; init; } = new();

    public HistoryConfiguration History { get; init; } = new();

    // Intentionally inert in v0.1. This preserves the future configuration shape.
    public List<RuleDefinition> ShutdownPolicies { get; init; } = [];
}

public sealed class AlertsConfiguration
{
    public bool EnableSoundAlerts { get; set; } = false;

    public double HighLoadWarningPercent { get; set; } = 80.0;

    public double LowVoltageWarningThreshold { get; set; } = 92.0;

    public double HighVoltageWarningThreshold { get; set; } = 108.0;
}

public sealed class WebhookConfiguration
{
    public bool Enabled { get; set; } = false;

    public string Url { get; set; } = string.Empty;

    public bool NotifyOnPowerLost { get; set; } = true;

    public bool NotifyOnPowerRestored { get; set; } = true;

    public bool NotifyOnBatteryLow { get; set; } = true;

    public bool NotifyOnHighLoad { get; set; } = false;
}

public sealed class ExternalCommandConfiguration
{
    public bool Enabled { get; set; } = false;

    public string CommandOnPowerLost { get; set; } = string.Empty;

    public string CommandOnPowerRestored { get; set; } = string.Empty;

    public string CommandOnBatteryLow { get; set; } = string.Empty;
}

public sealed class HistoryConfiguration
{
    private int _rawRetentionDays = 14;
    private int _rawUsageCheckpointSeconds = 300;

    public int RawRetentionDays
    {
        get => _rawRetentionDays;
        set => _rawRetentionDays = value is >= 1 and <= 365 ? value : 14;
    }

    public int RawUsageCheckpointSeconds
    {
        get => _rawUsageCheckpointSeconds;
        set => _rawUsageCheckpointSeconds = value is >= 30 and <= 86_400 ? value : 300;
    }
}

public sealed class BatteryHealthConfiguration
{
    public double WarningThresholdPercent { get; set; } = 70;

    public double CriticalThresholdPercent { get; set; } = 60;

    public double ComparableLoadTolerancePercent { get; set; } = 5;

    public List<BatteryHealthProfile> Profiles { get; init; } = [];
}

public sealed class UiConfiguration
{
    private string _language = "system";
    private string _theme = "system";

    public string Language
    {
        get => _language;
        set => _language = value is "ja-JP" or "en-US" ? value : "system";
    }

    public string Theme
    {
        get => _theme;
        set => _theme = value is "dark" or "light" ? value : "system";
    }

    public bool MinimizeToTray { get; set; } = true;

    public bool CloseToTray { get; set; } = true;

    public bool StartMinimized { get; set; } = false;

    public bool EnableNotifications { get; set; } = true;

    public bool RunOnStartup { get; set; } = false;
}

public sealed class MonitoringConfiguration
{
    private int _pollIntervalMs = 1000;
    private int _runtimeLowSeconds = 180;
    private double _electricityRatePerKwh = 31.0;

    public int PollIntervalMs
    {
        get => _pollIntervalMs;
        set => _pollIntervalMs = value is >= 250 and <= 60_000 ? value : 1000;
    }

    public int RuntimeLowSeconds
    {
        get => _runtimeLowSeconds;
        set => _runtimeLowSeconds = value is >= 0 and <= 86_400 ? value : 180;
    }

    public double ElectricityRatePerKwh
    {
        get => _electricityRatePerKwh;
        set => _electricityRatePerKwh = value is >= 0.0 and <= 1000.0 ? value : 31.0;
    }
}
