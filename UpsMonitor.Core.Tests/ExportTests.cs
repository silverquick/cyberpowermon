using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using UpsMonitor.App;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

internal static class ExportTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.UtcNow.AddMinutes(-10);
    private static readonly UpsDeviceInfo Device = new("synthetic", 1, 1, "Test", "UPS", "EXPORT", 0x84, 4, 64, 64);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    private static void Run(Func<SqliteTelemetryStore, string, Task> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "UpsMonitor.Export.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(root);
            Work().GetAwaiter().GetResult();
            async Task Work()
            {
                await using var store = new SqliteTelemetryStore(new AppPaths(root), new HistoryConfiguration());
                await store.InitializeAsync();
                await test(store, root);
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    private static Task Snapshot(SqliteTelemetryStore store, DateTimeOffset time) => store.WriteAsync(new UpsSnapshot
    {
        Timestamp = time, Device = Device, IsConnected = true, BatteryPercent = 90,
        RuntimeRemaining = TimeSpan.FromSeconds(123), AcPresent = true, Charging = false,
    }, default);

    public static void BoundariesAndFlush() => Run(async (store, root) =>
    {
        for (var i = -1; i <= 2; i++) await Snapshot(store, Start.AddSeconds(i));
        var csv = Path.Combine(root, "data.csv");
        var json = Path.Combine(root, "data.json");
        // No caller flush: the operation must include the last queued row.
        await ExportOperation.TelemetryAsync(store, json, TelemetryExportFormat.Json, Start.ToOffset(TimeSpan.FromHours(9)), Start.AddSeconds(1));
        using (var doc = JsonDocument.Parse(await File.ReadAllTextAsync(json)))
        {
            Check(doc.RootElement.GetArrayLength() == 2, "inclusive telemetry bounds");
            var row = doc.RootElement[0];
            Check(row.GetProperty("timestampUtc").GetString() == DateTimeOffset.FromUnixTimeMilliseconds(Start.ToUnixTimeMilliseconds()).ToString("O"), "UTC O format");
            Check(row.GetProperty("batteryPercent").GetDouble() == 90 && row.GetProperty("inputVoltage").ValueKind == JsonValueKind.Null, "numbers and nulls");
            Check(row.GetProperty("runtimeSeconds").GetDouble() == 123 && row.GetProperty("runtimeMinutes").GetDouble() == 2.0, "runtime fields");
            Check(row.GetProperty("acPresent").GetBoolean() && !row.GetProperty("charging").GetBoolean() &&
                row.GetProperty("overload").ValueKind == JsonValueKind.Null, "boolean and null types");
            Check(row.EnumerateObject().Select(p => p.Name).SequenceEqual(new[]
            {
                "timestampUtc", "timestampLocal", "deviceId", "inputVoltage", "outputVoltage", "batteryVoltage",
                "batteryPercent", "runtimeSeconds", "runtimeMinutes", "loadPercent", "activePowerWatts", "apparentPowerVa",
                "frequencyHz", "temperatureCelsius", "acPresent", "charging", "discharging", "lowBattery", "overload",
            }), "legacy JSON keys and order");
        }
        await ExportOperation.TelemetryAsync(store, csv, TelemetryExportFormat.Csv, Start, Start.AddSeconds(1));
        Check(File.ReadAllLines(csv).Length == 3, "CSV/JSON range count matches");
        await TelemetryExporter.ExportAllTelemetryJsonAsync(store.DatabasePath, json);
        await TelemetryExporter.ExportAllTelemetryCsvAsync(store.DatabasePath, csv);
        using var all = JsonDocument.Parse(await File.ReadAllTextAsync(json));
        Check(all.RootElement.GetArrayLength() == 4 && File.ReadAllLines(csv).Length == 5, "all ranges include both outer rows");
        Check(File.ReadAllBytes(csv).Take(3).SequenceEqual(Encoding.UTF8.GetPreamble()), "CSV UTF-8 BOM preserved");
        Check(File.ReadAllBytes(json)[0] == '[', "JSON no BOM");
    });

    public static void Empty() => Run(async (store, root) =>
    {
        var json = Path.Combine(root, "empty.json");
        var csv = Path.Combine(root, "empty.csv");
        await ExportOperation.TelemetryAsync(store, json, TelemetryExportFormat.Json, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(json));
        Check(doc.RootElement.GetArrayLength() == 0, "valid empty array");
        await ExportOperation.TelemetryAsync(store, csv, TelemetryExportFormat.Csv, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        Check(File.ReadAllLines(csv).Length == 1, "empty telemetry header");
        await ExportOperation.EventsAsync(store, csv, new());
        Check(File.ReadAllLines(csv).Length == 1, "empty event header");
    });

    public static void EventConditions() => Run(async (store, root) =>
    {
        await Snapshot(store, Start);
        var values = Enum.GetValues<UpsEventType>().Append((UpsEventType)999)
            .Select((type, i) => new UpsEvent(Start.AddMilliseconds(i), type, "synthetic-" + i, UpsPowerState.Online, UpsPowerState.OnBattery)).ToArray();
        foreach (var value in values) await store.WriteAsync(value, default);
        var file = Path.Combine(root, "events.csv");
        var queries = Enum.GetValues<UpsEventSeverity>().Select(severity => new EventQuery { Severity = severity })
            .Concat(new EventQuery[]
            {
                new() { From = Start, To = Start.AddMilliseconds(3), Limit = 1 },
                new() { EventTypes = new HashSet<UpsEventType>() },
                new() { DeviceId = "absent" },
                new() { EventTypes = new HashSet<UpsEventType> { UpsEventType.PowerLost, UpsEventType.BatteryCritical }, Severity = UpsEventSeverity.Warning },
            });
        foreach (var query in queries)
        {
            await ExportOperation.EventsAsync(store, file, query);
            var expected = query.DeviceId == "absent" ? [] : values.Where(query.Matches).ToArray();
            var lines = File.ReadAllLines(file).Skip(1).ToArray();
            Check(lines.Length == expected.Length, "EventQuery export count without display limit");
            foreach (var value in expected) Check(lines.Any(line => line.EndsWith(value.Message, StringComparison.Ordinal)), "same filtered messages");
        }
        // Export snapshot uses Logs' actual filters and intentionally ignores display-only search.
        using var vm = new LogsViewModel(store) { RangeHours = 168, EventType = UpsEventType.PowerLost, Severity = UpsEventSeverity.Warning, SearchText = "no match" };
        var current = vm.CreateExportQuery();
        Check(current.From.HasValue && current.EventTypes!.SetEquals(new[] { UpsEventType.PowerLost }) && current.Severity == UpsEventSeverity.Warning, "Logs filter snapshot");
        vm.EventType = UpsEventType.PowerRestored;
        Check(current.EventTypes!.Contains(UpsEventType.PowerLost), "snapshot stays stable");
        vm.ExportRange = 1;
        var all = vm.CreateExportQuery();
        Check(all.From is null && all.To is null && all.EventTypes is null && all.Severity is null, "all time clears filters");
        await ExportOperation.EventsAsync(store, file, all);
        Check(File.ReadAllLines(file).Length == values.Length + 1, "all saved events including latest queued events");
        for (var i = 0; i < 600; i++) await store.WriteAsync(new UpsEvent(Start.AddSeconds(i + 1),
            UpsEventType.PowerLost, "bulk-" + i, UpsPowerState.Online, UpsPowerState.OnBattery), default);
        await ExportOperation.EventsAsync(store, file, new() { Limit = 1 });
        Check(File.ReadAllLines(file).Length == values.Length + 601, "export never truncates at display limit");
    });

    public static void Large() => Run(async (store, root) =>
    {
        await using (var connection = new SqliteConnection(store.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                WITH RECURSIVE rows(n) AS (SELECT 1 UNION ALL SELECT n + 1 FROM rows WHERE n < 30000)
                INSERT INTO telemetry_samples(device_id,timestamp_utc_ms,is_connected,power_state,battery_percent)
                SELECT 'synthetic', $start + n, 1, 1, 90 FROM rows;
                """;
            command.Parameters.AddWithValue("$start", Start.ToUnixTimeMilliseconds());
            await command.ExecuteNonQueryAsync();
        }
        var json = Path.Combine(root, "large.json");
        var csv = Path.Combine(root, "large.csv");
        await ExportOperation.TelemetryAsync(store, json, TelemetryExportFormat.Json, DateTimeOffset.MinValue, DateTimeOffset.MaxValue);
        await TelemetryExporter.ExportAllTelemetryCsvAsync(store.DatabasePath, csv);
        using var stream = File.OpenRead(json);
        using var document = await JsonDocument.ParseAsync(stream);
        Check(document.RootElement.GetArrayLength() == 30000 && File.ReadLines(csv).Count() == 30001, "30000 rows parsed and CSV count matches");
    });

    public static void ShutdownCleanup() => Run(async (_, root) =>
    {
        var file = Path.Combine(root, "shutdown.json");
        await File.WriteAllTextAsync(file, "original");
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var worker = Task.Run(() => TelemetryExporter.WriteAtomicallyAsync(file, async temporary =>
        {
            await File.WriteAllTextAsync(temporary, "partial").ConfigureAwait(false);
            started.SetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, cancellation.Token).ConfigureAwait(false); }
            finally
            {
                cleanupStarted.SetResult();
                await releaseCleanup.Task.ConfigureAwait(false);
            }
        }, cancellation.Token));
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            cancellation.Cancel();
            await cleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var previousContext = SynchronizationContext.Current;
            var blockedDispatcher = new NonPumpingContext();
            SynchronizationContext.SetSynchronizationContext(blockedDispatcher);
            try
            {
                var shutdown = ExportShutdown.WaitForWorkerAsync(worker, ExportShutdown.WaitTimeout);
                Check(!shutdown.IsCompleted && Directory.EnumerateFiles(root, "*.tmp").Any(), "shutdown must wait while temp cleanup is pending");
                releaseCleanup.SetResult();
                // Like OnExit, synchronously block the caller without pumping dispatcher work.
                Check(shutdown.Wait(TimeSpan.FromSeconds(5)), "shutdown completes without a dispatcher continuation");
                Check(worker.IsCompleted && !Directory.EnumerateFiles(root, "*.tmp").Any(), "wait finishes only after atomic helper cleanup");
                Check(blockedDispatcher.PostCount == 0, "shutdown wait never captures the dispatcher");
            }
            finally { SynchronizationContext.SetSynchronizationContext(previousContext); }
            Check(await File.ReadAllTextAsync(file) == "original", "shutdown cancellation preserves existing destination");

            var stalledWorker = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await ExportShutdown.WaitForWorkerAsync(stalledWorker.Task, TimeSpan.Zero);
            Check(!stalledWorker.Task.IsCompleted, "shutdown returns when its bounded wait expires");
            stalledWorker.SetResult();
            await ExportShutdown.WaitForWorkerAsync(Task.FromException(new IOException("synthetic worker failure")), ExportShutdown.WaitTimeout);
            await ExportShutdown.WaitForWorkerAsync(null, ExportShutdown.WaitTimeout);
        }
        finally
        {
            cancellation.Cancel();
            releaseCleanup.TrySetResult();
            await ExportShutdown.WaitForWorkerAsync(worker, ExportShutdown.WaitTimeout);
        }
    });

    private sealed class NonPumpingContext : SynchronizationContext
    {
        private int _postCount;
        public int PostCount => Volatile.Read(ref _postCount);
        public override void Post(SendOrPostCallback callback, object? state) => Interlocked.Increment(ref _postCount);
    }

    public static void CancellationAndFailure() => Run(async (store, root) =>
    {
        var file = Path.Combine(root, "keep.json");
        await File.WriteAllTextAsync(file, "original");
        using var cts = new CancellationTokenSource();
        // Cancel after staging has begun, before publication. Deterministic, without timing races.
        try
        {
            await TelemetryExporter.WriteAtomicallyAsync(file, async temporary =>
            {
                await File.WriteAllTextAsync(temporary, "partial");
                cts.Cancel();
            }, cts.Token);
            throw new InvalidOperationException("expected cancellation");
        }
        catch (OperationCanceledException) { }
        Check(await File.ReadAllTextAsync(file) == "original" && !Directory.EnumerateFiles(root, "*.tmp").Any(), "cancel keeps destination and removes staging");
        try
        {
            await TelemetryExporter.WriteAtomicallyAsync(file, async temporary =>
            {
                await File.WriteAllTextAsync(temporary, "partial");
                throw new IOException("synthetic disk error");
            }, default);
            throw new InvalidOperationException("expected IO failure");
        }
        catch (IOException) { }
        Check(await File.ReadAllTextAsync(file) == "original" && !Directory.EnumerateFiles(root, "*.tmp").Any(), "IO failure preserves destination and cleans staging");
        foreach (var format in Enum.GetValues<TelemetryExportFormat>())
        {
            try { await ExportOperation.TelemetryAsync(store, file, format, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, cts.Token); throw new InvalidOperationException("expected cancellation"); }
            catch (OperationCanceledException) { }
        }
        try { await TelemetryExporter.ExportAllTelemetryJsonAsync(Path.Combine(root, "missing.db"), file); throw new InvalidOperationException("expected database failure"); }
        catch (SqliteException) { }
        // Publication failure after a complete streaming write must clean its staging file too.
        var directory = Path.Combine(root, "destination-directory");
        Directory.CreateDirectory(directory);
        try { await TelemetryExporter.ExportAllTelemetryJsonAsync(store.DatabasePath, directory); throw new InvalidOperationException("expected publication failure"); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        Check(await File.ReadAllTextAsync(file) == "original" && !Directory.EnumerateFiles(root, "*.tmp").Any(), "all failure paths preserve destination and clean staging");
        // Fail inside the real JSON row writer, after it has already emitted a valid row.
        await Snapshot(store, Start);
        await store.FlushAsync();
        await using (var connection = new SqliteConnection(store.ConnectionString))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO telemetry_samples(device_id,timestamp_utc_ms,is_connected,power_state,input_voltage) VALUES ('synthetic-invalid', $time, 1, 1, $value);";
            command.Parameters.AddWithValue("$time", Start.AddSeconds(1).ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("$value", double.PositiveInfinity);
            await command.ExecuteNonQueryAsync();
        }
        try { await TelemetryExporter.ExportAllTelemetryJsonAsync(store.DatabasePath, file); throw new InvalidOperationException("expected invalid JSON number failure"); }
        catch (ArgumentException) { }
        Check(await File.ReadAllTextAsync(file) == "original" && !Directory.EnumerateFiles(root, "*.tmp").Any(), "real streaming failure preserves destination and cleans staging");
    });
}
