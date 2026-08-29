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
    ("Dynamic runtime-low threshold update", DynamicRuntimeLowThreshold),
    ("Weekly heatmap pattern aggregation", WeeklyPatternAggregation),
    ("Runtime estimator load calculation", RuntimeEstimatorCalculation),
    ("Configuration theme, alerts, webhook, and command settings", ConfigurationNewFeatures),
    ("Daily energy reports and trouble summary queries", DailyEnergyAndTroubleSummaryQueries),
    ("Performance benchmark and EXPLAIN QUERY PLAN", PerformanceBenchmark),
    ("Event detector zero-allocation when quiet", EventDetectorZeroAllocationWhenQuiet),
    ("Command runner execution, large output, and escaping", CommandRunnerExecutionAndEscaping),
    ("Webhook notifier validation", WebhookNotifierValidation),
    ("Polling engine lifecycle and interval", PollingEngineLifecycleAndInterval),
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
        Equal(true, result.Summary != null);
        Near(98, result.Summary!.MinInputVoltage);
        Near(101, result.Summary!.MaxInputVoltage);
        Near(390, result.Summary!.PeakActivePowerWatts);
        Equal(true, result.Summary!.TotalEnergyKwh.HasValue);
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

    var evVoltage = new UpsEvent(DateTimeOffset.UtcNow, UpsEventType.VoltageAbnormal, "voltage abnormal", UpsPowerState.Online, UpsPowerState.Online);
    Equal(UpsEventSeverity.Warning, evVoltage.Severity);

    var evHighLoad = new UpsEvent(DateTimeOffset.UtcNow, UpsEventType.HighLoadWarning, "high load", UpsPowerState.Online, UpsPowerState.Online);
    Equal(UpsEventSeverity.Warning, evHighLoad.Severity);
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

static void DynamicRuntimeLowThreshold()
{
    var detector = new UpsEventDetector(TimeSpan.FromMinutes(3));
    var snap1 = Snapshot(ac: false, discharging: true, runtime: TimeSpan.FromMinutes(5));
    var events1 = detector.Observe(snap1);
    // Should NOT have RuntimeLow since 5m > 3m
    Equal(false, events1.Any(e => e.Type == UpsEventType.RuntimeLow));

    // Dynamically increase threshold to 6 minutes
    detector.SetRuntimeLowThreshold(TimeSpan.FromMinutes(6));
    var snap2 = Snapshot(ac: false, discharging: true, runtime: TimeSpan.FromMinutes(5));
    var events2 = detector.Observe(snap2);
    // Now it should trigger RuntimeLow since 5m <= 6m
    HasType(events2, UpsEventType.RuntimeLow);
}

static void WeeklyPatternAggregation()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsPatternTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot, testRoot);
        var store = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        store.InitializeAsync().GetAwaiter().GetResult();

        var device = new UpsDeviceInfo("test-path", 0x0764, 0x0601, "Vendor", "Product", "TEST_WEEKLY", 0x84, 0x04, 64, 64);
        var targetTime = new DateTimeOffset(2026, 8, 24, 14, 30, 0, TimeSpan.FromHours(9)); // Monday 14:30

        var snapshot1 = Snapshot(
            device: device,
            activePower: 350.0,
            apparentPower: 400.0,
            load: 45.0,
            timestamp: targetTime);
        var snapshot2 = Snapshot(
            device: device,
            activePower: 450.0,
            apparentPower: 500.0,
            load: 55.0,
            timestamp: targetTime.AddMinutes(15));

        store.WriteAsync(snapshot1, CancellationToken.None).GetAwaiter().GetResult();
        store.WriteAsync(snapshot2, CancellationToken.None).GetAwaiter().GetResult();
        store.FlushAsync().GetAwaiter().GetResult();

        var result = store.QueryWeeklyPatternAsync(
            UpsDeviceIdentity.Create(device),
            targetTime.AddDays(-7),
            targetTime.AddDays(7),
            TelemetryMetric.ActivePowerWatts).GetAwaiter().GetResult();

        Equal(168, result.Grid.Count);
        Equal(TelemetryMetric.ActivePowerWatts, result.Metric);
        Equal(2L, result.TotalSamples);
        Near(400.0, result.OverallAvg);
        Near(350.0, result.OverallMin);
        Near(450.0, result.OverallMax);

        var mon14 = result.Grid.First(p => p.DayOfWeek == 1 && p.HourOfDay == 14);
        Equal(2L, mon14.SampleCount);
        Near(400.0, mon14.Average);

        store.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
    finally
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(testRoot))
        {
            try { Directory.Delete(testRoot, recursive: true); } catch { }
        }
    }
}

static void RuntimeEstimatorCalculation()
{
    // 1. Basic calculation: higher load -> shorter runtime
    var t100 = RuntimeEstimator.EstimateRuntime(100, batteryPercent: 100, sohPercent: 100);
    var t300 = RuntimeEstimator.EstimateRuntime(300, batteryPercent: 100, sohPercent: 100);
    var t600 = RuntimeEstimator.EstimateRuntime(600, batteryPercent: 100, sohPercent: 100);

    if (t100 <= t300 || t300 <= t600)
    {
        throw new InvalidOperationException($"Runtime estimation inverted: 100W={t100.TotalMinutes}m, 300W={t300.TotalMinutes}m, 600W={t600.TotalMinutes}m");
    }

    // 2. Baseline-anchored calculation
    var baselineRuntime = TimeSpan.FromMinutes(60);
    var currentLoad = 150.0;
    var estimatedAt300 = RuntimeEstimator.EstimateRuntime(
        targetLoadWatts: 300,
        batteryPercent: 100,
        baselineRuntimeAtCurrentLoad: baselineRuntime,
        currentActiveLoadWatts: currentLoad);

    // At 2x load, runtime should be less than half (Peukert's law)
    if (estimatedAt300.TotalMinutes >= 30.0 || estimatedAt300.TotalMinutes < 15.0)
    {
        throw new InvalidOperationException($"Baseline-anchored estimate out of expected range: {estimatedAt300.TotalMinutes}m");
    }

    // 3. Generate standard load table
    var table = RuntimeEstimator.GenerateStandardLoadEstimates(
        batteryPercent: 100,
        sohPercent: 90,
        nominalBatteryVoltage: 24,
        ratedActivePowerWatts: 780);

    if (table.Count == 0 || table[0].LoadWatts != 50)
    {
        throw new InvalidOperationException("Standard load table generation failed.");
    }
}

static void ConfigurationNewFeatures()
{
    var config = new AppConfiguration();
    Equal("system", config.Ui.Theme);
    Equal(false, config.Alerts.EnableSoundAlerts);
    Equal(80.0, config.Alerts.HighLoadWarningPercent);
    Equal(92.0, config.Alerts.LowVoltageWarningThreshold);
    Equal(108.0, config.Alerts.HighVoltageWarningThreshold);
    Equal(false, config.Webhook.Enabled);
    Equal(false, config.ExternalCommand.Enabled);

    // Modify and verify
    config.Ui.Theme = "dark";
    Equal("dark", config.Ui.Theme);
    config.Alerts.HighLoadWarningPercent = 85.0;
    Equal(85.0, config.Alerts.HighLoadWarningPercent);
}

static void DailyEnergyAndTroubleSummaryQueries()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsDailyTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);

    SqliteTelemetryStore? store = null;
    try
    {
        var paths = new AppPaths(testRoot, testRoot);
        var config = new HistoryConfiguration { RawRetentionDays = 7, RawUsageCheckpointSeconds = 30 };
        store = new SqliteTelemetryStore(paths, config);
        store.InitializeAsync().GetAwaiter().GetResult();

        var device = new UpsDeviceInfo("test-dev", 0x0764, 0x0601, "Vendor", "Product", "12345", 0x84, 0x04, 64, 64);
        var devId = UpsDeviceIdentity.Create(device);
        var now = DateTimeOffset.Now;

        // Write telemetry sample with power and voltage
        var snap = Snapshot(connected: true, ac: true, battery: 100, load: 30, runtime: TimeSpan.FromMinutes(45));
        snap = snap with { Device = device, Timestamp = now.AddHours(-1), InputVoltage = 90.0, ActivePower = 200.0 };
        store.WriteAsync(snap, CancellationToken.None).GetAwaiter().GetResult();

        // Write power lost event
        var ev = new UpsEvent(now.AddMinutes(-30), UpsEventType.PowerLost, "Power lost", UpsPowerState.Online, UpsPowerState.OnBattery);
        store.WriteAsync(ev, CancellationToken.None).GetAwaiter().GetResult();
        store.FlushAsync().GetAwaiter().GetResult();

        // Query daily reports
        var reports = store.QueryDailyEnergyReportsAsync(devId, 7, 31.0).GetAwaiter().GetResult();
        Equal(7, reports.Count);

        // Query power trouble summary
        var trouble = store.QueryPowerTroubleSummaryAsync(devId, now.AddDays(-1), now, lowVoltageSagThreshold: 95.0, highVoltageSurgeThreshold: 105.0).GetAwaiter().GetResult();
        Equal(1, trouble.TotalOutages);
        Equal(1, trouble.VoltageSagCount);
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

static void EventDetectorZeroAllocationWhenQuiet()
{
    var detector = new UpsEventDetector(TimeSpan.FromMinutes(3));
    var snap1 = Snapshot(ac: true);
    var first = detector.Observe(snap1);
    HasSingle(first, UpsEventType.UpsReconnected);

    // Subsequent identical snapshots must produce zero events and return empty array
    var second = detector.Observe(snap1);
    Equal(0, second.Count);

    var third = detector.Observe(Snapshot(ac: true));
    Equal(0, third.Count);
}

static void CommandRunnerExecutionAndEscaping()
{
    var upsEvent = new UpsEvent(
        DateTimeOffset.UtcNow,
        UpsEventType.PowerLost,
        "AC \"Power\" Lost! \r\nCheck line.",
        UpsPowerState.Online,
        UpsPowerState.OnBattery);
    var snapshot = Snapshot(ac: false, discharging: true, battery: 85, runtime: TimeSpan.FromMinutes(30), activePower: 200);

    // Test successful execution with parameter substitution
    var cmd = "echo EVENT={EVENT} MSG={MESSAGE} BATTERY={BATTERY}";
    var result = CommandRunner.RunCommandAsync(cmd, upsEvent, snapshot).GetAwaiter().GetResult();
    Equal(true, result);

    // Test large output (prevent pipe deadlocks)
    var largeOutputCmd = "for /L %i in (1,1,200) do @echo Line %i of test output that is long enough to fill OS buffers";
    var largeResult = CommandRunner.RunCommandAsync(largeOutputCmd, upsEvent, snapshot).GetAwaiter().GetResult();
    Equal(true, largeResult);

    // Test cancellation / timeout handling
    using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
    var hangCmd = "ping 127.0.0.1 -n 10 > nul";
    var hangResult = CommandRunner.RunCommandAsync(hangCmd, upsEvent, snapshot, cts.Token).GetAwaiter().GetResult();
    Equal(false, hangResult);
}

static void WebhookNotifierValidation()
{
    // Invalid URLs
    Equal(false, WebhookNotifier.SendNotificationAsync("", new UpsEvent(DateTimeOffset.UtcNow, UpsEventType.PowerLost, "test", UpsPowerState.Online, UpsPowerState.OnBattery), Snapshot()).GetAwaiter().GetResult());
    Equal(false, WebhookNotifier.SendNotificationAsync("not_a_valid_url", new UpsEvent(DateTimeOffset.UtcNow, UpsEventType.PowerLost, "test", UpsPowerState.Online, UpsPowerState.OnBattery), Snapshot()).GetAwaiter().GetResult());
    Equal(false, WebhookNotifier.SendTestNotificationAsync("").GetAwaiter().GetResult());
}

static void PollingEngineLifecycleAndInterval()
{
    var mockProvider = new MockUpsProvider();
    var mockSink = new MockUpsEventSink();
    var engine = new UpsMonitorEngine(mockProvider, mockSink, 500, TimeSpan.FromMinutes(2));

    // Poll interval validation
    try
    {
        engine.SetPollInterval(100);
        throw new InvalidOperationException("Should throw on interval < 250");
    }
    catch (ArgumentOutOfRangeException)
    {
    }

    try
    {
        engine.SetPollInterval(70_000);
        throw new InvalidOperationException("Should throw on interval > 60000");
    }
    catch (ArgumentOutOfRangeException)
    {
    }

    engine.SetPollInterval(1000);
    engine.Start();
    Thread.Sleep(100);
    engine.StopAsync().GetAwaiter().GetResult();
    engine.DisposeAsync().AsTask().GetAwaiter().GetResult();
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
    double? activePower = null,
    double? apparentPower = null,
    bool? fullyCharged = null,
    bool? replacement = null,
    string? selfTest = null,
    double? designCapacity = null,
    double? fullChargeCapacity = null,
    IReadOnlyList<UpsTelemetryItem>? telemetry = null,
    UpsDeviceInfo? device = null,
    DateTimeOffset? timestamp = null) => new()
    {
        Device = device,
        Timestamp = timestamp ?? DateTimeOffset.UtcNow,
        IsConnected = connected,
        AcPresent = ac,
        Discharging = discharging,
        LowBattery = low,
        ShutdownImminent = critical,
        Overload = overload,
        RuntimeRemaining = runtime,
        BatteryPercent = battery,
        PercentLoad = load,
        ActivePower = activePower,
        ApparentPower = apparentPower,
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

static void PerformanceBenchmark() => PerformanceBenchmarkAsync().GetAwaiter().GetResult();

static async Task PerformanceBenchmarkAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsPerfTest_{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot, testRoot);
        var store = new SqliteTelemetryStore(paths, new HistoryConfiguration { RawRetentionDays = 30 });
        await store.InitializeAsync();

        var device = new UpsDeviceInfo("test-path", 0x0764, 0x0601, "CPS", "Test UPS", "TEST01", 0x84, 0x04, 64, 64);
        var devId = UpsDeviceIdentity.Create(device);
        var start = DateTimeOffset.UtcNow.AddDays(-30);

        using (var conn = new SqliteConnection($"Data Source={store.DatabasePath}"))
        {
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            cmd.CommandText = """
                INSERT INTO telemetry_samples (device_id, timestamp_utc_ms, is_connected, power_state, input_voltage, output_voltage, battery_voltage, battery_percent, runtime_seconds, load_percent, active_power_watts, apparent_power_va, frequency_hz, temperature_c)
                VALUES ($d, $t, 1, 1, 100.0 + ($i % 10), 100.0, 27.0, 100.0, 1800.0, 30.0 + ($i % 20), 200.0 + ($i % 50), 220.0, 50.0, 25.0);
            """;
            var pDev = cmd.Parameters.Add("$d", SqliteType.Text);
            var pTime = cmd.Parameters.Add("$t", SqliteType.Integer);
            var pI = cmd.Parameters.Add("$i", SqliteType.Integer);

            pDev.Value = devId;
            var baseMs = start.ToUnixTimeMilliseconds();
            for (int i = 0; i < 50000; i++)
            {
                pTime.Value = baseMs + (i * 2000L);
                pI.Value = i;
                await cmd.ExecuteNonQueryAsync();
            }

            cmd.CommandText = """
                INSERT INTO telemetry_rollups_1m (device_id, bucket_utc_ms, metric, minimum, maximum, value_sum, sample_count, last_value, last_timestamp_utc_ms)
                VALUES ($d, $b, $m, 100.0, 110.0, 3150.0, 30, 105.0, $b + 58000);
            """;
            cmd.Parameters.Clear();
            var pbDev = cmd.Parameters.Add("$d", SqliteType.Text);
            var pbB = cmd.Parameters.Add("$b", SqliteType.Integer);
            var pbM = cmd.Parameters.Add("$m", SqliteType.Integer);
            pbDev.Value = devId;

            int[] metrics = [0, 1, 2, 3, 5, 6];
            for (int day = 0; day < 30; day++)
            {
                for (int min = 0; min < 1440; min += 5)
                {
                    var bucket = baseMs + ((day * 1440L + min) * 60_000L);
                    pbB.Value = bucket;
                    foreach (var m in metrics)
                    {
                        pbM.Value = m;
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }

            cmd.CommandText = """
                INSERT INTO ups_events (device_id, timestamp_utc_ms, event_type, message, previous_state, current_state)
                VALUES ($d, $t, $type, 'Event msg', 1, 2);
            """;
            cmd.Parameters.Clear();
            cmd.Parameters.AddWithValue("$d", devId);
            var peTime = cmd.Parameters.Add("$t", SqliteType.Integer);
            var peType = cmd.Parameters.Add("$type", SqliteType.Integer);
            for (int i = 0; i < 500; i++)
            {
                peTime.Value = baseMs + (i * 3600_000L);
                peType.Value = i % 5;
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        using (var conn = new SqliteConnection($"Data Source={store.DatabasePath}"))
        {
            await conn.OpenAsync();

            void Explain(string label, string sql, params (string, object)[] parameters)
            {
                Console.WriteLine($"\n=== {label} ===");
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "EXPLAIN QUERY PLAN " + sql;
                foreach (var (k, v) in parameters) cmd.Parameters.AddWithValue(k, v);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    Console.WriteLine($"  [EXPLAIN] {reader[3]}");
                }
            }

            Explain("1. QueryDailyEnergyReportsAsync (Batch daily active power)",
                """
                SELECT
                    strftime('%Y-%m-%d', datetime(timestamp_utc_ms / 1000, 'unixepoch', 'localtime')) AS day_str,
                    AVG(active_power_watts),
                    MAX(active_power_watts)
                FROM telemetry_samples
                WHERE device_id = $device
                    AND timestamp_utc_ms >= $from
                    AND timestamp_utc_ms <= $to
                    AND active_power_watts IS NOT NULL
                GROUP BY day_str;
                """,
                ("$device", devId), ("$from", start.ToUnixTimeMilliseconds()), ("$to", start.AddDays(30).ToUnixTimeMilliseconds()));

            Explain("2. QueryPowerTroubleSummaryAsync (Single-scan Sag/Surge counts)",
                """
                SELECT
                    COUNT(CASE WHEN input_voltage > 0 AND input_voltage < $sag THEN 1 END),
                    COUNT(CASE WHEN input_voltage > $surge THEN 1 END)
                FROM telemetry_samples
                WHERE device_id = $device
                    AND timestamp_utc_ms >= $from
                    AND timestamp_utc_ms <= $to;
                """,
                ("$device", devId), ("$from", start.ToUnixTimeMilliseconds()), ("$to", start.AddDays(30).ToUnixTimeMilliseconds()), ("$sag", 95.0), ("$surge", 105.0));

            Explain("3. QueryRawMetricsAsync (Batch multi-metric raw query)",
                """
                SELECT
                    (timestamp_utc_ms / $bucket) * $bucket AS bucket,
                    MIN(input_voltage), AVG(input_voltage), MAX(input_voltage), COUNT(input_voltage),
                    MIN(output_voltage), AVG(output_voltage), MAX(output_voltage), COUNT(output_voltage),
                    MIN(battery_voltage), AVG(battery_voltage), MAX(battery_voltage), COUNT(battery_voltage)
                FROM telemetry_samples
                WHERE device_id = $device
                    AND timestamp_utc_ms >= $from
                    AND timestamp_utc_ms <= $to
                GROUP BY bucket
                ORDER BY bucket;
                """,
                ("$device", devId), ("$from", start.ToUnixTimeMilliseconds()), ("$to", start.AddDays(1).ToUnixTimeMilliseconds()), ("$bucket", 60000L));

            Explain("4. QueryRollupMetricsAsync (Batch multi-metric rollup query)",
                """
                SELECT
                    metric,
                    (bucket_utc_ms / $bucket) * $bucket AS bucket,
                    MIN(minimum),
                    SUM(value_sum) / SUM(sample_count),
                    MAX(maximum)
                FROM telemetry_rollups_1m
                WHERE device_id = $device
                    AND metric IN (0, 1, 2, 3, 5, 6)
                    AND bucket_utc_ms >= $from
                    AND bucket_utc_ms <= $to
                GROUP BY metric, (bucket_utc_ms / $bucket)
                ORDER BY metric, bucket;
                """,
                ("$device", devId), ("$from", start.ToUnixTimeMilliseconds()), ("$to", start.AddDays(30).ToUnixTimeMilliseconds()), ("$bucket", 300000L));

            Explain("5. QueryWeeklyPatternAsync (Single metric heatmap)",
                """
                SELECT 
                    CAST(strftime('%w', datetime(bucket_utc_ms / 1000, 'unixepoch', 'localtime')) AS INTEGER) AS dow,
                    CAST(strftime('%H', datetime(bucket_utc_ms / 1000, 'unixepoch', 'localtime')) AS INTEGER) AS hod,
                    MIN(minimum),
                    SUM(value_sum) / SUM(sample_count),
                    MAX(maximum),
                    SUM(sample_count)
                FROM telemetry_rollups_1m
                WHERE device_id = $device
                    AND metric = $metric
                    AND bucket_utc_ms >= $from
                    AND bucket_utc_ms <= $to
                GROUP BY dow, hod;
                """,
                ("$device", devId), ("$from", start.ToUnixTimeMilliseconds()), ("$to", start.AddDays(30).ToUnixTimeMilliseconds()), ("$metric", 0));

            Explain("6. Cleanup query (DELETE telemetry_samples via ix_telemetry_samples_time)",
                """
                DELETE FROM telemetry_samples WHERE timestamp_utc_ms < $cutoff;
                """,
                ("$cutoff", start.AddDays(1).ToUnixTimeMilliseconds()));
        }

        Console.WriteLine("\n=== Benchmarking Current Methods ===");
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var dailyReports = await store.QueryDailyEnergyReportsAsync(devId, 30, 31.0);
        Console.WriteLine($"[BENCH] QueryDailyEnergyReportsAsync(30 days): {sw.ElapsedMilliseconds} ms, count={dailyReports.Count}");

        sw.Restart();
        var trouble = await store.QueryPowerTroubleSummaryAsync(devId, start, start.AddDays(30));
        Console.WriteLine($"[BENCH] QueryPowerTroubleSummaryAsync(30 days): {sw.ElapsedMilliseconds} ms, outages={trouble.TotalOutages}");

        sw.Restart();
        var historyRaw = await store.QueryHistoryAsync(devId, start, start.AddDays(1), [TelemetryMetric.InputVoltage, TelemetryMetric.OutputVoltage, TelemetryMetric.BatteryVoltage, TelemetryMetric.LoadPercent, TelemetryMetric.ActivePowerWatts]);
        Console.WriteLine($"[BENCH] QueryHistoryAsync(raw 1 day, 5 metrics): {sw.ElapsedMilliseconds} ms, points={historyRaw.Metrics[TelemetryMetric.InputVoltage].Points.Count}");

        sw.Restart();
        var historyRollup = await store.QueryHistoryAsync(devId, start, start.AddDays(30), [TelemetryMetric.InputVoltage, TelemetryMetric.OutputVoltage, TelemetryMetric.BatteryVoltage, TelemetryMetric.LoadPercent, TelemetryMetric.ActivePowerWatts]);
        Console.WriteLine($"[BENCH] QueryHistoryAsync(rollup 30 days, 5 metrics): {sw.ElapsedMilliseconds} ms, points={historyRollup.Metrics[TelemetryMetric.InputVoltage].Points.Count}");

        sw.Restart();
        var weekly = await store.QueryWeeklyPatternAsync(devId, start, start.AddDays(30), TelemetryMetric.ActivePowerWatts);
        Console.WriteLine($"[BENCH] QueryWeeklyPatternAsync(30 days): {sw.ElapsedMilliseconds} ms, totalSamples={weekly.TotalSamples}");

        await store.DisposeAsync();
    }
    finally
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(testRoot))
        {
            try { Directory.Delete(testRoot, recursive: true); } catch { }
        }
    }
}

sealed class MockUpsProvider : IUpsProvider
{
    public UpsDeviceInfo? Device { get; private set; } = new UpsDeviceInfo("Path", 0x1234, 0x5678, "Mock", "Model", "SN123", 0x84, 0x04, 64, 64);
    public Task<bool> ConnectAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    public Task<UpsSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult(new UpsSnapshot { IsConnected = true, Timestamp = DateTimeOffset.Now });
    public void Disconnect() => Device = null;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class MockUpsEventSink : IUpsEventSink
{
    public Task WriteAsync(UpsEvent upsEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}
