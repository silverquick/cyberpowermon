using UpsMonitor.Core;

namespace UpsMonitor.App;

public sealed record SettingsEffects(
    Action<string> ApplyTheme,
    Action<string> ApplyLanguage,
    Action<bool, bool> SetRunOnStartup,
    Action<int> SetPollInterval,
    Action<TimeSpan> SetRuntimeLowThreshold,
    Action<UpsAlertThresholds> SetAlertThresholds,
    Action RefreshPeriodCost,
    Action ReapplySnapshot);
