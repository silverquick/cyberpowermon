using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public sealed class AppConfiguration
{
    public MonitoringConfiguration Monitoring { get; init; } = new();

    public UiConfiguration Ui { get; init; } = new();

    // Intentionally inert in v0.1. This preserves the future configuration shape.
    public List<RuleDefinition> ShutdownPolicies { get; init; } = [];
}

public sealed class UiConfiguration
{
    private string _language = "system";

    public string Language
    {
        get => _language;
        set => _language = value is "ja-JP" or "en-US" ? value : "system";
    }
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
