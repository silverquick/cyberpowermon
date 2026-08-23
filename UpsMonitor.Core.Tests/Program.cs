using Microsoft.Data.Sqlite;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

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
    ("Current baseline reports relative trend only", CurrentRelativeBaseline),
    ("Relative runtime decline requests a battery check", RelativeRuntimeDecline),
    ("Known BHI anchors the runtime estimate", KnownHealthAnchor),
    ("Missing baseline leaves health unknown", MissingBaselineIsUnknown),
    ("Hard battery failures override score", HardFailureOverridesScore),
    ("Self-test failure requests a battery check", SelfTestFailureRequestsCheck),
    ("SQLite history stores samples, rollups, events, and health", SqliteHistoryRoundTrip),
    ("Event severity classification", EventSeverityClassification),
    ("Telemetry and event export to CSV/JSON", TelemetryExportRoundTrip),
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
    Equal(BatteryReplacementStatus.NoSignal, health.Replacement.Status);
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
        RuntimeBaselineKind = BatteryRuntimeBaselineKind.NewBattery,
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
    Equal(BatteryReplacementStatus.ConsiderReplacement, health.Replacement.Status);
    Equal(BatteryReplacementReasonCode.NewBatteryRuntimeBelowReference, health.Replacement.Reasons[0].Code);
}

static void CurrentRelativeBaseline()
{
    var telemetry = UpsTelemetryValidator.Normalize(Snapshot(
        battery: 100,
        load: 20,
        fullyCharged: true,
        runtime: TimeSpan.FromMinutes(46.8)));
    var profile = RuntimeProfile(BatteryRuntimeBaselineKind.CurrentRelative);
    var health = BatteryHealthCalculator.Calculate(telemetry, profile);

    Equal(null, health.HealthPercent);
    Near(90, health.RelativePerformancePercent);
    Equal(BatteryHealthMethod.RelativeRuntimeTrend, health.PrimaryMethod);
    Equal(BatteryHealthStatus.Unknown, health.Status);
    Equal(BatteryHealthConfidence.Low, health.Confidence);
    Equal(BatteryReplacementStatus.NoSignal, health.Replacement.Status);
}

static void RelativeRuntimeDecline()
{
    var telemetry = UpsTelemetryValidator.Normalize(Snapshot(
        battery: 100,
        load: 20,
        fullyCharged: true,
        runtime: TimeSpan.FromMinutes(36.4)));
    var health = BatteryHealthCalculator.Calculate(
        telemetry,
        RuntimeProfile(BatteryRuntimeBaselineKind.CurrentRelative));

    Near(70, health.RelativePerformancePercent);
    Equal(BatteryReplacementStatus.CheckRequired, health.Replacement.Status);
    Equal(BatteryReplacementReasonCode.RelativeRuntimeDeclined, health.Replacement.Reasons[0].Code);
}

static void KnownHealthAnchor()
{
    var telemetry = UpsTelemetryValidator.Normalize(Snapshot(
        battery: 100,
        load: 20,
        fullyCharged: true,
        runtime: TimeSpan.FromMinutes(46.8)));
    var profile = RuntimeProfile(BatteryRuntimeBaselineKind.KnownHealthAnchor) with
    {
        AnchorHealthPercent = 59,
        AnchorSource = "CyberPower BHI",
        VendorHealthCategory = VendorBatteryHealthCategory.Poor,
    };
    var health = BatteryHealthCalculator.Calculate(telemetry, profile);

    Near(53.1, health.HealthPercent);
    Near(90, health.RelativePerformancePercent);
    Near(59, health.AnchorHealthPercent);
    Equal("CyberPower BHI", health.AnchorSource);
    Equal(BatteryHealthMethod.VendorAnchoredRuntime, health.PrimaryMethod);
    Equal(BatteryHealthConfidence.Medium, health.Confidence);
    Equal(VendorBatteryHealthCategory.Poor, health.VendorHealthCategory);
    Equal(BatteryHealthStatus.VendorReported, health.Status);
    Equal(BatteryReplacementStatus.NoSignal, health.Replacement.Status);
    Equal(0, health.Replacement.Reasons.Count);
}

static BatteryHealthProfile RuntimeProfile(BatteryRuntimeBaselineKind kind) => new()
{
    DeviceId = "test",
    RuntimeBaselineKind = kind,
    BaselineRecordedAt = DateTimeOffset.UtcNow,
    RuntimeBaselines =
    [
        new BatteryRuntimeBaselinePoint
        {
            LoadPercent = 20,
            Runtime = TimeSpan.FromMinutes(52),
            MeasuredAt = DateTimeOffset.UtcNow,
        },
    ],
};

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
    Equal(BatteryReplacementStatus.NoSignal, health.Replacement.Status);
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
    Equal(BatteryReplacementStatus.ReplacementRequested, health.Replacement.Status);
    Equal(BatteryReplacementReasonCode.NeedReplacementReported, health.Replacement.Reasons[0].Code);
}

static void SelfTestFailureRequestsCheck()
{
    var telemetry = UpsTelemetryValidator.Normalize(Snapshot(selfTest: "Done - error"));
    var health = BatteryHealthCalculator.Calculate(telemetry, null);

    Equal(BatteryHealthStatus.Critical, health.Status);
    Equal(BatteryReplacementStatus.CheckRequired, health.Replacement.Status);
    Equal(BatteryReplacementReasonCode.SelfTestFailed, health.Replacement.Reasons[0].Code);
}

static void SqliteHistoryRoundTrip() => SqliteHistoryRoundTripAsync().GetAwaiter().GetResult();

static async Task SqliteHistoryRoundTripAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), "UpsMonitor.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot, testRoot);
        await using var store = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        await store.InitializeAsync();
        var device = new UpsDeviceInfo(
            "test-path",
            0x0764,
            0x0601,
            "CPS",
            "Test UPS",
            "TEST01",
            0x84,
            0x04,
            64,
            64);
        var start = DateTimeOffset.UtcNow.AddMinutes(-2);
        var raw1 = CapacityItem(0x40, 98, "V", 0, 255) with { Key = "Input:1:0084:0040:0", UsagePage = 0x84 };
        var raw2 = raw1 with { NumericValue = 99, RawValue = 99, DisplayValue = "99" };
        var first = Snapshot(
            ac: true,
            runtime: TimeSpan.FromMinutes(12),
            battery: 100,
            load: 50,
            telemetry: [raw1]) with
        {
            Timestamp = start,
            Device = device,
            InputVoltage = 98,
            OutputVoltage = 98,
            BatteryVoltage = 26.4,
            ActivePower = 380,
            ApparentPower = 382,
        };
        var second = first with
        {
            Timestamp = start.AddSeconds(2),
            InputVoltage = 99,
            OutputVoltage = 99,
            ActivePower = 385,
            Telemetry = [raw2],
        };

        await store.WriteAsync(first, CancellationToken.None);
        await store.WriteAsync(second, CancellationToken.None);
        await store.WriteAsync(
            new UpsEvent(
                start.AddSeconds(1),
                UpsEventType.PowerRestored,
                "restored",
                UpsPowerState.OnBattery,
                UpsPowerState.Online),
            CancellationToken.None);
        await store.RecordBatteryHealthAsync(new BatteryHealthObservation
        {
            DeviceId = UpsDeviceIdentity.Create(device),
            Timestamp = start,
            HealthPercent = 59,
            RelativePerformancePercent = 100,
            Status = BatteryHealthStatus.VendorReported,
            Method = BatteryHealthMethod.VendorAnchoredRuntime,
            Confidence = BatteryHealthConfidence.Medium,
            AnchorSource = "CyberPower BHI",
            VendorCategory = VendorBatteryHealthCategory.Unknown,
            ReplacementStatus = BatteryReplacementStatus.NoSignal,
        });
        var third = second with
        {
            Timestamp = start.AddMinutes(2),
            InputVoltage = 101,
            OutputVoltage = 101,
            ActivePower = 390,
        };
        await store.WriteAsync(third, CancellationToken.None);
        await store.FlushAsync();

        var result = await store.QueryHistoryAsync(
            UpsDeviceIdentity.Create(device),
            start.AddMinutes(-1),
            start.AddMinutes(3),
            [TelemetryMetric.InputVoltage, TelemetryMetric.ActivePowerWatts]);
        var statistics = await store.GetStatisticsAsync();

        Equal(3L, result.SourceSampleCount);
        Equal(3, result.Metrics[TelemetryMetric.InputVoltage].Points.Count);
        Near(98, result.Metrics[TelemetryMetric.InputVoltage].Points[0].Average);
        Near(385, result.Metrics[TelemetryMetric.ActivePowerWatts].Points[1].Average);
        Near(101, result.Metrics[TelemetryMetric.InputVoltage].Points[2].Average);
        Equal(1, result.Events.Count);
        Equal(1, result.StateChanges.Count);
        Equal(1, result.BatteryHealth.Count);
        Equal(3L, statistics.SampleCount);
        Equal(2L, statistics.RawValueCount);
        Equal(1L, statistics.EventCount);
    }
    finally
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
}

static void EventSeverityClassification()
{
    var evInfo = new UpsEvent(DateTimeOffset.UtcNow, UpsEventType.PowerRestored, "restored", UpsPowerState.OnBattery, UpsPowerState.Online);
    Equal(UpsEventSeverity.Information, evInfo.Severity);

    var evWarn = new UpsEvent(DateTimeOffset.UtcNow, UpsEventType.PowerLost, "lost", UpsPowerState.Online, UpsPowerState.OnBattery);
    Equal(UpsEventSeverity.Warning, evWarn.Severity);

    var evCrit = new UpsEvent(DateTimeOffset.UtcNow, UpsEventType.BatteryCritical, "crit", UpsPowerState.LowBattery, UpsPowerState.Critical);
    Equal(UpsEventSeverity.Critical, evCrit.Severity);

    var evOverload = new UpsEvent(DateTimeOffset.UtcNow, UpsEventType.OverloadDetected, "overload", UpsPowerState.Online, UpsPowerState.Online);
    Equal(UpsEventSeverity.Critical, evOverload.Severity);
}

static void TelemetryExportRoundTrip()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsExportTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);

    SqliteTelemetryStore? store = null;
    try
    {
        var paths = new AppPaths(testRoot, testRoot);
        var dbPath = paths.TelemetryDatabaseFile;
        var config = new HistoryConfiguration { RawRetentionDays = 1, RawUsageCheckpointSeconds = 30 };
        store = new SqliteTelemetryStore(paths, config);
        store.InitializeAsync().GetAwaiter().GetResult();

        var device = new UpsDeviceInfo(
            "test-path",
            0x0764,
            0x0601,
            "Vendor",
            "Product",
            "12345",
            0x84,
            0x04,
            64,
            64);
        var start = DateTimeOffset.UtcNow.AddMinutes(-5);

        var snap = Snapshot(connected: true, ac: true, battery: 100, load: 25, runtime: TimeSpan.FromMinutes(40));
        snap = snap with { Device = device, Timestamp = start };
        store.WriteAsync(snap, CancellationToken.None).GetAwaiter().GetResult();

        var ev = new UpsEvent(start.AddSeconds(10), UpsEventType.PowerLost, "Power lost test", UpsPowerState.Online, UpsPowerState.OnBattery);
        store.WriteAsync(ev, CancellationToken.None).GetAwaiter().GetResult();
        store.FlushAsync().GetAwaiter().GetResult();

        var csvTelemetryFile = Path.Combine(testRoot, "telemetry.csv");
        var jsonTelemetryFile = Path.Combine(testRoot, "telemetry.json");
        var csvEventsFile = Path.Combine(testRoot, "events.csv");

        TelemetryExporter.ExportTelemetryCsvAsync(dbPath, csvTelemetryFile, start.AddMinutes(-1), start.AddMinutes(1)).GetAwaiter().GetResult();
        TelemetryExporter.ExportTelemetryJsonAsync(dbPath, jsonTelemetryFile, start.AddMinutes(-1), start.AddMinutes(1)).GetAwaiter().GetResult();
        TelemetryExporter.ExportEventsCsvAsync(dbPath, csvEventsFile, start.AddMinutes(-1), start.AddMinutes(1)).GetAwaiter().GetResult();

        if (!File.Exists(csvTelemetryFile) || new FileInfo(csvTelemetryFile).Length == 0)
        {
            throw new InvalidOperationException("CSV telemetry export file is missing or empty.");
        }

        if (!File.Exists(jsonTelemetryFile) || new FileInfo(jsonTelemetryFile).Length == 0)
        {
            throw new InvalidOperationException("JSON telemetry export file is missing or empty.");
        }

        if (!File.Exists(csvEventsFile) || new FileInfo(csvEventsFile).Length == 0)
        {
            throw new InvalidOperationException("CSV events export file is missing or empty.");
        }

        var csvLines = File.ReadAllLines(csvTelemetryFile);
        if (csvLines.Length < 2)
        {
            throw new InvalidOperationException("CSV telemetry export did not write header and data lines.");
        }

        var evLines = File.ReadAllLines(csvEventsFile);
        if (evLines.Length < 2 || !evLines[1].Contains("PowerLost"))
        {
            throw new InvalidOperationException("CSV events export does not contain expected PowerLost event.");
        }
    }
    finally
    {
        if (store is not null)
        {
            store.DisposeAsync().GetAwaiter().GetResult();
        }
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(testRoot))
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }
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
