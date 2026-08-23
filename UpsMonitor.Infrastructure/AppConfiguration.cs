using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public sealed class AppConfiguration
{
    public MonitoringConfiguration Monitoring { get; init; } = new();

    public UiConfiguration Ui { get; init; } = new();

    public BatteryHealthConfiguration BatteryHealth { get; init; } = new();

    public HistoryConfiguration History { get; init; } = new();

    // Intentionally inert in v0.1. This preserves the future configuration shape.
    public List<RuleDefinition> ShutdownPolicies { get; init; } = [];
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

    public string Language
    {
        get => _language;
        set => _language = value is "ja-JP" or "en-US" ? value : "system";
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
}
