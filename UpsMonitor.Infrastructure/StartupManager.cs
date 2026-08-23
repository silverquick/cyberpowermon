using System.Diagnostics;
using Microsoft.Win32;

namespace UpsMonitor.Infrastructure;

public static class StartupManager
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "PowerGuardUpsMonitor";

    public static bool IsRunOnStartupEnabled()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: false);
            return key?.GetValue(AppName) is not null;
        }
        catch
        {
            return false;
        }
    }

    public static void SetRunOnStartup(bool enable, bool startMinimized = false)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, writable: true);
            if (key is null)
            {
                return;
            }

            if (enable)
            {
                var processPath = Environment.ProcessPath;
                if (string.IsNullOrEmpty(processPath))
                {
                    return;
                }

                var command = startMinimized ? $"\"{processPath}\" --tray" : $"\"{processPath}\"";
                key.SetValue(AppName, command);
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Ignore registry permission errors if running in restricted environments
        }
    }
}
