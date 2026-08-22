namespace UpsMonitor.Core;

// Contracts only in v0.1. No action executor is wired to the monitor.
public enum RuleTriggerType
{
    PowerLost,
    BatteryLow,
    RuntimeLow,
    BatteryCritical,
    OverloadDetected,
}

public enum RuleConditionType
{
    Duration,
    BatteryPercentBelow,
    RuntimeBelow,
}

public enum RuleActionType
{
    Log,
    Notification,
    SshShutdown,
    LocalShutdown,
    ExternalCommand,
    Webhook,
}

public sealed record RuleDefinition(
    string Id,
    string Name,
    bool Enabled,
    RuleTriggerType Trigger,
    IReadOnlyList<RuleConditionDefinition> Conditions,
    IReadOnlyList<RuleActionDefinition> Actions);

public sealed record RuleConditionDefinition(RuleConditionType Type, double Value);

public sealed record RuleActionDefinition(RuleActionType Type, TimeSpan Delay, IReadOnlyDictionary<string, string> Settings);
