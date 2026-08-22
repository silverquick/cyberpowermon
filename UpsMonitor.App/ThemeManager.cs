using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace UpsMonitor.App;

internal static class ThemeManager
{
    internal static bool IsDarkMode { get; private set; } = true;

    internal static void ApplySystemTheme(Application application)
    {
        IsDarkMode = ReadSystemDarkMode();
        var colors = IsDarkMode
            ? new Dictionary<string, string>
            {
                ["WindowBrush"] = "#111827",
                ["PanelBrush"] = "#E61F2937",
                ["NavigationBrush"] = "#E60B1220",
                ["SelectedNavBrush"] = "#303B82F6",
                ["SecondaryPanelBrush"] = "#D9182235",
                ["TextBrush"] = "#F8FAFC",
                ["MutedTextBrush"] = "#B3C0D1",
                ["BorderBrush"] = "#42516A",
                ["HoverBrush"] = "#263B82F6",
                ["DisabledBrush"] = "#263247",
                ["DisabledTextBrush"] = "#9AA8BC",
            }
            : new Dictionary<string, string>
            {
                ["WindowBrush"] = "#F4F7FB",
                ["PanelBrush"] = "#FFFFFFFF",
                ["NavigationBrush"] = "#FFF0F4F8",
                ["SelectedNavBrush"] = "#FFDCEBFF",
                ["SecondaryPanelBrush"] = "#FFEEF2F7",
                ["TextBrush"] = "#FF111827",
                ["MutedTextBrush"] = "#FF475569",
                ["BorderBrush"] = "#FFCBD5E1",
                ["HoverBrush"] = "#FFE8F1FF",
                ["DisabledBrush"] = "#FFE2E8F0",
                ["DisabledTextBrush"] = "#FF64748B",
            };

        foreach (var (key, color) in colors)
        {
            application.Resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }
    }

    private static bool ReadSystemDarkMode()
    {
        try
        {
            var value = Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                0);
            return value is not int intValue || intValue == 0;
        }
        catch (Exception)
        {
            return true;
        }
    }
}
