using UpsMonitor.Core;

var tests = new (string Name, Action Run)[]
{
    ("Power state priority", PowerStatePriority),
    ("Power loss and restore events", PowerLossAndRestore),
    ("Alarm edge events", AlarmEdges),
    ("Disconnect and reconnect events", DisconnectAndReconnect),
    ("Invalid charge is rejected, not clamped", InvalidChargeRejected),
    ("Percentage capacities are not physical SOH", PercentageCapacityRejected),
    ("Physical capacity ratio calculates SOH", PhysicalCapacityRatio),
    ("Runtime baseline calculates comparable-load SOH", RuntimeBaselineHealth),
    ("Missing baseline leaves health unknown", MissingBaselineIsUnknown),
    ("Hard battery failures override score", HardFailureOverridesScore),
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

static void InvalidChargeRejected()
{
    var telemetry = UpsTelemetryValidator.Normalize(Snapshot(battery: 120));
    Equal(TelemetryQuality.Invalid, telemetry.BatteryChargePercent.Quality);
    Equal(120d, telemetry.BatteryChargePercent.Value);
    Equal(TelemetryValidationIssueCode.OutOfRange, telemetry.BatteryChargePercent.Issue);
}

static void PercentageCapacityRejected()
{
    var items = new[]
    {
        CapacityItem(0x83, 100, "%", hidUnit: 0, logicalMaximum: 100),
        CapacityItem(0x67, 100, "%", hidUnit: 0, logicalMaximum: 100),
    };
    var telemetry = UpsTelemetryValidator.Normalize(Snapshot(
        designCapacity: 100,
        fullChargeCapacity: 100,
        telemetry: items));

    Equal(TelemetryQuality.Invalid, telemetry.DesignCapacity.Quality);
    Equal(TelemetryQuality.Invalid, telemetry.FullChargeCapacity.Quality);
    var health = BatteryHealthCalculator.Calculate(telemetry, null);
    Equal(null, health.HealthPercent);
    Equal(BatteryHealthStatus.Unknown, health.Status);
}

static void PhysicalCapacityRatio()
{
    var items = new[]
    {
        CapacityItem(0x83, 9000, "As", hidUnit: 0x00100001, logicalMaximum: 20_000),
        CapacityItem(0x67, 7200, "As", hidUnit: 0x00100001, logicalMaximum: 20_000),
    };
    var telemetry = UpsTelemetryValidator.Normalize(Snapshot(
        designCapacity: 9000,
        fullChargeCapacity: 7200,
        telemetry: items));
    var health = BatteryHealthCalculator.Calculate(telemetry, null);

    Near(80, health.HealthPercent);
    Equal(BatteryHealthMethod.CapacityRatio, health.PrimaryMethod);
    Equal(BatteryHealthStatus.Good, health.Status);
}

static void RuntimeBaselineHealth()
{
    var telemetry = UpsTelemetryValidator.Normalize(Snapshot(
        battery: 100,
        load: 20,
        fullyCharged: true,
        runtime: TimeSpan.FromMinutes(39)));
    var profile = new BatteryHealthProfile
    {
        DeviceId = "test",
        RuntimeBaselines =
        [
            new BatteryRuntimeBaselinePoint
            {
                LoadPercent = 20,
                Runtime = TimeSpan.FromMinutes(52),
                MeasuredAt = DateTimeOffset.UtcNow.AddYears(-2),
            },
        ],
    };
    var health = BatteryHealthCalculator.Calculate(telemetry, profile);

    Near(75, health.HealthPercent);
    Equal(BatteryHealthMethod.RuntimeBaseline, health.PrimaryMethod);
    Equal(BatteryHealthStatus.Fair, health.Status);
    Equal(BatteryHealthConfidence.Low, health.Confidence);
}

static void MissingBaselineIsUnknown()
{
    var telemetry = UpsTelemetryValidator.Normalize(Snapshot(
        battery: 100,
        load: 20,
        fullyCharged: true,
        runtime: TimeSpan.FromMinutes(39)));
    var health = BatteryHealthCalculator.Calculate(telemetry, null);

    Equal(null, health.HealthPercent);
    Equal(BatteryHealthStatus.Unknown, health.Status);
    Equal(BatteryHealthConfidence.Unknown, health.Confidence);
}

static void HardFailureOverridesScore()
{
    var telemetry = UpsTelemetryValidator.Normalize(Snapshot(
        replacement: true,
        selfTest: "Done - error"));
    var health = BatteryHealthCalculator.Calculate(telemetry, null);

    Equal(null, health.HealthPercent);
    Equal(BatteryHealthStatus.Critical, health.Status);
    Equal(BatteryHealthConfidence.High, health.Confidence);
}

static UpsSnapshot Snapshot(
    bool connected = true,
    bool? ac = null,
    bool? discharging = null,
    bool? low = null,
    bool? critical = null,
    bool? overload = null,
    TimeSpan? runtime = null,
    double? battery = null,
    double? load = null,
    bool? fullyCharged = null,
    bool? replacement = null,
    string? selfTest = null,
    double? designCapacity = null,
    double? fullChargeCapacity = null,
    IReadOnlyList<UpsTelemetryItem>? telemetry = null) => new()
    {
        Timestamp = DateTimeOffset.UtcNow,
        IsConnected = connected,
        AcPresent = ac,
        Discharging = discharging,
        LowBattery = low,
        ShutdownImminent = critical,
        Overload = overload,
        RuntimeRemaining = runtime,
        BatteryPercent = battery,
        PercentLoad = load,
        FullyCharged = fullyCharged,
        NeedReplacement = replacement,
        SelfTestState = selfTest,
        DesignCapacity = designCapacity,
        FullChargeCapacity = fullChargeCapacity,
        Telemetry = telemetry ?? [],
    };

static UpsTelemetryItem CapacityItem(
    ushort usage,
    double value,
    string unit,
    uint hidUnit,
    int logicalMaximum) => new()
    {
        Key = $"Feature:1:0085:{usage:X4}:0",
        ReportType = "Feature",
        ReportId = 1,
        UsagePage = 0x85,
        Usage = usage,
        UsagePageName = "Battery System",
        UsageName = usage == 0x83 ? "DesignCapacity" : "FullChargeCapacity",
        LinkCollection = 0,
        CollectionPath = "UPS / PowerSummary",
        IsReadable = true,
        HasValue = true,
        RawValue = (long)value,
        NumericValue = value,
        DisplayValue = value.ToString(),
        UnitSymbol = unit,
        LogicalMinimum = 0,
        LogicalMaximum = logicalMaximum,
        PhysicalMinimum = 0,
        PhysicalMaximum = logicalMaximum,
        HidUnit = hidUnit,
        UnitExponent = 0,
        BitSize = 16,
        ReportCount = 1,
        IsButton = false,
        IsVendorDefined = false,
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

static void Near(double expected, double? actual, double tolerance = 0.001)
{
    if (actual is null || Math.Abs(expected - actual.Value) > tolerance)
    {
        throw new InvalidOperationException($"Expected approximately {expected}, got {actual?.ToString() ?? "null"}.");
    }
}
