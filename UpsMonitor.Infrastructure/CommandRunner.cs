using System.Diagnostics;
using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public sealed class CommandRunner
{
    public static async Task<bool> RunCommandAsync(
        string commandLine,
        UpsEvent upsEvent,
        UpsSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return false;
        }

        try
        {
            var batteryStr = snapshot.BatteryPercent.HasValue ? $"{snapshot.BatteryPercent.Value:F0}" : "0";
            var runtimeStr = snapshot.RuntimeRemaining.HasValue ? $"{snapshot.RuntimeRemaining.Value.TotalMinutes:F0}" : "0";
            var powerStr = snapshot.ActivePower.HasValue ? $"{snapshot.ActivePower.Value:F0}" : "0";

            var formatted = commandLine
                .Replace("{EVENT}", upsEvent.Type.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{SEVERITY}", upsEvent.Severity.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{STATE}", upsEvent.CurrentState.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{BATTERY}", batteryStr, StringComparison.OrdinalIgnoreCase)
                .Replace("{RUNTIME}", runtimeStr, StringComparison.OrdinalIgnoreCase)
                .Replace("{POWER}", powerStr, StringComparison.OrdinalIgnoreCase)
                .Replace("{MESSAGE}", $"\"{upsEvent.Message}\"", StringComparison.OrdinalIgnoreCase);

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {formatted}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            // 最大30秒でタイムアウト
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
