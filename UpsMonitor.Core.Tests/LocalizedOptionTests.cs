using UpsMonitor.App;
using UpsMonitor.Core;

internal static class LocalizedOptionTests
{
    public static void LanguageKeepsSelections()
    {
        var previousLanguage = LocalizationManager.Language;
        string L(string key) => LocalizationManager.Get(key);
        Func<IReadOnlyList<LocalizedOption>>[] choices =
        [
            () => new ThemeOption[] { new("system", L("ThemeSystem")), new("dark", L("ThemeDark")) },
            () => new BatteryBaselineModeOption[] { new(BatteryRuntimeBaselineKind.CurrentRelative, L("BaselineModeRelative")),
                new(BatteryRuntimeBaselineKind.KnownHealthAnchor, L("BaselineModeKnown")) },
            () => new VendorHealthCategoryOption[] { new(VendorBatteryHealthCategory.Unknown, L("VendorHealthCategoryUnknown")),
                new(VendorBatteryHealthCategory.Good, L("VendorHealthCategoryGood")) },
            () => new HistoryRangeOption[] { new("1h", TimeSpan.FromHours(1), L("HistoryRange1Hour")),
                new("30d", TimeSpan.FromDays(30), L("HistoryRange30Days")) },
            () => new AnalyticsMetricOption[] { new(WeeklyPatternMetric.ActivePower, L("AnalyticsMetricPower"), "W"),
                new(WeeklyPatternMetric.PowerFactor, L("AnalyticsMetricPowerFactor"), "%") },
            () => new AnalyticsRangeOption[] { new(TimeSpan.FromDays(30), L("AnalyticsRange30D")), new(null, L("AnalyticsRangeAll")) },
        ];
        static object? Value(LocalizedOption option) => option switch
        {
            ThemeOption theme => theme.Code,
            BatteryBaselineModeOption baseline => baseline.Kind,
            VendorHealthCategoryOption vendor => vendor.Category,
            HistoryRangeOption history => (history.Key, history.Duration),
            AnalyticsMetricOption metric => (metric.Metric, metric.Unit),
            AnalyticsRangeOption range => range.Duration,
            _ => throw new InvalidOperationException("unexpected option type"),
        };
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        try
        {
            foreach (var create in choices)
            {
                LocalizationManager.Language = "ja-JP";
                var options = LocalizedOption.UpdateLabels<LocalizedOption>([], create());
                var selectedItems = options.ToArray();
                var selectedValues = options.Select(Value).ToArray();
                var notifications = 0;
                foreach (var option in options)
                    option.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(LocalizedOption.DisplayName)) notifications++; };
                foreach (var language in new[] { "en-US", "ja-JP" })
                {
                    LocalizationManager.Language = language;
                    var translated = create();
                    var refreshed = LocalizedOption.UpdateLabels(options, translated);
                    Check(ReferenceEquals(options, refreshed), "ItemsSource identity is preserved");
                    for (var i = 0; i < options.Count; i++)
                    {
                        Check(ReferenceEquals(selectedItems[i], refreshed[i]) && Equals(selectedValues[i], Value(refreshed[i])),
                            "selected item and value survive translation, including the null all-time duration");
                        Check(refreshed[i].DisplayName == translated[i].DisplayName && refreshed[i].ToString() == translated[i].DisplayName,
                            "display labels and ToString are translated");
                    }
                }
                Check(notifications > 0, "translated labels notify the UI");
            }
        }
        finally { LocalizationManager.Language = previousLanguage; }
    }
}
