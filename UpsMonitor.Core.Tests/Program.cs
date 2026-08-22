using UpsMonitor.Core;

var tests = new (string Name, Action Run)[]
{
    ("Power state priority", PowerStatePriority),
    ("Power loss and restore events", PowerLossAndRestore),
    ("Alarm edge events", AlarmEdges),
    ("Disconnect and reconnect events", DisconnectAndReconnect),
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {test.Name}: {exception.Message}");
        Console.Error.WriteLine(failures[^1]);
    }
}

Console.WriteLine($"{tests.Length - failures.Count}/{tests.Length} tests passed.");
if (failures.Count > 0)
{
    Environment.ExitCode = 1;
}

static void PowerStatePriority()
{
    Equal(UpsPowerState.Unknown, UpsPowerStateEvaluator.Evaluate(Snapshot(connected: false)));
    Equal(UpsPowerState.Online, UpsPowerStateEvaluator.Evaluate(Snapshot(ac: true)));
    Equal(UpsPowerState.OnBattery, UpsPowerStateEvaluator.Evaluate(Snapshot(ac: false, discharging: true)));
    Equal(UpsPowerState.LowBattery, UpsPowerStateEvaluator.Evaluate(Snapshot(ac: false, low: true)));
    Equal(UpsPowerState.Critical, UpsPowerStateEvaluator.Evaluate(Snapshot(ac: false, low: true, critical: true)));
}

static void PowerLossAndRestore()
{
    var detector = new UpsEventDetector(TimeSpan.FromMinutes(3));
    var initial = detector.Observe(Snapshot(ac: true));
    HasSingle(initial, UpsEventType.UpsReconnected);

    var lost = detector.Observe(Snapshot(ac: false, discharging: true));
    HasSingle(lost, UpsEventType.PowerLost);
    Equal(UpsPowerState.OnBattery, lost[0].CurrentState);

    var restored = detector.Observe(Snapshot(ac: true));
    HasSingle(restored, UpsEventType.PowerRestored);
    Equal(UpsPowerState.Online, restored[0].CurrentState);
}

static void AlarmEdges()
{
    var detector = new UpsEventDetector(TimeSpan.FromMinutes(3));
    _ = detector.Observe(Snapshot(ac: false, runtime: TimeSpan.FromMinutes(10)));

    var alarms = detector.Observe(Snapshot(
        ac: false,
        discharging: true,
        low: true,
        critical: true,
        overload: true,
        runtime: TimeSpan.FromMinutes(2)));

    HasType(alarms, UpsEventType.BatteryLow);
    HasType(alarms, UpsEventType.BatteryCritical);
    HasType(alarms, UpsEventType.OverloadDetected);
    HasType(alarms, UpsEventType.RuntimeLow);

    var repeated = detector.Observe(Snapshot(
        ac: false,
        discharging: true,
        low: true,
        critical: true,
        overload: true,
        runtime: TimeSpan.FromMinutes(1)));
    Equal(0, repeated.Count);
}

static void DisconnectAndReconnect()
{
    var detector = new UpsEventDetector(TimeSpan.FromMinutes(3));
    _ = detector.Observe(Snapshot(ac: true));
    HasSingle(detector.Observe(Snapshot(connected: false)), UpsEventType.UpsDisconnected);
    HasSingle(detector.Observe(Snapshot(ac: true)), UpsEventType.UpsReconnected);
}

static UpsSnapshot Snapshot(
    bool connected = true,
    bool? ac = null,
    bool? discharging = null,
    bool? low = null,
    bool? critical = null,
    bool? overload = null,
    TimeSpan? runtime = null) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        IsConnected = connected,
        AcPresent = ac,
        Discharging = discharging,
        LowBattery = low,
        ShutdownImminent = critical,
        Overload = overload,
        RuntimeRemaining = runtime,
    };

static void HasSingle(IReadOnlyList<UpsEvent> events, UpsEventType type)
{
    Equal(1, events.Count);
    Equal(type, events[0].Type);
}

static void HasType(IReadOnlyList<UpsEvent> events, UpsEventType type)
{
    if (!events.Any(item => item.Type == type))
    {
        throw new InvalidOperationException($"Expected event {type} was not present.");
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}
