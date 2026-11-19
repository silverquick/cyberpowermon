using System.Globalization;
using UpsMonitor.App;
using UpsMonitor.Core;

internal static class UiTextFormattingTests
{
    private static void InLanguages(Action<string> check)
    {
        var previousLanguage = LocalizationManager.Language;
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            foreach (var language in new[] { "ja-JP", "en-US" })
            {
                LocalizationManager.Language = language;
                check(language);
            }
        }
        finally
        {
            LocalizationManager.Language = previousLanguage;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private static void Equal(string expected, string actual)
    {
        if (expected != actual) throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }

    internal static void Margins() => InLanguages(language =>
    {
        var ja = language == "ja-JP";
        Equal(ja ? "残り 12.3 W / 45.6 VA" : "Remaining 12.3 W / 45.6 VA", UiTextFormatting.PowerMargin(12.34, 45.56));
        Equal(ja ? "残り 0 W" : "Remaining 0 W", UiTextFormatting.PowerMargin(0, null));
        Equal(ja ? "下限(92V)まで +0 V" : "Above lower limit (92 V): +0 V", UiTextFormatting.VoltageMargin(92, 0));
        Equal(ja ? "下限(92V)まで +8.5 V" : "Above lower limit (92 V): +8.5 V", UiTextFormatting.VoltageMargin(92, 8.5));
        Equal(ja ? "下限(92V)超過 -2.5 V" : "Below lower limit (92 V): -2.5 V", UiTextFormatting.VoltageMargin(92, -2.5));
        Equal(ja ? "2.27 V/cell (12セル)" : "2.27 V/cell (12 cells)", UiTextFormatting.CellVoltage(2.2666, 12));
    });

    internal static void Costs() => InLanguages(language =>
    {
        var ja = language == "ja-JP";
        Equal(ja ? "約 1,234 円 (31.5円/kWh)" : "Approx. 1,234 JPY (31.5 JPY/kWh)", UiTextFormatting.PeriodCost(1234, 31.5));
        Equal(ja ? "約 1,234 円" : "Approx. 1,234 JPY", UiTextFormatting.EstimatedCost(1234));
        Equal(ja ? "約 0 円" : "Approx. 0 JPY", UiTextFormatting.EstimatedCost(0));
    });

    internal static void OfficialContextSeparator() => InLanguages(language =>
    {
        var separator = LocalizationManager.Get("OfficialContextSeparator");
        Equal(language == "ja-JP" ? "・" : " · ", separator);
        var load = LocalizationManager.Format("OfficialContextLoad", 25);
        var charging = LocalizationManager.Get("OfficialContextCharging");
        Equal(language == "ja-JP" ? "負荷 25%・充電中" : "Load 25% · Charging",
            string.Join(separator, new[] { load, charging }));
    });

    internal static void Notifications() => InLanguages(language =>
    {
        var ja = language == "ja-JP";
        Equal(ja ? "[PowerGuard] テスト通知 (重大)" : "[PowerGuard] Test notification (Critical)", UiTextFormatting.TestNotificationTitle(UpsEventSeverity.Critical));
        Equal(ja ? "[PowerGuard] テスト通知 (警告)" : "[PowerGuard] Test notification (Warning)", UiTextFormatting.TestNotificationTitle(UpsEventSeverity.Warning));
        Equal(ja ? "[PowerGuard] テスト通知 (情報)" : "[PowerGuard] Test notification (Information)", UiTextFormatting.TestNotificationTitle(UpsEventSeverity.Information));
        Equal(UiTextFormatting.TestNotificationTitle(UpsEventSeverity.Information), UiTextFormatting.TestNotificationTitle((UpsEventSeverity)999));
        Equal(ja ? "これは PowerGuard の Windows 通知テストです。通知設定は正常に機能しています。" : "This is a PowerGuard Windows notification test. Notification settings are working correctly.", LocalizationManager.Get("TestNotificationMessage"));
    });
}
