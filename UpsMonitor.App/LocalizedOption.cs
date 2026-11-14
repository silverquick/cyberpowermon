using System.ComponentModel;
using UpsMonitor.Core;

namespace UpsMonitor.App;

/// <summary>A fixed choice whose label can change without invalidating WPF selection.</summary>
public abstract class LocalizedOption(string displayName) : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public string DisplayName { get; private set; } = displayName;
    public override string ToString() => DisplayName;

    // These lists have fixed values and order. Preserve both ItemsSource and item identity.
    internal static IReadOnlyList<T> UpdateLabels<T>(IReadOnlyList<T> current, IReadOnlyList<T> translated)
        where T : LocalizedOption
    {
        if (current.Count == 0) return translated;
        if (current.Count != translated.Count) throw new ArgumentException("The choice list must have a fixed shape.", nameof(translated));
        for (var i = 0; i < current.Count; i++)
        {
            if (current[i].DisplayName == translated[i].DisplayName) continue;
            current[i].DisplayName = translated[i].DisplayName;
            current[i].PropertyChanged?.Invoke(current[i], new(nameof(DisplayName)));
        }
        return current;
    }
}

public sealed class ThemeOption(string code, string displayName) : LocalizedOption(displayName)
{
    public string Code { get; } = code;
}

public sealed class BatteryBaselineModeOption(BatteryRuntimeBaselineKind kind, string displayName) : LocalizedOption(displayName)
{
    public BatteryRuntimeBaselineKind Kind { get; } = kind;
}

public sealed class VendorHealthCategoryOption(VendorBatteryHealthCategory category, string displayName) : LocalizedOption(displayName)
{
    public VendorBatteryHealthCategory Category { get; } = category;
}

public sealed class HistoryRangeOption(string key, TimeSpan duration, string displayName) : LocalizedOption(displayName)
{
    public string Key { get; } = key;
    public TimeSpan Duration { get; } = duration;
}

public sealed class AnalyticsMetricOption(WeeklyPatternMetric metric, string displayName, string unit) : LocalizedOption(displayName)
{
    public WeeklyPatternMetric Metric { get; } = metric;
    public string Unit { get; } = unit;
}

public sealed class AnalyticsRangeOption(TimeSpan? duration, string displayName) : LocalizedOption(displayName)
{
    public TimeSpan? Duration { get; } = duration;
}
