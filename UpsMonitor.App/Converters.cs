using System;
using System.Globalization;
using System.Windows.Data;
using UpsMonitor.Core;

namespace UpsMonitor.App;

public sealed class EstimatedCostFormatConverter : IMultiValueConverter
{
    // The second binding is the UI language so existing rows update immediately.
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values.Length > 0 && values[0] is double cost ? UiTextFormatting.EstimatedCost(cost) : "-";

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

public sealed class EnergyPeriodFormatConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is EnergyReportItem item)
        {
            return item.Period == EnergyReportPeriod.Month
                ? item.PeriodStart.LocalDateTime.ToString("yyyy-MM", culture)
                : item.PeriodStart.LocalDateTime.ToString("yyyy-MM-dd", culture);
        }

        if (value is DateTimeOffset dto)
        {
            return dto.LocalDateTime.ToString("yyyy-MM-dd", culture);
        }

        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
