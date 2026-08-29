using System.Net.Http;
using System.Text;
using System.Text.Json;
using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public sealed class WebhookNotifier
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    private const int MaxRetries = 2;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(8);

    public static async Task<bool> SendNotificationAsync(
        string webhookUrl,
        UpsEvent upsEvent,
        UpsSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl) || !Uri.TryCreate(webhookUrl, UriKind.Absolute, out _))
        {
            return false;
        }

        string payload;
        try
        {
            payload = BuildPayload(webhookUrl, upsEvent, snapshot);
        }
        catch
        {
            return false;
        }

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            try
            {
                using var attemptCts = new CancellationTokenSource(RequestTimeout);
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, attemptCts.Token);
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");

                using var response = await HttpClient.PostAsync(webhookUrl, content, linkedCts.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    return true;
                }

                // If non-transient client error (4xx other than 429), don't retry
                var statusCode = (int)response.StatusCode;
                if (statusCode is >= 400 and < 500 && statusCode != 429)
                {
                    return false;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return false;
            }
            catch (Exception)
            {
                // Transient exception (network down, DNS error, timeout) - retry if attempts remain
                if (attempt == MaxRetries)
                {
                    return false;
                }
            }

            if (attempt < MaxRetries)
            {
                var delayMs = (attempt + 1) * 1000;
                try
                {
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return false;
                }
            }
        }

        return false;
    }

    public static async Task<bool> SendTestNotificationAsync(
        string webhookUrl,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl) || !Uri.TryCreate(webhookUrl, UriKind.Absolute, out _))
        {
            return false;
        }

        try
        {
            var testEvent = new UpsEvent(
                DateTimeOffset.UtcNow,
                UpsEventType.PowerLost,
                "PowerGuard Webhook Test Notification",
                UpsPowerState.Online,
                UpsPowerState.OnBattery);

            var testSnapshot = new UpsSnapshot
            {
                Timestamp = DateTimeOffset.UtcNow,
                IsConnected = true,
                AcPresent = false,
                BatteryPercent = 95.0,
                InputVoltage = 0.0,
                OutputVoltage = 100.0,
                ActivePower = 150.0,
                RuntimeRemaining = TimeSpan.FromMinutes(45),
            };

            return await SendNotificationAsync(webhookUrl, testEvent, testSnapshot, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return false;
        }
    }

    private static string BuildPayload(string webhookUrl, UpsEvent upsEvent, UpsSnapshot snapshot)
    {
        var title = $"[PowerGuard] {GetEventTitle(upsEvent.Type)}";
        var deviceName = snapshot.Device?.DisplayName ?? "UPS";
        var batteryInfo = snapshot.BatteryPercent.HasValue ? $"{snapshot.BatteryPercent.Value:F0}%" : "N/A";
        var runtimeInfo = snapshot.RuntimeRemaining.HasValue ? $"{snapshot.RuntimeRemaining.Value.TotalMinutes:F0} min" : "N/A";
        var powerInfo = snapshot.ActivePower.HasValue ? $"{snapshot.ActivePower.Value:F0} W" : "N/A";
        var fullMessage = $"{upsEvent.Message}\nDevice: {deviceName} | Battery: {batteryInfo} | Runtime: {runtimeInfo} | Load: {powerInfo}";

        // Discord Webhook
        if (webhookUrl.Contains("discord.com/api/webhooks", StringComparison.OrdinalIgnoreCase))
        {
            var color = upsEvent.Severity switch
            {
                UpsEventSeverity.Critical => 0xEF4444, // Red
                UpsEventSeverity.Warning => 0xF59E0B,  // Amber
                _ => 0x10B981,                         // Green
            };

            var discordPayload = new
            {
                username = "PowerGuard UPS Monitor",
                embeds = new[]
                {
                    new
                    {
                        title,
                        description = fullMessage,
                        color,
                        timestamp = upsEvent.Timestamp.ToString("o"),
                        fields = new[]
                        {
                            new { name = "State", value = upsEvent.CurrentState.ToString(), @inline = true },
                            new { name = "Battery", value = batteryInfo, @inline = true },
                            new { name = "Runtime", value = runtimeInfo, @inline = true },
                        },
                    },
                },
            };
            return JsonSerializer.Serialize(discordPayload);
        }

        // Slack Webhook
        if (webhookUrl.Contains("hooks.slack.com", StringComparison.OrdinalIgnoreCase))
        {
            var slackPayload = new
            {
                text = $"*{title}*\n{fullMessage}",
            };
            return JsonSerializer.Serialize(slackPayload);
        }

        // Generic JSON Webhook
        var genericPayload = new
        {
            title,
            eventType = upsEvent.Type.ToString(),
            severity = upsEvent.Severity.ToString(),
            message = upsEvent.Message,
            previousState = upsEvent.PreviousState.ToString(),
            currentState = upsEvent.CurrentState.ToString(),
            timestamp = upsEvent.Timestamp.ToString("o"),
            device = deviceName,
            batteryPercent = snapshot.BatteryPercent,
            runtimeRemainingMinutes = snapshot.RuntimeRemaining?.TotalMinutes,
            activePowerWatts = snapshot.ActivePower,
        };

        return JsonSerializer.Serialize(genericPayload);
    }

    private static string GetEventTitle(UpsEventType type) => type switch
    {
        UpsEventType.PowerLost => "Power Loss (AC Outage)",
        UpsEventType.PowerRestored => "Power Restored",
        UpsEventType.BatteryLow => "Battery Low",
        UpsEventType.BatteryCritical => "Battery Critical / Imminent Shutdown",
        UpsEventType.RuntimeLow => "Runtime Remaining Low",
        UpsEventType.OverloadDetected => "UPS Overload Detected",
        UpsEventType.UpsDisconnected => "UPS Disconnected",
        UpsEventType.UpsReconnected => "UPS Reconnected",
        UpsEventType.VoltageAbnormal => "Input Voltage Abnormal",
        UpsEventType.HighLoadWarning => "High Load Warning",
        _ => "UPS Status Event",
    };
}
