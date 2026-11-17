using UpsMonitor.Core;

namespace UpsMonitor.App;

// Pure presentation helpers; resource lookup follows the active UI language.
internal static class UiTextFormatting
{
    internal static string PowerMargin(double watts, double? voltAmperes) => voltAmperes.HasValue
        ? LocalizationManager.Format("PowerMarginWithVaFormat", watts, voltAmperes.Value)
        : LocalizationManager.Format("PowerMarginFormat", watts);

    internal static string VoltageMargin(double lowerLimit, double margin) => LocalizationManager.Format(
        margin >= 0 ? "VoltageMarginAboveFormat" : "VoltageMarginBelowFormat", lowerLimit, margin);

    internal static string CellVoltage(double voltage, int cells) =>
        LocalizationManager.Format("CellVoltageFormat", voltage, cells);

    internal static string PeriodCost(double cost, double rate) =>
        LocalizationManager.Format("PeriodCostFormat", cost, rate);

    internal static string EstimatedCost(double cost) => LocalizationManager.Format("EstimatedCostFormat", cost);

    internal static string TestNotificationTitle(UpsEventSeverity severity) => LocalizationManager.Get(severity switch
    {
        UpsEventSeverity.Critical => "TestNotificationCriticalTitle",
        UpsEventSeverity.Warning => "TestNotificationWarningTitle",
        _ => "TestNotificationInfoTitle",
    });
}
