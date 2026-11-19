using System.Text.Json;
using UpsMonitor.App;
using UpsMonitor.Core;
using static HistoryViewModelTests;

internal static class SharedExportViewModelTests
{
    public static void CancellationAndFailure() => WithStore(async store =>
    {
        var root = Path.GetDirectoryName(store.DatabasePath)!;
        var path = Path.Combine(root, "cancelled.csv");
        var errors = new List<Exception>();
        var picks = 0;
        await using var exports = new ExportViewModel(store, (_, _) => { picks++; return path; }, () => new(), errors.Add, errors.Add);
        var enabledAtStart = true;
        exports.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(exports.IsExporting) || !exports.IsExporting) return;
            enabledAtStart = exports.CanStartExport;
            exports.ExportEventsCsvCommand.Execute(null); // Shared busy state blocks the other tab.
            exports.CancelExportCommand.Execute(null);
        };
        await exports.ExportTelemetryAsync(false, () => (DateTimeOffset.MinValue, DateTimeOffset.MaxValue));
        Check(!enabledAtStart && picks == 1, "History/Logs share busy state");
        Check(exports.ExportStatus == LocalizationManager.Get("ExportCancelled") && !exports.IsExporting && exports.CanStartExport, "cancel clears shared loading");
        Check(!File.Exists(path) && errors.Count == 0, "cancel never publishes partial file");
        await using var failure = new ExportViewModel(store, (_, _) => Path.Combine(store.DatabasePath, "impossible.csv"), () => new(), errors.Add, errors.Add);
        await failure.ExportTelemetryAsync(false, () => (DateTimeOffset.MinValue, DateTimeOffset.MaxValue));
        Check(errors.Count == 1 && !failure.IsExporting && failure.CanStartExport, "IO error reaches owner and resets shared state");
    }).GetAwaiter().GetResult();

    public static void HistoryAndLogsShareExport() => WithStore(async store =>
    {
        var now = DateTimeOffset.Now;
        await store.WriteAsync(new UpsSnapshot { Device = Device, Timestamp = now.AddDays(-2), IsConnected = true, BatteryPercent = 80 }, default);
        await store.WriteAsync(new UpsSnapshot { Device = Device, Timestamp = now.AddMinutes(-10), IsConnected = true, BatteryPercent = 90 }, default);
        await store.WriteAsync(new UpsEvent(now.AddMinutes(-5), UpsEventType.PowerLost, "synthetic", UpsPowerState.Online, UpsPowerState.OnBattery), default);
        using var logs = new LogsViewModel(store);
        logs.EventType = UpsEventType.PowerLost;
        var errors = new List<Exception>();
        var root = Path.GetDirectoryName(store.DatabasePath)!;
        var path = Path.Combine(root, "telemetry.json");
        HistoryViewModel? history = null;
        var cancelDialog = false;
        var pickerCalls = 0;
        await using var exports = new ExportViewModel(store, (events, json) =>
        {
            pickerCalls++;
            if (cancelDialog) return null;
            Check(!events && json, "History JSON command parameters");
            history!.HistoryExportRange = 1; // Filters are captured after the file picker returns.
            return path;
        }, logs.CreateExportQuery, errors.Add, errors.Add);
        using (history = new HistoryViewModel(HistoryQueries.FromStore(store), new(), errors.Add, exports: exports))
        {
            history.ExportTelemetryJsonCommand.Execute(null);
            await Until(() => history.ExportTelemetryJsonCommand.CanExecute(null));
            using var json = JsonDocument.Parse(await File.ReadAllTextAsync(path));
            Check(json.RootElement.GetArrayLength() == 2, "all-time range selected during dialog included old row");
            Check(!exports.IsExporting && exports.CanStartExport && exports.ExportStatus == LocalizationManager.Format("ExportSuccess", "telemetry.json"), "shared status and availability reset");
            cancelDialog = true;
            var status = exports.ExportStatus;
            history.ExportTelemetryCsvCommand.Execute(null);
            await Until(() => history.ExportTelemetryCsvCommand.CanExecute(null));
            Check(exports.ExportStatus == status && pickerCalls == 2, "canceling dialog leaves status unchanged");
        }
        var eventPath = Path.Combine(root, "events.csv");
        await using var eventExports = new ExportViewModel(store, (events, json) =>
        { Check(events && !json, "Logs CSV command parameters"); return eventPath; }, logs.CreateExportQuery, errors.Add, errors.Add);
        eventExports.ExportEventsCsvCommand.Execute(null);
        await Until(() => eventExports.ExportEventsCsvCommand.CanExecute(null));
        Check(File.ReadAllLines(eventPath).Length == 2 && errors.Count == 0, "Logs uses shared event export");
        await eventExports.DisposeAsync();
        Check(!eventExports.CanStartExport, "disposed export unavailable");
        eventExports.ExportEventsCsvCommand.Execute(null);
        Check(eventExports.ExportEventsCsvCommand.CanExecute(null), "disposed command completes without work");
    }).GetAwaiter().GetResult();

    private static async Task Until(Func<bool> ready)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!ready())
        { if (DateTime.UtcNow > deadline) throw new TimeoutException("export command did not complete"); await Task.Delay(10); }
    }
}
