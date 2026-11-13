using System.Collections.Concurrent;
using Microsoft.Data.Sqlite;
using UpsMonitor.App;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

internal static class SavedEventTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly UpsDeviceInfo Device = new("synthetic", 0x0764, 0x0601, "Test", "UPS", "FILTER-TEST", 0x84, 4, 64, 64);
    private static UpsEvent Event(int milliseconds, UpsEventType type = UpsEventType.PowerLost, string? message = null) =>
        new(Start.AddMilliseconds(milliseconds), type, message ?? $"synthetic-{milliseconds}", UpsPowerState.Online, UpsPowerState.OnBattery);
    private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

    private static async Task RunStore(Func<SqliteTelemetryStore, AppPaths, Task> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "UpsMonitor.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var paths = new AppPaths(root, root);
            await using var store = new SqliteTelemetryStore(paths, new HistoryConfiguration());
            await store.InitializeAsync();
            await test(store, paths);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }
    private static async Task Seed(SqliteTelemetryStore store, IEnumerable<UpsEvent> events, UpsDeviceInfo? device = null)
    {
        await store.WriteAsync(new UpsSnapshot { Timestamp = Start, Device = device ?? Device, IsConnected = true }, default);
        foreach (var value in events) await store.WriteAsync(value, default);
    }

    public static void Empty() => RunStore(async (store, _) =>
    {
        var result = await store.QueryEventsAsync(new());
        Check(result.Events.Count == 0 && !result.HasMore, "empty database");
        Check((await store.QueryEventsAsync(new() { DeviceId = "absent" })).Events.Count == 0, "absent device");
    }).GetAwaiter().GetResult();

    public static void Boundaries() => RunStore(async (store, _) =>
    {
        await Seed(store, new[] { Event(-1), Event(0), Event(1), Event(2) });
        var query = new EventQuery { From = Start.ToOffset(TimeSpan.FromHours(9)), To = Start.AddMilliseconds(2) };
        var result = await store.QueryEventsAsync(query);
        Check(result.Events.Select(e => e.Message).SequenceEqual(new[] { "synthetic-1", "synthetic-0" }), "[From,To), newest first, offset");
        Check((await store.QueryEventsAsync(new() { To = Start })).Events.Count == 1, "open lower bound");
        Check((await store.QueryEventsAsync(new() { From = Start.AddMilliseconds(2) })).Events.Count == 1, "open upper bound");
    }).GetAwaiter().GetResult();

    public static void TypeSeverity() => RunStore(async (store, _) =>
    {
        var values = Enum.GetValues<UpsEventType>().Select((t, i) => Event(i, t)).Append(Event(20, (UpsEventType)999)).ToArray();
        await Seed(store, values);
        foreach (var severity in Enum.GetValues<UpsEventSeverity>())
        {
            var expected = values.Where(e => e.Severity == severity).OrderByDescending(e => e.Timestamp).ToArray();
            var result = await store.QueryEventsAsync(new() { Severity = severity });
            Check(result.Events.SequenceEqual(expected), $"severity {severity} including unknown enum");
        }
        var types = new HashSet<UpsEventType> { UpsEventType.PowerLost, UpsEventType.PowerRestored, UpsEventType.BatteryCritical };
        var query = new EventQuery { From = Start, To = Start.AddSeconds(1), EventTypes = types, Severity = UpsEventSeverity.Warning, Limit = 1 };
        var combined = await store.QueryEventsAsync(query);
        Check(combined.Events.Count == 1 && combined.Events[0].Type == UpsEventType.PowerLost && !combined.HasMore, "combined filters before limit");
        Check((await store.QueryEventsAsync(query with { EventTypes = new HashSet<UpsEventType>() })).Events.Count == 0, "empty set matches nothing");
        Check((await store.QueryEventsAsync(query with { EventTypes = new HashSet<UpsEventType> { UpsEventType.BatteryCritical } })).Events.Count == 0, "type/severity intersection");
        Check((await store.QueryEventsAsync(new() { EventTypes = types })).Events.Count == 3, "multiple types");
    }).GetAwaiter().GetResult();

    public static void Limit() => RunStore(async (store, _) =>
    {
        await Seed(store, Enumerable.Range(0, 510).Select(i => Event(i)));
        var result = await store.QueryEventsAsync(new());
        Check(result.Events.Count == 500 && result.HasMore && result.Events[0].Timestamp == Start.AddMilliseconds(509), "500 limit plus overflow");
        result = await store.QueryEventsAsync(new() { From = Start.AddMilliseconds(10) });
        Check(result.Events.Count == 500 && !result.HasMore, "exact limit");
        result = await store.QueryEventsAsync(new() { Limit = 1 });
        Check(result.Events.Count == 1 && result.HasMore, "small limit");
    }).GetAwaiter().GetResult();

    public static void IndexUsage() => RunStore(async (store, _) =>
    {
        await using var connection = new SqliteConnection(store.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + SqliteTelemetryStore.SavedEventQuerySql;
        command.Parameters.AddWithValue("$device", UpsDeviceIdentity.Create(Device));
        command.Parameters.AddWithValue("$from", Start.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$to", Start.AddDays(1).ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$types", DBNull.Value);
        command.Parameters.AddWithValue("$excludedTypes", DBNull.Value);
        command.Parameters.AddWithValue("$limit", 501);
        var plan = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) plan.Add(reader.GetString(3));
        Check(plan.Any(s => s.Contains("SEARCH ups_events USING INDEX ix_events_device_time") && s.Contains("timestamp_utc_ms")), "existing device/timestamp index range search");
        Console.WriteLine("Saved events plan: " + plan.First(s => s.Contains("SEARCH ups_events USING INDEX ix_events_device_time")));
    }).GetAwaiter().GetResult();

    public static void ReopenDevices() => RunStore(async (store, paths) =>
    {
        await Seed(store, new[] { Event(0) });
        await Seed(store, new[] { Event(1, message: "second device") }, Device with { SerialNumber = "SECOND" });
        await store.FlushAsync();
        await store.DisposeAsync();
        await using var reopened = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        Check((await reopened.QueryEventsAsync(new())).Events.Count == 2, "reopen without a connected UPS");
        Check((await reopened.QueryEventsAsync(new() { DeviceId = UpsDeviceIdentity.Create(Device) })).Events.Single().Message == "synthetic-0", "device isolation");
        Check((await reopened.QueryEventsAsync(new() { DeviceId = "' OR 1=1 --" })).Events.Count == 0, "parameterized device");
    }).GetAwaiter().GetResult();

    public static void Deduplication() => RunStore(async (store, _) =>
    {
        var live = Event(0) with { Timestamp = Start.AddTicks(1234) };
        await Seed(store, new[] { live, live, Event(1) });
        var saved = await store.QueryEventsAsync(new() { Limit = 2 });
        Check(saved.Events.Count == 2 && !saved.HasMore, "stored duplicate before limit");
        var merged = EventQueryResults.Merge(new() { Limit = 2 }, saved, new[] { live });
        Check(merged.Events.Count == 2 && !merged.HasMore, "live timestamp precision matches SQLite");
        merged = EventQueryResults.Merge(new() { Limit = 2 }, saved, new[] { live with { Message = "different" } });
        Check(merged.Events.Count == 2 && merged.HasMore, "different original message stays distinct");
        merged = EventQueryResults.Merge(new() { EventTypes = new HashSet<UpsEventType> { UpsEventType.PowerRestored } }, saved, new[] { live });
        Check(merged.Events.Count == 0, "live obeys current filters");
    }).GetAwaiter().GetResult();

    public static void ValidationCancellation() => RunStore(async (store, _) =>
    {
        foreach (var query in new[] { new EventQuery { Limit = 0 }, new EventQuery { Limit = 10001 }, new EventQuery { From = Start, To = Start } })
        {
            try { await store.QueryEventsAsync(query); throw new InvalidOperationException("invalid query accepted"); }
            catch (ArgumentOutOfRangeException) { }
        }
        var types = new HashSet<UpsEventType> { UpsEventType.PowerLost };
        var snapshot = new EventQuery { EventTypes = types }.Snapshot(); types.Clear();
        Check(snapshot.EventTypes!.Count == 1, "immutable query snapshot");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { await store.QueryEventsAsync(new(), cancellation.Token); throw new InvalidOperationException("cancel ignored"); }
        catch (OperationCanceledException) { }
    }).GetAwaiter().GetResult();

    public static void ViewModelLatestWins() => UiRun(async () =>
    {
        var calls = new ConcurrentQueue<(EventQuery Query, CancellationToken Token, TaskCompletionSource<EventQueryResult> Completion)>();
        using var vm = new LogsViewModel((query, token) =>
        {
            var completion = new TaskCompletionSource<EventQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            calls.Enqueue((query, token, completion)); return completion.Task; // Deliberately ignores cancellation.
        });
        vm.SetActive(true); await Until(() => calls.Count == 1);
        Check(calls.TryDequeue(out var old), "first query");
        vm.RangeHours = 168; vm.EventType = UpsEventType.PowerRestored; vm.Severity = UpsEventSeverity.Information;
        await Until(() => calls.Count == 1);
        Check(calls.TryDequeue(out var latest), "latest debounced query");
        Check(old.Token.IsCancellationRequested && latest.Query.EventTypes!.SetEquals(new[] { UpsEventType.PowerRestored }) && latest.Query.Severity == UpsEventSeverity.Information, "cancel and coalesce");
        latest.Completion.SetResult(new(new[] { Event(1, UpsEventType.PowerRestored) with { Timestamp = DateTimeOffset.UtcNow } }, false));
        await Until(() => !vm.IsLoading);
        old.Completion.SetResult(new(new[] { Event(0) with { Timestamp = DateTimeOffset.UtcNow } }, false));
        await Task.Delay(30);
        Check(vm.FilteredEvents.Count == 1 && vm.FilteredEvents[0].Type == LocalizationManager.Get("EventPowerRestored"), "old result must not overwrite latest");
        vm.SetActive(false); vm.RangeHours = 0; await Task.Delay(330);
        Check(calls.IsEmpty, "inactive tab must not query");
    });

    public static void ViewModelLiveSearchLanguage() => UiRun(async () =>
    {
        using var vm = new LogsViewModel((SqliteTelemetryStore?)null);
        vm.RangeHours = 0; vm.SetActive(true); await Until(() => !vm.IsLoading);
        var value = Event(0, UpsEventType.HighLoadWarning);
        vm.AddLive(value); vm.AddLive(value);
        Check(vm.FilteredEvents.Count == 1, "duplicate live");
        vm.SearchText = "high load"; Check(vm.FilteredEvents.Count == 1, "localized text search");
        vm.SearchText = "absent"; Check(vm.FilteredEvents.Count == 0, "text search excludes");
        vm.SearchText = string.Empty;
        LocalizationManager.Language = "ja-JP"; vm.RefreshLanguage();
        Check(vm.FilteredEvents.Single().Type == "高負荷警告" && vm.TypeOptions.Single(o => o.Type == UpsEventType.HighLoadWarning).DisplayName == "高負荷警告", "language refresh");
        LocalizationManager.Language = "en-US"; vm.RefreshLanguage();
        for (var i = 1; i <= 501; i++) vm.AddLive(Event(i));
        Check(vm.FilteredEvents.Count == 500 && vm.LimitText.Length > 0, "live cap and hint");
    });

    public static void ViewModelFailureDisposal() => UiRun(async () =>
    {
        using var vm = new LogsViewModel((_, _) => Task.FromException<EventQueryResult>(new IOException("synthetic failure")));
        vm.SetActive(true); await Until(() => !vm.IsLoading);
        Check(vm.Status.Contains("synthetic failure"), "query failure is visible");
        vm.SetActive(false);
        var completion = new TaskCompletionSource<EventQueryResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var called = false;
        var pending = new LogsViewModel((_, _) => { called = true; return completion.Task; });
        pending.SetActive(true); await Until(() => Volatile.Read(ref called));
        pending.Dispose();
        completion.SetResult(new(new[] { Event(0) with { Timestamp = DateTimeOffset.UtcNow } }, false));
        await Task.Delay(30);
        Check(pending.FilteredEvents.Count == 0, "disposed VM ignores late completion");
    });

    private static async Task Until(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!predicate()) { if (DateTime.UtcNow > deadline) throw new TimeoutException("VM test timeout"); await Task.Delay(10); }
    }
    private static void UiRun(Func<Task> test)
    {
        var previous = SynchronizationContext.Current;
        using var context = new UiContext(); SynchronizationContext.SetSynchronizationContext(context);
        try
        {
            var task = test();
            while (!task.IsCompleted) if (context.Queue.TryTake(out var action, 100)) action();
            task.GetAwaiter().GetResult();
        }
        finally { SynchronizationContext.SetSynchronizationContext(previous); }
    }
    private sealed class UiContext : SynchronizationContext, IDisposable
    {
        public BlockingCollection<Action> Queue { get; } = new();
        public override void Post(SendOrPostCallback callback, object? state) => Queue.Add(() => callback(state));
        public void Dispose() => Queue.Dispose();
    }
}
