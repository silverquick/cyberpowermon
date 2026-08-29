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

            var safeMessage = (upsEvent.Message ?? string.Empty)
                .Replace("\"", "\\\"")
                .Replace("\r", " ")
                .Replace("\n", " ");

            var formatted = commandLine
                .Replace("{EVENT}", upsEvent.Type.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{SEVERITY}", upsEvent.Severity.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{STATE}", upsEvent.CurrentState.ToString(), StringComparison.OrdinalIgnoreCase)
                .Replace("{BATTERY}", batteryStr, StringComparison.OrdinalIgnoreCase)
                .Replace("{RUNTIME}", runtimeStr, StringComparison.OrdinalIgnoreCase)
                .Replace("{POWER}", powerStr, StringComparison.OrdinalIgnoreCase)
                .Replace("{MESSAGE}", $"\"{safeMessage}\"", StringComparison.OrdinalIgnoreCase);

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

            try
            {
                // Asynchronously drain stdout and stderr to prevent OS pipe buffer exhaustion deadlocks
                var stdoutTask = process.StandardOutput.ReadToEndAsync(linkedCts.Token);
                var stderrTask = process.StandardError.ReadToEndAsync(linkedCts.Token);

                await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
                await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

                return process.ExitCode == 0;
            }
            catch (OperationCanceledException)
            {
                KillProcessTree(process);
                return false;
            }
            catch
            {
                KillProcessTree(process);
                return false;
            }
        }
        catch
        {
            return false;
        }
    }

    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
        }
    }
}
