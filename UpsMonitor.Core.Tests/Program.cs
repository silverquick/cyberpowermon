using System.Globalization;
using Microsoft.Data.Sqlite;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

var tests = new (string Name, Action Run)[]
{
    ("Settings and battery disposal during save ignores completion", SettingsHealthLifecycleTests.DisposalDuringSave),
    ("Settings notification webhook and disposal", SettingsHealthLifecycleTests.NotificationWebhookAndDisposal),
    ("Battery record validation models and editor language", SettingsHealthLifecycleTests.RecordsContextModelsAndValidation),
    ("Battery SQLite context backfill and failed save preserves editor", SettingsHealthLifecycleTests.ContextBackfillAndFailedRecordSave),
    ("Settings battery bindings and native wiring", SettingsHealthLifecycleTests.BindingsAndNativeWiring),
    ("Settings save failure retains edits and success updates engine", SettingsHealthTests.SaveFailureAndSuccess),
    ("Settings immediate effects and language keep selections", SettingsHealthTests.ImmediateChoicesAndLanguage),
    ("Battery official records grouping baseline and purchase date", SettingsHealthTests.OfficialRecordsAndBaseline),
    ("Shared export cancellation busy state and IO failure", SharedExportViewModelTests.CancellationAndFailure),
    ("History and Logs share export workflow", SharedExportViewModelTests.HistoryAndLogsShareExport),
    ("History derived charts reference lines and raw health fallback", HistoryLifecycleTests.ChartProjectionsAndHealthFallback),
    ("History activation language and snapshot intervals", HistoryLifecycleTests.ActivationLanguageAndSnapshots),
    ("History latest wins at every query await", HistoryLifecycleTests.LatestWinsEveryAwait),
    ("History energy report notification counts and costs", HistoryLifecycleTests.ReportNotificationsAndCosts),
    ("History failure cancellation and disposal", HistoryLifecycleTests.FailureCancellationDisposal),
    ("History XAML bindings and Main integration", HistoryLifecycleTests.BindingsAndMainIntegration),
    ("History ranges language and export selection", HistoryViewModelTests.ChoicesLanguageExport),
    ("History SQLite charts summaries reports and downsampled health", HistoryViewModelTests.SqliteChartsSummaryReportsHealth),
    ("Analytics activation throttle and selection requests", AnalyticsViewModelTests.RoutingAndSelectionRequests),
    ("Analytics latest request errors and disposal", AnalyticsViewModelTests.LatestWins),
    ("Analytics XAML bindings resources and Main integration", AnalyticsViewModelTests.BindingsAndMainIntegration),
    ("Analytics selections and language", AnalyticsViewModelTests.SelectionsAndLanguage),
    ("Analytics SQLite display minutes days and headings", AnalyticsViewModelTests.SqliteDisplay),
    ("Official grouping elapsed boundaries and fixed window", OfficialGroupingTests.Boundaries),
    ("Official grouping stable order duplicates and confidence", OfficialGroupingTests.OrderAndDuplicates),
    ("Official grouping past prediction backtest and saved snapshots", OfficialGroupingTests.PastPredictionAndSnapshots),
    ("Official grouping sparse exact regression", OfficialGroupingTests.SparseExactRegression),
    ("Official grouping dense L/P series", OfficialGroupingTests.DenseSeries),
    ("Version display formatting", VersionDisplayFormatting),
    ("Official record empty input uses now", OfficialRecordEmptyInput),
    ("Official record today without time uses now", OfficialRecordTodayOnly),
    ("Official record past date uses midnight", OfficialRecordPastDateOnly),
    ("Official record time only uses today", OfficialRecordTimeOnly),
    ("Official record explicit date and time", OfficialRecordExplicitDateTime),
    ("Official record rejects invalid time formats", OfficialRecordInvalidTime),
    ("Official record future tolerance boundary", OfficialRecordFutureTolerance),
    ("Official record midnight boundaries", OfficialRecordMidnightBoundaries),
    ("Official record uses supplied offset", OfficialRecordSuppliedOffset),
    ("Export ranges flush format and row counts", ExportTests.BoundariesAndFlush),
    ("Export empty database", ExportTests.Empty),
    ("Export EventQuery and Logs filter snapshots", ExportTests.EventConditions),
    ("Export streaming 30000 synthetic rows", ExportTests.Large),
    ("Export cancellation IO and publication failure cleanup", ExportTests.CancellationAndFailure),
    ("Export shutdown waits for cleanup without dispatcher and bounds wait", ExportTests.ShutdownCleanup),
    ("Saved events empty database", SavedEventTests.Empty),
    ("Saved events inclusive From exclusive To", SavedEventTests.Boundaries),
    ("Saved events type severity and combined conditions", SavedEventTests.TypeSeverity),
    ("Saved events display limit and overflow", SavedEventTests.Limit),
    ("Saved events use existing device timestamp index", SavedEventTests.IndexUsage),
    ("Saved events reopen and device isolation", SavedEventTests.ReopenDevices),
    ("Saved events and live identity deduplication", SavedEventTests.Deduplication),
    ("Saved events validation snapshot and cancellation", SavedEventTests.ValidationCancellation),
    ("Logs VM defaults to all time with display limit and hint", SavedEventTests.ViewModelDefaultAllRange),
    ("Logs VM debounce cancellation latest wins", SavedEventTests.ViewModelLatestWins),
    ("Logs VM live text search and language", SavedEventTests.ViewModelLiveSearchLanguage),
    ("Logs VM language switch preserves filter selections and updates labels", SavedEventTests.ViewModelLanguagePreservesFilters),
    ("Settings History and Analytics choices preserve selection while translating", LocalizedOptionTests.LanguageKeepsSelections),
    ("Logs VM query error and disposal", SavedEventTests.ViewModelFailureDisposal),
    ("Logs local timestamp survives saved/live merge and search", SavedEventTests.LocalTimestampDisplaySearch),
    ("Logs alert messages preserve measured values and thresholds", SavedEventTests.AlertMessagesPreserveDetails),
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
    ("Legacy v1 schema migrates new indexes without dropping data", LegacySchemaV1IndexMigration),
    ("Event severity classification", EventSeverityClassification),
    ("Telemetry and event export to CSV/JSON", TelemetryExportRoundTrip),
    ("Dynamic runtime-low threshold update", DynamicRuntimeLowThreshold),
    ("Localized dashboard margins and cell voltage", UiTextFormattingTests.Margins),
    ("Localized estimated cost and rate", UiTextFormattingTests.Costs),
    ("Localized notification title severity and body", UiTextFormattingTests.Notifications),
    ("Localized official context separator preserves spacing", UiTextFormattingTests.OfficialContextSeparator),
    ("Weekly heatmap equivalent-day and localized formats", WeeklyEquivalentDays),
    ("Weekly heatmap pattern aggregation", WeeklyPatternAggregation),
    ("Weekly heatmap power-factor aggregation", WeeklyPatternPowerFactorAggregation),
    ("Runtime estimator load calculation", RuntimeEstimatorCalculation),
    ("Configuration theme, alerts, webhook, and command settings", ConfigurationNewFeatures),
    ("Daily energy reports and trouble summary queries", DailyEnergyAndTroubleSummaryQueries),
    ("Accurate energy reports and monthly aggregation", AccurateEnergyReportsAndMonthlyAggregation),
    ("Performance benchmark and EXPLAIN QUERY PLAN", PerformanceBenchmark),
    ("Event detector zero-allocation when quiet", EventDetectorZeroAllocationWhenQuiet),
    ("Command runner execution, large output, and escaping", CommandRunnerExecutionAndEscaping),
    ("Webhook notifier validation", WebhookNotifierValidation),
    ("Polling engine lifecycle and interval", PollingEngineLifecycleAndInterval),
    ("Navigation tab refresh routing rules", NavigationTabRefreshRoutingRules),
    ("Navigation refresh on tab selection", NavigationRefreshOnTabSelection),
    ("Navigation refresh on visibility change", NavigationRefreshOnVisibilityChange),
    ("Navigation refresh on language change", NavigationRefreshOnLanguageChange),
    ("High load alert detector edge and hysteresis", HighLoadAlertDetectorEdgeAndHysteresis),
    ("Voltage abnormal message matches inclusive warning thresholds", VoltageAbnormalMessageMatchesWarningThresholds),
    ("Voltage abnormal alert detector edge and hysteresis", VoltageAbnormalAlertDetectorEdgeAndHysteresis),
    ("Alert detector disconnected produces no alerts", AlertDetectorDisconnectedNoAlerts),
    ("Dynamic alert thresholds update", DynamicAlertThresholdsUpdate),
    ("Engine alert thresholds propagation and persistence", EngineAlertThresholdsPropagationAndPersistence),
    ("Alert events SQLite round trip", AlertEventsSqliteRoundTrip),
    ("Concurrent reader does not lock telemetry store writer", ConcurrentReaderDoesNotLockTelemetryStoreWriter),
    ("Official health trend interpolates and extrapolates", OfficialTrendInterpolateAndExtrapolate),
    ("Official health trend slope is clipped to decline only", OfficialTrendSlopeClip),
    ("Official health trend uses install date as implicit 100% point", OfficialTrendInstalledAtImplicitPoint),
    ("Official health single record holds value and caps confidence", OfficialTrendSingleRecordAndConfidence),
    ("Official health prediction errors and MAE", OfficialHealthPredictionErrors),
    ("Runtime load normalization replaces interpolation", RuntimeLoadNormalization),
    ("Runtime steady-state filter", RuntimeSteadyStateFilterBehavior),
    ("Calculate prefers official health trend", OfficialTrendIsPreferredInCalculate),
    ("Legacy anchor migration is idempotent", LegacyAnchorMigration),
    ("Official health records configuration round trip", OfficialHealthRecordsConfigurationRoundTrip),
    ("Official install-anchored model fits slope and gates availability", OfficialInstallAnchoredSlope),
    ("Official accelerating model recovers synthetic parameters", OfficialAcceleratingPowerRecovery),
    ("Official models extrapolate continuously from the last record", OfficialModelsExtrapolateFromLastRecord),
    ("Official backtest counts, MAE, and model selection", OfficialBacktestAndSelection),
    ("Official model predictions are stored and rounding is detected", OfficialModelPredictionsAndRounding),
    ("Official record context configuration compatibility", OfficialContextConfigurationCompatibility),
    ("Official record context query and idempotent backfill", OfficialContextQueryAndBackfill),
    ("Official record context capture failure recovers after reload", OfficialContextCaptureFailureRecovery),
    ("Official record live context survives history recovery", OfficialLiveContextCaptureFailureRecovery),
    ("Health history downsampling caps points above limit", HealthHistoryDownsamplingCapsPointsAboveLimit),
    ("Health history preserves exact points at or below limit", HealthHistoryPreservesExactPointsAtOrBelowLimit),
    ("Health history inclusive range boundaries and empty range", HealthHistoryRangeBoundariesAndEmptyRange),
    ("Health history bucket representative values min avg max", HealthHistoryBucketRepresentativeValues),
};

static void VersionDisplayFormatting()
{
    Equal("0.1.016", VersionFormatter.Format(new Version(0, 1, 16, 0)));
    Equal("0.1.001", VersionFormatter.Format(new Version(0, 1, 1)));
    Equal("0.1.999", VersionFormatter.Format(new Version(0, 1, 999)));
}

static void OfficialRecordEmptyInput()
{
    var now = new DateTimeOffset(2026, 11, 13, 10, 20, 37, TimeSpan.FromHours(9)).AddTicks(1234);
    foreach (var text in new string?[] { null, "", " \t " })
        Equal(new OfficialRecordTimeResult(now, OfficialRecordTimeError.None),
            OfficialRecordTimeResolver.Resolve(null, text, now));
}

static void OfficialRecordTodayOnly()
{
    var now = new DateTimeOffset(2026, 11, 13, 10, 20, 37, TimeSpan.FromHours(9)).AddTicks(1234);
    foreach (var text in new string?[] { null, "", " \t " })
        Equal(new OfficialRecordTimeResult(now, OfficialRecordTimeError.None),
            OfficialRecordTimeResolver.Resolve(now.Date, text, now));
}

static void OfficialRecordPastDateOnly()
{
    var now = new DateTimeOffset(2026, 11, 13, 10, 20, 37, TimeSpan.FromHours(9));
    Equal(new OfficialRecordTimeResult(new DateTimeOffset(2026, 11, 12, 0, 0, 0, now.Offset), OfficialRecordTimeError.None),
        OfficialRecordTimeResolver.Resolve(new DateTime(2026, 11, 12, 18, 30, 0), null, now));
}

static void OfficialRecordTimeOnly()
{
    var now = new DateTimeOffset(2026, 11, 13, 10, 20, 37, TimeSpan.FromHours(9));
    foreach (var text in new[] { "9:05", "09:05", " 09:05 " })
        Equal(new OfficialRecordTimeResult(new DateTimeOffset(2026, 11, 13, 9, 5, 0, now.Offset), OfficialRecordTimeError.None),
            OfficialRecordTimeResolver.Resolve(null, text, now));
}

static void OfficialRecordExplicitDateTime()
{
    var now = new DateTimeOffset(2026, 11, 13, 10, 20, 37, TimeSpan.FromHours(9));
    foreach (var day in new[] { now.Date, now.Date.AddDays(-1) })
        Equal(new OfficialRecordTimeResult(new DateTimeOffset(day.AddHours(9).AddMinutes(5), now.Offset), OfficialRecordTimeError.None),
            OfficialRecordTimeResolver.Resolve(day, "9:05", now));
}

static void OfficialRecordInvalidTime()
{
    var now = new DateTimeOffset(2026, 11, 13, 10, 20, 37, TimeSpan.Zero);
    foreach (var text in new[] { "24:00", "12:60", "9:5", "09:05:00", "noon", "2026-11-13 09:05", "-1:00" })
        Equal(new OfficialRecordTimeResult(null, OfficialRecordTimeError.InvalidTimeFormat),
            OfficialRecordTimeResolver.Resolve(null, text, now));
}

static void OfficialRecordFutureTolerance()
{
    var now = new DateTimeOffset(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
    Equal(new OfficialRecordTimeResult(now.AddMinutes(5), OfficialRecordTimeError.None),
        OfficialRecordTimeResolver.Resolve(null, "10:05", now));
    Equal(new OfficialRecordTimeResult(null, OfficialRecordTimeError.Future),
        OfficialRecordTimeResolver.Resolve(null, "10:05", now.AddTicks(-1)));
    Equal(new OfficialRecordTimeResult(null, OfficialRecordTimeError.Future),
        OfficialRecordTimeResolver.Resolve(now.Date, "10:06", now));
    Equal(new OfficialRecordTimeResult(null, OfficialRecordTimeError.Future),
        OfficialRecordTimeResolver.Resolve(now.Date.AddDays(1), null, now));
    Equal(new OfficialRecordTimeResult(null, OfficialRecordTimeError.Future),
        OfficialRecordTimeResolver.Resolve(now.Date.AddDays(1), "10:00", now));
}

static void OfficialRecordMidnightBoundaries()
{
    var now = new DateTimeOffset(2026, 12, 31, 23, 59, 0, TimeSpan.FromHours(9));
    Equal(new OfficialRecordTimeResult(now, OfficialRecordTimeError.None),
        OfficialRecordTimeResolver.Resolve(now.Date, null, now));
    Equal(new OfficialRecordTimeResult(new DateTimeOffset(now.Date, now.Offset), OfficialRecordTimeError.None),
        OfficialRecordTimeResolver.Resolve(null, "0:00", now));
    Equal(new OfficialRecordTimeResult(now.AddMinutes(5), OfficialRecordTimeError.None),
        OfficialRecordTimeResolver.Resolve(now.Date.AddDays(1), "0:04", now));
    Equal(new OfficialRecordTimeResult(null, OfficialRecordTimeError.Future),
        OfficialRecordTimeResolver.Resolve(now.Date.AddDays(1), "0:05", now));
    var afterMidnight = now.AddMinutes(1).AddTicks(1);
    Equal(new OfficialRecordTimeResult(new DateTimeOffset(now.Date, now.Offset), OfficialRecordTimeError.None),
        OfficialRecordTimeResolver.Resolve(now.Date, null, afterMidnight));
    Equal(new OfficialRecordTimeResult(afterMidnight, OfficialRecordTimeError.None),
        OfficialRecordTimeResolver.Resolve(afterMidnight.Date, null, afterMidnight));
}

static void OfficialRecordSuppliedOffset()
{
    foreach (var offset in new[] { TimeSpan.FromHours(-7), TimeSpan.Zero, TimeSpan.FromHours(14) })
    {
        var now = new DateTimeOffset(2026, 11, 13, 0, 2, 37, offset);
        // DatePicker の DateTime.Kind や実行マシンのタイムゾーンに依存しない。
        foreach (var kind in new[] { DateTimeKind.Local, DateTimeKind.Utc, DateTimeKind.Unspecified })
        {
            var result = OfficialRecordTimeResolver.Resolve(DateTime.SpecifyKind(now.Date, kind), "0:01", now);
            Equal(new OfficialRecordTimeResult(new DateTimeOffset(2026, 11, 13, 0, 1, 0, offset), OfficialRecordTimeError.None), result);
            Equal(offset, result.RecordedAt!.Value.Offset);
        }
    }
}

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

static DateTimeOffset OfficialT0() => new(2026, 8, 22, 0, 0, 0, TimeSpan.Zero);

static OfficialHealthRecord OfficialRecord(DateTimeOffset at, double percent) => new()
{
    RecordedAt = at,
    HealthPercent = percent,
    Source = VendorBatteryHealthCategory.Poor,
};

static void OfficialTrendInterpolateAndExtrapolate()
{
    var t1 = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    OfficialHealthRecord[] records = [OfficialRecord(OfficialT0(), 59), OfficialRecord(t1, 54)];

    Near(56.5, OfficialHealthEstimator.Estimate(records, null, OfficialT0() + ((t1 - OfficialT0()) / 2))?.Percent);
    Near(51.887, OfficialHealthEstimator.Estimate(records, null, t1.AddDays(30))?.Percent, 0.01);
    Near(59, OfficialHealthEstimator.Estimate(records, null, OfficialT0().AddDays(-10))?.Percent);
    Equal(true, OfficialHealthEstimator.Estimate([], null, t1) is null);
}

static void OfficialTrendSlopeClip()
{
    var t1 = OfficialT0().AddDays(10);
    var steep = OfficialHealthEstimator.Estimate([OfficialRecord(OfficialT0(), 90), OfficialRecord(t1, 50)], null, t1.AddDays(365));
    Near(10, steep?.Percent, 0.01);
    Near(-40, steep?.SlopePointsPerYear, 0.01);

    var rising = OfficialHealthEstimator.Estimate([OfficialRecord(OfficialT0(), 50), OfficialRecord(t1, 60)], null, t1.AddDays(100));
    Near(60, rising?.Percent);
    Near(0, rising?.SlopePointsPerYear);

    Near(0, OfficialHealthEstimator.Estimate([OfficialRecord(OfficialT0(), 20), OfficialRecord(t1, 10)], null, t1.AddDays(3650))?.Percent);
}

static void OfficialTrendInstalledAtImplicitPoint()
{
    var installed = OfficialT0().AddDays(-365);
    var estimate = OfficialHealthEstimator.Estimate([OfficialRecord(OfficialT0(), 80)], installed, OfficialT0().AddDays(365));
    Near(60, estimate?.Percent, 0.01);
    Near(-20, estimate?.SlopePointsPerYear, 0.01);

    var only = OfficialHealthEstimator.Estimate([], installed, OfficialT0());
    Near(100, only?.Percent);
    Equal(BatteryHealthConfidence.Low, only!.Confidence);
}

static void OfficialTrendSingleRecordAndConfidence()
{
    var one = OfficialHealthEstimator.Estimate([OfficialRecord(OfficialT0(), 70)], null, OfficialT0().AddDays(5));
    Near(70, one?.Percent);
    Equal(BatteryHealthConfidence.Medium, one!.Confidence);

    OfficialHealthRecord[] two = [OfficialRecord(OfficialT0().AddDays(-30), 72), OfficialRecord(OfficialT0(), 70)];
    Equal(BatteryHealthConfidence.High, OfficialHealthEstimator.Estimate(two, null, OfficialT0().AddDays(14))!.Confidence);
    Equal(BatteryHealthConfidence.Medium, OfficialHealthEstimator.Estimate(two, null, OfficialT0().AddDays(14.5))!.Confidence);
    Equal(BatteryHealthConfidence.Medium, OfficialHealthEstimator.Estimate(two, null, OfficialT0().AddDays(60))!.Confidence);
    Equal(BatteryHealthConfidence.Low, OfficialHealthEstimator.Estimate(two, null, OfficialT0().AddDays(61))!.Confidence);
}

static void OfficialHealthPredictionErrors()
{
    var records = new List<OfficialHealthRecord>();
    var first = OfficialHealthEstimator.AddRecord(records, null, OfficialRecord(OfficialT0(), 59));
    Equal(null, first.PredictedPercent);
    var t1 = OfficialT0().AddDays(71);
    var second = OfficialHealthEstimator.AddRecord(records, null, OfficialRecord(t1, 54));
    Near(59, second.PredictedPercent);
    var third = OfficialHealthEstimator.AddRecord(records, null, OfficialRecord(t1.AddDays(30), 50));
    Near(51.887, third.PredictedPercent, 0.01);

    // 過去日の記録は時刻順に挿入され、それより前の記録のみで予測される。
    var middle = OfficialHealthEstimator.AddRecord(records, null, OfficialRecord(OfficialT0().AddDays(35.5), 58));
    Near(59, middle.PredictedPercent);
    Equal(1, records.IndexOf(middle));

    var errors = OfficialHealthEstimator.Errors(records);
    Equal(3, errors.Count);
    Near(-1, errors[0].Error);
    Near(-5, errors[1].Error);
    Near(-1.887, errors[2].Error, 0.01);
    Near((1 + 5 + 1.887) / 3, OfficialHealthEstimator.MeanAbsoluteError(records), 0.01);
    Equal(null, OfficialHealthEstimator.MeanAbsoluteError([OfficialRecord(OfficialT0(), 59)]));

    var withInstall = new List<OfficialHealthRecord>();
    var p = OfficialHealthEstimator.AddRecord(withInstall, OfficialT0().AddDays(-100), OfficialRecord(OfficialT0(), 80));
    Near(100, p.PredictedPercent);
}

static void RuntimeLoadNormalization()
{
    var options = new BatteryHealthOptions();
    var telemetry = UpsTelemetryValidator.Normalize(Snapshot(
        battery: 100, load: 40, fullyCharged: true, runtime: TimeSpan.FromMinutes(12)));
    // 基準1点(20%/52分)のみ: 既定 k=1.9 で 40% 負荷へ正規化。
    var expectedMinutes = 52 * Math.Pow(20.0 / 40.0, 1.9);
    var health = BatteryHealthCalculator.Calculate(telemetry, RuntimeProfile(BatteryRuntimeBaselineKind.CurrentRelative), options);
    Near(12 / expectedMinutes * 100, health.RelativePerformancePercent, 0.01);

    // 2点(負荷差10pt以上)から k を推定: 20%/52分, 40%/13分 -> k=2。
    var profile = RuntimeProfile(BatteryRuntimeBaselineKind.CurrentRelative);
    profile.RuntimeBaselines.Add(new BatteryRuntimeBaselinePoint
    {
        LoadPercent = 40,
        Runtime = TimeSpan.FromMinutes(13),
        MeasuredAt = DateTimeOffset.UtcNow,
    });
    // 2基準点の間(30%)は従来の線形補間(52分と13分の中点 32.5分)が使われ、正規化値ではない。
    var at30 = UpsTelemetryValidator.Normalize(Snapshot(
        battery: 100, load: 30, fullyCharged: true, runtime: TimeSpan.FromMinutes(32.5)));
    Near(100, BatteryHealthCalculator.Calculate(at30, profile, options).RelativePerformancePercent, 0.01);
    var at30Normalized = UpsTelemetryValidator.Normalize(Snapshot(
        battery: 100, load: 30, fullyCharged: true, runtime: TimeSpan.FromMinutes(52 * Math.Pow(20.0 / 30.0, 2))));
    Equal(true, BatteryHealthCalculator.Calculate(at30Normalized, profile, options).RelativePerformancePercent < 80);
    // 許容差外の低負荷(10%)でも N/A にならない。
    var at10 = UpsTelemetryValidator.Normalize(Snapshot(
        battery: 100, load: 10, fullyCharged: true, runtime: TimeSpan.FromMinutes(52 * 4)));
    Near(100, BatteryHealthCalculator.Calculate(at10, profile, options).RelativePerformancePercent, 0.01);

    var invalid = false;
    try
    {
        BatteryHealthCalculator.Calculate(telemetry, null, new BatteryHealthOptions { RuntimeLoadExponent = 5 });
    }
    catch (ArgumentOutOfRangeException)
    {
        invalid = true;
    }

    Equal(true, invalid);
}

static void RuntimeSteadyStateFilterBehavior()
{
    var start = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    UpsTelemetry Sample(int seconds, double? load, double runtimeMinutes) => UpsTelemetryValidator.Normalize(Snapshot(
        load: load, runtime: TimeSpan.FromMinutes(runtimeMinutes), timestamp: start.AddSeconds(seconds)));

    var filter = new RuntimeSteadyStateFilter();
    // 履歴が60秒に満たない間は定常値が無く Missing。
    Equal(false, filter.Apply(Sample(0, 20, 50)).RuntimeRemaining.IsValid);
    Equal(false, filter.Apply(Sample(30, 21, 51)).RuntimeRemaining.IsValid);
    // 60秒以上 3pt 以内で定常: そのまま通す。
    var steady = filter.Apply(Sample(60, 22, 52));
    Near(52, steady.RuntimeRemaining.Value?.TotalMinutes);
    // 負荷が跳ねた過渡値は直前の定常値に置換される。
    var transient = filter.Apply(Sample(70, 40, 20));
    Near(52, transient.RuntimeRemaining.Value?.TotalMinutes);
    Near(22, transient.LoadPercent.Value);
    // 負荷が無効なら履歴クリア。再び定常になるまで直前の定常値を保持する。
    var cleared = filter.Apply(Sample(80, null, 10));
    Near(52, cleared.RuntimeRemaining.Value?.TotalMinutes);
    Near(22, filter.Apply(Sample(90, 40, 20)).LoadPercent.Value);
    // 40% で60秒以上安定すれば新しい定常値になる。
    var settled = filter.Apply(Sample(150, 41, 21));
    Near(21, settled.RuntimeRemaining.Value?.TotalMinutes);
    Near(41, settled.LoadPercent.Value);
}

static void OfficialTrendIsPreferredInCalculate()
{
    var now = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
    var profile = RuntimeProfile(BatteryRuntimeBaselineKind.CurrentRelative);
    profile.OfficialHealthRecords.Add(OfficialRecord(new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero), 59));
    profile.OfficialHealthRecords.Add(OfficialRecord(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero), 54));

    var slow = UpsTelemetryValidator.Normalize(Snapshot(
        battery: 100, load: 20, fullyCharged: true, runtime: TimeSpan.FromMinutes(36.4), timestamp: now));
    var result = BatteryHealthCalculator.Calculate(slow, profile);
    Equal(BatteryHealthMethod.OfficialHealthTrend, result.PrimaryMethod);
    Near(51.887, result.HealthPercent, 0.01);
    Near(70, result.RelativePerformancePercent);
    Equal(BatteryHealthStatus.VendorReported, result.Status);
    Equal(BatteryHealthConfidence.Medium, result.Confidence);
    Equal(true, result.OfficialEstimate is not null);
    Equal(true, result.Reasons.Any(item => item.Code == BatteryHealthReasonCode.OfficialHealthTrendEstimated));
    Equal(true, result.Reasons.Any(item => item.Code == BatteryHealthReasonCode.RuntimeDeclineDetectedRerecord));
    Equal(BatteryReplacementStatus.ConsiderReplacement, result.Replacement.Status);
    Equal(BatteryReplacementReasonCode.OfficialHealthTrendBelowReference, result.Replacement.Reasons[0].Code);

    // ベースラインが無くても成立し、相対性能が計算できなければ再記録理由は付かない。
    var bare = new BatteryHealthProfile { DeviceId = "test", BatteryInstalledAt = now.AddYears(-1) };
    bare.OfficialHealthRecords.Add(OfficialRecord(now.AddDays(-40), 86));
    bare.OfficialHealthRecords.Add(OfficialRecord(now.AddDays(-3), 85));
    var good = BatteryHealthCalculator.Calculate(slow, bare);
    Equal(BatteryHealthMethod.OfficialHealthTrend, good.PrimaryMethod);
    Equal(BatteryHealthConfidence.High, good.Confidence);
    Equal(null, good.RelativePerformancePercent);
    Equal(false, good.Reasons.Any(item => item.Code == BatteryHealthReasonCode.RuntimeDeclineDetectedRerecord));

    // 物理容量比は公式値推移より優先される。
    var capacity = UpsTelemetryValidator.Normalize(Snapshot(
        designCapacity: 100, fullChargeCapacity: 90, timestamp: now,
        telemetry: [CapacityItem(0x83, 100, "Wh", 0x00F0D121, 1000), CapacityItem(0x67, 90, "Wh", 0x00F0D121, 1000)]));
    Equal(BatteryHealthMethod.CapacityRatio, BatteryHealthCalculator.Calculate(capacity, profile).PrimaryMethod);

    // 設置日のみ(記録0件)では公式値推移は採用されず、従来のランタイム比になる。
    var installOnly = RuntimeProfile(BatteryRuntimeBaselineKind.CurrentRelative);
    installOnly.BatteryInstalledAt = now.AddYears(-1);
    Equal(BatteryHealthMethod.RelativeRuntimeTrend, BatteryHealthCalculator.Calculate(slow, installOnly).PrimaryMethod);

    // 設置日が最古の記録より後なら暗黙点は無視され、上昇する補間にならない。
    var lateInstall = new[] { OfficialRecord(now.AddDays(-40), 80) };
    Near(80, OfficialHealthEstimator.Estimate(lateInstall, now.AddDays(-10), now.AddDays(-20))?.Percent, 0.001);
    Equal(true, OfficialHealthEstimator.Estimate(lateInstall, now.AddDays(-10), now)!.Percent <= 80);

    // 記録も設置日も無ければ従来どおりランタイム比(新規分岐に入らない)。
    Equal(BatteryHealthMethod.RelativeRuntimeTrend,
        BatteryHealthCalculator.Calculate(slow, RuntimeProfile(BatteryRuntimeBaselineKind.CurrentRelative)).PrimaryMethod);
}

static void LegacyAnchorMigration()
{
    var recordedAt = new DateTimeOffset(2026, 8, 22, 9, 30, 15, 500, TimeSpan.Zero);
    var profile = new BatteryHealthProfile
    {
        DeviceId = "test",
        RuntimeBaselineKind = BatteryRuntimeBaselineKind.KnownHealthAnchor,
        AnchorHealthPercent = 59,
        VendorHealthCategory = VendorBatteryHealthCategory.Poor,
        BaselineRecordedAt = recordedAt,
    };

    Equal(true, OfficialHealthEstimator.TryMigrateLegacyAnchor(profile));
    Equal(false, OfficialHealthEstimator.TryMigrateLegacyAnchor(profile));
    Equal(1, profile.OfficialHealthRecords.Count);
    Near(59, profile.OfficialHealthRecords[0].HealthPercent);
    Equal(VendorBatteryHealthCategory.Poor, profile.OfficialHealthRecords[0].Source);
    Equal(null, profile.OfficialHealthRecords[0].PredictedPercent);
    Near(59, profile.AnchorHealthPercent);
    Equal(BatteryRuntimeBaselineKind.KnownHealthAnchor, profile.RuntimeBaselineKind);

    var other = new BatteryHealthProfile { DeviceId = "other", AnchorHealthPercent = 59, BaselineRecordedAt = recordedAt };
    Equal(false, OfficialHealthEstimator.TryMigrateLegacyAnchor(other));
}

static void OfficialHealthRecordsConfigurationRoundTrip() =>
    OfficialHealthRecordsConfigurationRoundTripAsync().GetAwaiter().GetResult();

static async Task OfficialHealthRecordsConfigurationRoundTripAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsOfficialHealth_{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        var store = new JsonConfigurationStore(paths);
        var configuration = new AppConfiguration();
        var profile = new BatteryHealthProfile
        {
            DeviceId = "test",
            BatteryInstalledAt = new DateTimeOffset(2024, 3, 1, 0, 0, 0, TimeSpan.FromHours(9)),
        };
        OfficialHealthEstimator.AddRecord(profile.OfficialHealthRecords, profile.BatteryInstalledAt, OfficialRecord(OfficialT0(), 59));
        profile.OfficialHealthRecords[0].Note = "memo";
        configuration.BatteryHealth.Profiles.Add(profile);
        await store.SaveAsync(configuration);

        var json = await File.ReadAllTextAsync(paths.ConfigurationFile);
        Equal(true, json.Contains("\"poor\""));

        var loaded = (await store.LoadAsync()).BatteryHealth.Profiles.Single();
        Equal(profile.BatteryInstalledAt, loaded.BatteryInstalledAt);
        Equal(1, loaded.OfficialHealthRecords.Count);
        Equal(OfficialT0(), loaded.OfficialHealthRecords[0].RecordedAt);
        Near(59, loaded.OfficialHealthRecords[0].HealthPercent);
        Equal(VendorBatteryHealthCategory.Poor, loaded.OfficialHealthRecords[0].Source);
        Near(100, loaded.OfficialHealthRecords[0].PredictedPercent);
        Equal("memo", loaded.OfficialHealthRecords[0].Note);
    }
    finally
    {
        Directory.Delete(testRoot, recursive: true);
    }
}

static OfficialHealthModelResult ModelResult(OfficialHealthEstimate estimate, OfficialHealthModel model) =>
    estimate.Models.Single(item => item.Model == model);

static DateTimeOffset OfficialInstalled() => new(2024, 7, 15, 0, 0, 0, TimeSpan.Zero);

static void OfficialInstallAnchoredSlope()
{
    OfficialHealthRecord[] records =
    [
        OfficialRecord(new DateTimeOffset(2026, 8, 22, 8, 18, 0, TimeSpan.Zero), 59),
        OfficialRecord(new DateTimeOffset(2026, 11, 1, 6, 38, 0, TimeSpan.Zero), 54),
    ];
    var at = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);
    var estimate = OfficialHealthEstimator.Estimate(records, OfficialInstalled(), at)!;
    // 件数が足りない間は購入日起点の直線を採用する。傾きは約 19.8pt/年。
    Equal(OfficialHealthModel.InstallAnchoredLinear, estimate.Model);
    Near(-19.79, estimate.SlopePointsPerYear, 0.1);
    Near(54 - (19.79 * (at - records[1].RecordedAt).TotalDays / 365), estimate.Percent, 0.05);

    // 購入日が無い、または最古の記録より後なら M2/M3 は利用不可で M1 を使う。
    var noInstall = OfficialHealthEstimator.Estimate(records, null, at)!;
    Equal(OfficialHealthModel.RecentLinear, noInstall.Model);
    Equal(false, ModelResult(noInstall, OfficialHealthModel.InstallAnchoredLinear).IsAvailable);
    Equal(false, ModelResult(noInstall, OfficialHealthModel.AcceleratingPower).IsAvailable);
    var late = OfficialHealthEstimator.Estimate(records, records[0].RecordedAt.AddDays(1), at)!;
    Equal(false, ModelResult(late, OfficialHealthModel.InstallAnchoredLinear).IsAvailable);
    Equal(OfficialHealthModel.RecentLinear, late.Model);
}

static void OfficialAcceleratingPowerRecovery()
{
    var installed = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    OfficialHealthRecord Make(double years) =>
        OfficialRecord(installed.AddDays(years * 365), 100 - (12 * Math.Pow(years, 1.5)));
    OfficialHealthRecord[] records = [Make(0.5), Make(1), Make(1.5), Make(2)];

    // 合成データ(p=1.5, a=12)を復元できていれば、外挿は元の曲線に一致する。
    var at = installed.AddDays(2.5 * 365);
    var estimate = OfficialHealthEstimator.Estimate(records, installed, at)!;
    var power = ModelResult(estimate, OfficialHealthModel.AcceleratingPower);
    Near(100 - (12 * Math.Pow(2.5, 1.5)), power.Percent, 0.05);
    var fit = OfficialHealthEstimator.FitPower(records, installed)!.Value;
    Near(1.5, fit.P, 0.011);
    Near(12, fit.A, 0.3);

    // 記録1件では M3 は利用不可。
    var single = OfficialHealthEstimator.Estimate([records[0]], installed, at)!;
    Equal(false, ModelResult(single, OfficialHealthModel.AcceleratingPower).IsAvailable);
    Equal(true, ModelResult(single, OfficialHealthModel.InstallAnchoredLinear).IsAvailable);
}

static void OfficialModelsExtrapolateFromLastRecord()
{
    var installed = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    OfficialHealthRecord[] records =
    [
        OfficialRecord(installed.AddDays(400), 80),
        OfficialRecord(installed.AddDays(600), 70),
    ];
    var last = records[1].RecordedAt;

    // どのモデルも最後の記録の直後は記録値から連続し、範囲内は共通の線形補間。
    var justAfter = OfficialHealthEstimator.Estimate(records, installed, last.AddSeconds(1))!;
    foreach (var result in justAfter.Models)
    {
        Near(70, result.Percent, 0.001);
    }

    var middle = OfficialHealthEstimator.Estimate(records, installed, installed.AddDays(500))!;
    foreach (var result in middle.Models)
    {
        Near(75, result.Percent, 0.001);
    }

    // クリップ: 低下は年40pt以内、0 未満にならない、上昇はしない。
    var year = OfficialHealthEstimator.Estimate(records, installed, last.AddDays(365))!;
    foreach (var result in year.Models)
    {
        Equal(true, result.Percent >= 30 - 0.001 && result.Percent <= 70 + 0.001);
    }

    var far = OfficialHealthEstimator.Estimate(records, installed, last.AddDays(36500))!;
    foreach (var result in far.Models)
    {
        Equal(true, result.Percent >= 0 && result.Percent <= 70);
    }

    OfficialHealthRecord[] rising = [OfficialRecord(installed.AddDays(400), 60), OfficialRecord(installed.AddDays(600), 70)];
    foreach (var result in OfficialHealthEstimator.Estimate(rising, installed, installed.AddDays(900))!.Models)
    {
        Equal(true, result.Percent <= 70 + 0.001);
    }

    Near(justAfter.MinPercent, justAfter.MaxPercent);
    Equal(true, year.SpreadPercent >= 0);
}

static void OfficialBacktestAndSelection()
{
    var installed = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    // h = 100 - 5 t^2: 加速曲線そのもの。
    OfficialHealthRecord Make(double years) =>
        OfficialRecord(installed.AddDays(years * 365), 100 - (5 * years * years));
    OfficialHealthRecord[] five = [Make(1), Make(1.5), Make(2), Make(2.5), Make(3)];
    var at = installed.AddDays(3.5 * 365);

    var all = OfficialHealthEstimator.Estimate(five, installed, at)!;
    Equal(4, ModelResult(all, OfficialHealthModel.RecentLinear).BacktestCount);
    Equal(4, ModelResult(all, OfficialHealthModel.InstallAnchoredLinear).BacktestCount);
    Equal(3, ModelResult(all, OfficialHealthModel.AcceleratingPower).BacktestCount);
    Near(0, ModelResult(all, OfficialHealthModel.AcceleratingPower).MeanAbsoluteError, 0.02);
    Equal(true, ModelResult(all, OfficialHealthModel.InstallAnchoredLinear).MeanAbsoluteError > 0.5);
    // 全モデルのバックテストが3件以上 → MAE 最小(加速曲線)。
    Equal(OfficialHealthModel.AcceleratingPower, all.Model);

    // 3件未満が1つでもあれば M2 を採用する。
    var three = OfficialHealthEstimator.Estimate(five[..3], installed, at)!;
    Equal(1, ModelResult(three, OfficialHealthModel.AcceleratingPower).BacktestCount);
    Equal(OfficialHealthModel.InstallAnchoredLinear, three.Model);

    // 購入日なしは M1 のみ。
    Equal(OfficialHealthModel.RecentLinear, OfficialHealthEstimator.Estimate(five, null, at)!.Model);

    // MAE が同値(直線データ: 全モデル誤差0)なら M2 > M1 > M3 の順で優先。
    OfficialHealthRecord Linear(double years) =>
        OfficialRecord(installed.AddDays(years * 365), 100 - (10 * years));
    OfficialHealthRecord[] line = [Linear(0.5), Linear(1), Linear(1.5), Linear(2), Linear(2.5)];
    Equal(OfficialHealthModel.InstallAnchoredLinear, OfficialHealthEstimator.Estimate(line, installed, at)!.Model);

    var backtest = OfficialHealthEstimator.BacktestModels(five, installed);
    Equal(4, backtest.Single(item => item.Model == OfficialHealthModel.RecentLinear).BacktestCount);
    Equal(0, OfficialHealthEstimator.BacktestModels([five[0]], installed).Max(item => item.BacktestCount));
}

static void OfficialModelPredictionsAndRounding()
{
    var installed = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    var records = new List<OfficialHealthRecord>();
    var first = OfficialHealthEstimator.AddRecord(records, installed, OfficialRecord(installed.AddDays(400), 80));
    Equal(true, first.ModelPredictions is { Count: 1 } && first.ModelPredictions.ContainsKey(OfficialHealthModel.RecentLinear));

    var second = OfficialHealthEstimator.AddRecord(records, installed, OfficialRecord(installed.AddDays(600), 70));
    Equal(2, second.ModelPredictions!.Count);
    Near(second.ModelPredictions[OfficialHealthModel.InstallAnchoredLinear], second.PredictedPercent);

    var third = OfficialHealthEstimator.AddRecord(records, installed, OfficialRecord(installed.AddDays(800), 60));
    Equal(3, third.ModelPredictions!.Count);
    Equal(null, OfficialHealthEstimator.AddRecord(new List<OfficialHealthRecord>(), null, OfficialRecord(installed, 90)).ModelPredictions);

    Equal(true, OfficialHealthEstimator.IsWithinRounding(0.5));
    Equal(true, OfficialHealthEstimator.IsWithinRounding(-0.5));
    Equal(false, OfficialHealthEstimator.IsWithinRounding(0.51));
    Equal(false, OfficialHealthEstimator.IsWithinRounding(-1.2));
}

static void OfficialContextConfigurationCompatibility() =>
    OfficialContextConfigurationCompatibilityAsync().GetAwaiter().GetResult();

static async Task OfficialContextConfigurationCompatibilityAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsOfficialContext_{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        var store = new JsonConfigurationStore(paths);
        Directory.CreateDirectory(paths.SharedDirectory);

        // 旧形式(Context/ModelPredictions/ContextBackfillAttempted なし)がそのまま読める。
        await File.WriteAllTextAsync(paths.ConfigurationFile, """
            {"batteryHealth":{"profiles":[{"deviceId":"x","officialHealthRecords":[
              {"recordedAt":"2026-08-22T17:18:00+09:00","healthPercent":59,"source":"poor","predictedPercent":100,"note":"legacy anchor"}
            ]}]}}
            """);
        var old = (await store.LoadAsync()).BatteryHealth.Profiles.Single().OfficialHealthRecords.Single();
        Near(59, old.HealthPercent);
        Equal(null, old.Context);
        Equal(null, old.ModelPredictions);
        Equal(false, old.ContextBackfillAttempted);

        // 新形式の保存・読込。
        var configuration = await store.LoadAsync();
        var record = configuration.BatteryHealth.Profiles.Single().OfficialHealthRecords.Single();
        record.Context = new OfficialHealthContext
        {
            LoadPercent = 34,
            ActivePowerWatts = 315,
            RuntimeSeconds = 1092,
            BatteryVoltage = 27.1,
            Charging = true,
            DaysSinceLastDischarge = null,
            HistoryStartedAt = new DateTimeOffset(2026, 8, 22, 9, 20, 0, TimeSpan.Zero),
        };
        record.ModelPredictions = new() { [OfficialHealthModel.RecentLinear] = 58.5, [OfficialHealthModel.AcceleratingPower] = 57.2 };
        record.ContextBackfillAttempted = true;
        await store.SaveAsync(configuration);

        var loaded = (await store.LoadAsync()).BatteryHealth.Profiles.Single().OfficialHealthRecords.Single();
        Near(34, loaded.Context!.LoadPercent);
        Near(315, loaded.Context.ActivePowerWatts);
        Near(27.1, loaded.Context.BatteryVoltage);
        Equal(true, loaded.Context.Charging);
        Equal(null, loaded.Context.InputVoltage);
        Equal(null, loaded.Context.DaysSinceLastDischarge);
        Equal(record.Context.HistoryStartedAt, loaded.Context.HistoryStartedAt);
        Near(57.2, loaded.ModelPredictions![OfficialHealthModel.AcceleratingPower]);
        Equal(true, loaded.ContextBackfillAttempted);
    }
    finally
    {
        Directory.Delete(testRoot, recursive: true);
    }
}

static void OfficialContextQueryAndBackfill() => OfficialContextQueryAndBackfillAsync().GetAwaiter().GetResult();

static async Task OfficialContextQueryAndBackfillAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsOfficialContextDb_{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        await using var store = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        await store.InitializeAsync();
        var at = new DateTimeOffset(2026, 11, 1, 6, 38, 0, TimeSpan.Zero);
        long Ms(DateTimeOffset value) => value.ToUnixTimeMilliseconds();

        await using (var connection = new SqliteConnection($"Data Source={paths.TelemetryDatabaseFile};Pooling=False"))
        {
            await connection.OpenAsync();
            async Task Exec(string sql, params (string Name, object Value)[] parameters)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = sql;
                foreach (var (name, value) in parameters)
                {
                    command.Parameters.AddWithValue(name, value);
                }

                await command.ExecuteNonQueryAsync();
            }

            const string sampleSql = """
                INSERT INTO telemetry_samples (device_id, timestamp_utc_ms, is_connected, power_state,
                    input_voltage, battery_voltage, runtime_seconds, load_percent, active_power_watts, temperature_c, charging)
                VALUES ($d, $t, 1, 1, $iv, 27.1, 1092, $load, 315, 25, 1);
                """;
            await Exec(sampleSql, ("$d", "dev"), ("$t", Ms(at.AddSeconds(-30))), ("$iv", 100.0), ("$load", 34.0));
            // 窓(±5分)の外にあるサンプルは無視される。
            await Exec(sampleSql, ("$d", "dev"), ("$t", Ms(at.AddMinutes(10))), ("$iv", 90.0), ("$load", 90.0));

            const string rollupSql = """
                INSERT INTO telemetry_rollups_1m (device_id, bucket_utc_ms, metric, minimum, maximum, value_sum, sample_count, last_value, last_timestamp_utc_ms)
                VALUES ($d, $b, $m, $min, $max, $sum, 60, $last, $b);
                """;
            for (var minute = 0; minute <= 1500; minute++)
            {
                var bucket = Ms(at.AddMinutes(-minute));
                var load = minute <= 10 ? 40.0 : 20.0;
                await Exec(rollupSql, ("$d", "dev"), ("$b", bucket), ("$m", (int)TelemetryMetric.LoadPercent),
                    ("$min", load), ("$max", load + 5), ("$sum", load * 60), ("$last", load));
                await Exec(rollupSql, ("$d", "dev"), ("$b", bucket), ("$m", (int)TelemetryMetric.BatteryVoltage),
                    ("$min", 27.0), ("$max", 27.0), ("$sum", 27.0 * 60), ("$last", 27.0));
            }

            // 別デバイスは混ざらない。
            await Exec(rollupSql, ("$d", "other"), ("$b", Ms(at)), ("$m", (int)TelemetryMetric.LoadPercent),
                ("$min", 99.0), ("$max", 99.0), ("$sum", 99.0 * 60), ("$last", 99.0));

            const string stateSql = "INSERT INTO ups_state_changes (device_id, timestamp_utc_ms, power_state) VALUES ($d, $t, $s);";
            await Exec(stateSql, ("$d", "dev"), ("$t", Ms(at.AddDays(-5))), ("$s", (int)UpsPowerState.Online));
            await Exec(stateSql, ("$d", "dev"), ("$t", Ms(at.AddDays(-3))), ("$s", (int)UpsPowerState.OnBattery));
            await Exec(stateSql, ("$d", "dev"), ("$t", Ms(at.AddDays(-3).AddMinutes(10))), ("$s", (int)UpsPowerState.Online));
        }

        var context = await store.QueryOfficialHealthContextAsync("dev", at);
        Near(34, context.LoadPercent);
        Near(315, context.ActivePowerWatts);
        Near(1092, context.RuntimeSeconds);
        Near(27.1, context.BatteryVoltage);
        Near(100, context.InputVoltage);
        Near(25, context.TemperatureCelsius);
        Equal(true, context.Charging);
        Near(40, context.Load10MinuteMean);
        Near(45, context.Load10MinuteMax);
        Near(((11 * 40.0) + (1430 * 20.0)) / 1441, context.Load24HourMean, 0.001);
        Near(45, context.Load24HourMax);
        Near(27.0, context.BatteryVoltage24HourMean);
        Near(3 - (10.0 / 1440), context.DaysSinceLastDischarge, 0.001);
        Equal(at.AddDays(-5), context.HistoryStartedAt);

        // raw サンプルが無い時刻は1分ロールアップから瞬間値を取る(ロールアップに無い項目は null)。
        var fallback = await store.QueryOfficialHealthContextAsync("dev", at.AddHours(-2));
        Near(20, fallback.LoadPercent);
        Equal(null, fallback.ActivePowerWatts);
        Equal(null, fallback.RuntimeSeconds);
        Near(27.0, fallback.BatteryVoltage);
        Equal(null, fallback.Charging);

        // 履歴の開始より前の時刻は全項目 null(開始日時だけが分かる)。放電履歴の無いデバイスは日数 null。
        var before = await store.QueryOfficialHealthContextAsync("dev", at.AddDays(-30));
        Equal(false, before.HasAnyValue);
        Equal(at.AddDays(-5), before.HistoryStartedAt);
        var noDischarge = await store.QueryOfficialHealthContextAsync("other", at);
        Equal(null, noDischarge.DaysSinceLastDischarge);
        Near(99, noDischarge.Load10MinuteMean);
        Equal((await store.QueryOfficialHealthContextAsync("nothing", at)).HistoryStartedAt, null);

        // 後付け: 履歴取得が未完了の記録に補完し、完了済みは再試行しない(冪等)。
        var existing = OfficialRecord(at.AddDays(-1), 50);
        existing.Context = new OfficialHealthContext { LoadPercent = 11 };
        existing.ContextBackfillAttempted = true;
        var records = new List<OfficialHealthRecord>
        {
            OfficialRecord(at.AddDays(-40), 60),
            OfficialRecord(at, 54),
            existing,
        };
        Equal(2, await OfficialHealthContextService.BackfillAsync(records, "dev", store));
        Equal(null, records[0].Context);
        Equal(true, records[0].ContextBackfillAttempted);
        Near(34, records[1].Context!.LoadPercent);
        Equal(true, records[1].ContextBackfillAttempted);
        Near(11, existing.Context.LoadPercent);
        Equal(true, existing.ContextBackfillAttempted);

        var snapshotJson = System.Text.Json.JsonSerializer.Serialize(records);
        Equal(0, await OfficialHealthContextService.BackfillAsync(records, "dev", store));
        Equal(snapshotJson, System.Text.Json.JsonSerializer.Serialize(records));

        // 記録時: 現在に近い記録は瞬間値を現在値で上書きし、遠い記録は履歴の値のまま。
        var live = new OfficialHealthContext { LoadPercent = 50, ActivePowerWatts = 400 };
        var near = await OfficialHealthContextService.CaptureAsync(store, "dev", at, at.AddMinutes(1), live);
        Near(50, near!.LoadPercent);
        Near(400, near.ActivePowerWatts);
        Near(40, near.Load10MinuteMean);
        var distant = await OfficialHealthContextService.CaptureAsync(store, "dev", at, at.AddDays(2), live);
        Near(34, distant!.LoadPercent);
        Equal(null, await OfficialHealthContextService.CaptureAsync(null, "dev", at, at, null));
    }
    finally
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(testRoot, recursive: true);
    }
}

static void OfficialContextCaptureFailureRecovery() => OfficialContextCaptureFailureRecoveryAsync(withLive: false).GetAwaiter().GetResult();

static void OfficialLiveContextCaptureFailureRecovery() => OfficialContextCaptureFailureRecoveryAsync(withLive: true).GetAwaiter().GetResult();

static async Task OfficialContextCaptureFailureRecoveryAsync(bool withLive)
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsOfficialContextRecovery_{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        await using var history = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        await history.InitializeAsync();
        var at = OfficialT0();
        await using (var connection = new SqliteConnection($"Data Source={paths.TelemetryDatabaseFile};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO telemetry_samples (device_id, timestamp_utc_ms, is_connected, power_state,
                    load_percent, active_power_watts, runtime_seconds, battery_voltage, input_voltage, temperature_c, charging)
                VALUES ('dev', $at, 1, 1, 34, 315, 1092, 26.7, 100, 25, 1);
                INSERT INTO telemetry_rollups_1m (device_id, bucket_utc_ms, metric, minimum, maximum,
                    value_sum, sample_count, last_value, last_timestamp_utc_ms)
                VALUES ('dev', $at, $load, 35, 45, 80, 2, 40, $at),
                    ('dev', $at, $voltage, 27, 27, 54, 2, 27, $at);
                INSERT INTO ups_state_changes (device_id, timestamp_utc_ms, power_state)
                VALUES ('dev', $discharge, $onBattery), ('dev', $restored, $online);
                """;
            command.Parameters.AddWithValue("$at", at.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("$load", (int)TelemetryMetric.LoadPercent);
            command.Parameters.AddWithValue("$voltage", (int)TelemetryMetric.BatteryVoltage);
            command.Parameters.AddWithValue("$discharge", at.AddDays(-3).ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("$restored", at.AddDays(-2).ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("$onBattery", (int)UpsPowerState.OnBattery);
            command.Parameters.AddWithValue("$online", (int)UpsPowerState.Online);
            await command.ExecuteNonQueryAsync();
        }

        // live の有無に関係なく履歴取得失敗は未完了とし、保存・再読込する。
        var source = new FailingOfficialContextSource(history) { Fail = true };
        var record = OfficialRecord(at, 59);
        var live = withLive ? new OfficialHealthContext
        {
            LoadPercent = 50, ActivePowerWatts = 400, RuntimeSeconds = 1500,
            BatteryVoltage = 28, InputVoltage = 110, TemperatureCelsius = 30, Charging = false,
        } : null;
        await OfficialHealthContextService.CaptureForRecordAsync(record, source, "dev", at, live);
        Equal(live, record.Context);
        Equal(false, record.ContextBackfillAttempted);
        var configuration = new AppConfiguration();
        var profile = new BatteryHealthProfile { DeviceId = "dev" };
        OfficialHealthEstimator.AddRecord(profile.OfficialHealthRecords, null, record);
        configuration.BatteryHealth.Profiles.Add(profile);
        var configStore = new JsonConfigurationStore(paths);
        await configStore.SaveAsync(configuration);
        configuration = await configStore.LoadAsync();
        record = configuration.BatteryHealth.Profiles.Single().OfficialHealthRecords.Single();
        Equal(live, record.Context);
        Equal(false, record.ContextBackfillAttempted);

        // 起動時補完も失敗したら未試行を維持し、復旧後の補完で履歴が入る。
        Equal(0, await OfficialHealthContextService.BackfillAsync([record], "dev", source));
        Equal(false, record.ContextBackfillAttempted);
        Equal(live, record.Context);
        source.Fail = false;
        Equal(1, await OfficialHealthContextService.BackfillAsync([record], "dev", source));
        void AssertRecovered(OfficialHealthContext context)
        {
            Near(withLive ? 50 : 34, context.LoadPercent);
            Near(withLive ? 400 : 315, context.ActivePowerWatts);
            Near(withLive ? 1500 : 1092, context.RuntimeSeconds);
            Near(withLive ? 28 : 26.7, context.BatteryVoltage);
            Near(withLive ? 110 : 100, context.InputVoltage);
            Near(withLive ? 30 : 25, context.TemperatureCelsius);
            Equal(!withLive, context.Charging);
            Near(40, context.Load10MinuteMean);
            Near(45, context.Load10MinuteMax);
            Near(40, context.Load24HourMean);
            Near(45, context.Load24HourMax);
            Near(27, context.BatteryVoltage24HourMean);
            Near(2, context.DaysSinceLastDischarge);
            Equal(at.AddDays(-3), context.HistoryStartedAt);
        }

        AssertRecovered(record.Context!);
        Equal(true, record.ContextBackfillAttempted);
        await configStore.SaveAsync(configuration);
        record = (await configStore.LoadAsync()).BatteryHealth.Profiles.Single().OfficialHealthRecords.Single();
        AssertRecovered(record.Context!);
        Equal(true, record.ContextBackfillAttempted);
        var calls = source.Calls;
        Equal(0, await OfficialHealthContextService.BackfillAsync([record], "dev", source));
        Equal(calls, source.Calls);

        // 正常な空結果は記録時に完了とし、起動時には問い合わせない。
        var empty = OfficialRecord(at.AddYears(-1), 60);
        await OfficialHealthContextService.CaptureForRecordAsync(empty, source, "dev", at, null);
        Equal(null, empty.Context);
        Equal(true, empty.ContextBackfillAttempted);
        calls = source.Calls;
        Equal(0, await OfficialHealthContextService.BackfillAsync([empty], "dev", source));
        Equal(calls, source.Calls);

        // source がない場合も履歴は未完了。live があればその値は残す。
        var unavailable = OfficialRecord(at, 59);
        await OfficialHealthContextService.CaptureForRecordAsync(
            unavailable, null, "dev", at, live);
        Equal(live, unavailable.Context);
        Equal(false, unavailable.ContextBackfillAttempted);
    }
    finally
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(testRoot, recursive: true);
    }
}

static void SqliteHistoryRoundTrip() => SqliteHistoryRoundTripAsync().GetAwaiter().GetResult();

static async Task SqliteHistoryRoundTripAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), "UpsMonitor.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
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

static void LegacySchemaV1IndexMigration() => LegacySchemaV1IndexMigrationAsync().GetAwaiter().GetResult();

static async Task LegacySchemaV1IndexMigrationAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsMigrationTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(paths.TelemetryDatabaseFile)!);

        // Recreate the pre-merge "v1" schema: full DDL minus the two new time indexes,
        // ending on PRAGMA user_version=1 - exactly what every pre-existing installation has on disk.
        using (var legacyConnection = new SqliteConnection($"Data Source={paths.TelemetryDatabaseFile}"))
        {
            await legacyConnection.OpenAsync();
            using var command = legacyConnection.CreateCommand();
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS telemetry_samples (
                    id INTEGER PRIMARY KEY,
                    device_id TEXT NOT NULL,
                    timestamp_utc_ms INTEGER NOT NULL,
                    is_connected INTEGER NOT NULL,
                    power_state INTEGER NOT NULL,
                    input_voltage REAL,
                    output_voltage REAL,
                    battery_voltage REAL,
                    battery_percent REAL,
                    runtime_seconds REAL,
                    load_percent REAL,
                    active_power_watts REAL,
                    apparent_power_va REAL,
                    frequency_hz REAL,
                    temperature_c REAL,
                    ac_present INTEGER,
                    charging INTEGER,
                    discharging INTEGER,
                    low_battery INTEGER,
                    shutdown_imminent INTEGER,
                    overload INTEGER,
                    boost INTEGER,
                    UNIQUE(device_id, timestamp_utc_ms)
                );
                CREATE INDEX IF NOT EXISTS ix_telemetry_samples_device_time
                    ON telemetry_samples(device_id, timestamp_utc_ms);

                CREATE TABLE IF NOT EXISTS telemetry_rollups_1m (
                    device_id TEXT NOT NULL,
                    bucket_utc_ms INTEGER NOT NULL,
                    metric INTEGER NOT NULL,
                    minimum REAL NOT NULL,
                    maximum REAL NOT NULL,
                    value_sum REAL NOT NULL,
                    sample_count INTEGER NOT NULL,
                    last_value REAL NOT NULL,
                    last_timestamp_utc_ms INTEGER NOT NULL,
                    PRIMARY KEY(device_id, bucket_utc_ms, metric)
                );
                CREATE INDEX IF NOT EXISTS ix_rollups_device_metric_time
                    ON telemetry_rollups_1m(device_id, metric, bucket_utc_ms);

                CREATE TABLE IF NOT EXISTS raw_telemetry_values (
                    id INTEGER PRIMARY KEY,
                    device_id TEXT NOT NULL,
                    timestamp_utc_ms INTEGER NOT NULL,
                    metric_key TEXT NOT NULL,
                    usage_page INTEGER NOT NULL,
                    usage INTEGER NOT NULL,
                    report_type TEXT NOT NULL,
                    report_id INTEGER NOT NULL,
                    numeric_value REAL NOT NULL,
                    raw_value INTEGER,
                    unit_symbol TEXT,
                    usage_name TEXT NOT NULL,
                    collection_path TEXT NOT NULL,
                    is_vendor_defined INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_raw_values_device_metric_time
                    ON raw_telemetry_values(device_id, metric_key, timestamp_utc_ms);

                CREATE TABLE IF NOT EXISTS ups_events (
                    id INTEGER PRIMARY KEY,
                    device_id TEXT NOT NULL,
                    timestamp_utc_ms INTEGER NOT NULL,
                    event_type INTEGER NOT NULL,
                    message TEXT NOT NULL,
                    previous_state INTEGER NOT NULL,
                    current_state INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_events_device_time
                    ON ups_events(device_id, timestamp_utc_ms);

                CREATE TABLE IF NOT EXISTS ups_state_changes (
                    id INTEGER PRIMARY KEY,
                    device_id TEXT NOT NULL,
                    timestamp_utc_ms INTEGER NOT NULL,
                    power_state INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_state_changes_device_time
                    ON ups_state_changes(device_id, timestamp_utc_ms);

                CREATE TABLE IF NOT EXISTS battery_health_observations (
                    id INTEGER PRIMARY KEY,
                    device_id TEXT NOT NULL,
                    timestamp_utc_ms INTEGER NOT NULL,
                    health_percent REAL,
                    relative_performance_percent REAL,
                    status INTEGER NOT NULL,
                    method INTEGER NOT NULL,
                    confidence INTEGER NOT NULL,
                    anchor_source TEXT,
                    vendor_category INTEGER NOT NULL,
                    replacement_status INTEGER NOT NULL
                );
                CREATE INDEX IF NOT EXISTS ix_health_device_time
                    ON battery_health_observations(device_id, timestamp_utc_ms);

                INSERT INTO telemetry_samples
                    (device_id, timestamp_utc_ms, is_connected, power_state, input_voltage)
                    VALUES ('legacy-device', 1000, 1, 1, 100.0);

                PRAGMA user_version=1;
                """;
            await command.ExecuteNonQueryAsync();
        }

        // Sanity check: the new v2 indexes must not exist yet on the legacy database.
        await AssertIndexExistsAsync(paths.TelemetryDatabaseFile, "ix_telemetry_samples_time", expected: false);
        await AssertIndexExistsAsync(paths.TelemetryDatabaseFile, "ix_raw_values_time", expected: false);
        Equal(1L, await ReadUserVersionAsync(paths.TelemetryDatabaseFile));

        // (b) Existing user at v1: initializing should add only the new indexes, keep existing
        // data intact, and bump user_version to 2 - without erroring on CREATE INDEX against
        // tables that already exist.
        await using (var store = new SqliteTelemetryStore(paths, new HistoryConfiguration()))
        {
            await store.InitializeAsync();
        }
        SqliteConnection.ClearAllPools();

        Equal(2L, await ReadUserVersionAsync(paths.TelemetryDatabaseFile));
        await AssertIndexExistsAsync(paths.TelemetryDatabaseFile, "ix_telemetry_samples_time", expected: true);
        await AssertIndexExistsAsync(paths.TelemetryDatabaseFile, "ix_raw_values_time", expected: true);
        Equal(1L, await CountRowsAsync(paths.TelemetryDatabaseFile, "telemetry_samples"));

        // (c) Re-initializing an already-migrated (v2) database must be a no-op: version stays at
        // 2, indexes and data remain untouched, and it must not throw.
        await using (var store = new SqliteTelemetryStore(paths, new HistoryConfiguration()))
        {
            await store.InitializeAsync();
        }
        SqliteConnection.ClearAllPools();

        Equal(2L, await ReadUserVersionAsync(paths.TelemetryDatabaseFile));
        await AssertIndexExistsAsync(paths.TelemetryDatabaseFile, "ix_telemetry_samples_time", expected: true);
        await AssertIndexExistsAsync(paths.TelemetryDatabaseFile, "ix_raw_values_time", expected: true);
        Equal(1L, await CountRowsAsync(paths.TelemetryDatabaseFile, "telemetry_samples"));
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

static async Task<long> ReadUserVersionAsync(string databasePath)
{
    using var connection = new SqliteConnection($"Data Source={databasePath}");
    await connection.OpenAsync();
    using var command = connection.CreateCommand();
    command.CommandText = "PRAGMA user_version;";
    return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
}

static async Task<long> CountRowsAsync(string databasePath, string tableName)
{
    using var connection = new SqliteConnection($"Data Source={databasePath}");
    await connection.OpenAsync();
    using var command = connection.CreateCommand();
    command.CommandText = $"SELECT COUNT(*) FROM {tableName};";
    return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
}

static async Task AssertIndexExistsAsync(string databasePath, string indexName, bool expected)
{
    using var connection = new SqliteConnection($"Data Source={databasePath}");
    await connection.OpenAsync();
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name=$name;";
    command.Parameters.AddWithValue("$name", indexName);
    var count = Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    if (expected && count == 0)
    {
        throw new InvalidOperationException($"Expected index '{indexName}' to exist, but it was not found.");
    }
    if (!expected && count != 0)
    {
        throw new InvalidOperationException($"Expected index '{indexName}' to not exist yet, but it was found.");
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
        var paths = new AppPaths(testRoot);
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

static void WeeklyEquivalentDays()
{
    var result = new WeeklyPatternResult
    {
        Metric = WeeklyPatternMetric.PowerFactor, From = DateTimeOffset.UnixEpoch,
        To = DateTimeOffset.UnixEpoch.AddDays(7), OverallMin = 0, OverallMax = 0,
        OverallAvg = 0, Grid = [], TotalSamples = 5493,
    };
    Near(5493 / 1440.0, result.EquivalentDays);
    Near(0, (result with { TotalSamples = 0 }).EquivalentDays);
    Near(1, (result with { TotalSamples = 1440 }).EquivalentDays);
    Near(result.EquivalentDays, (result with { From = result.From.AddHours(9), To = result.To.AddDays(23) }).EquivalentDays);
    foreach (var language in new[] { "ja-JP", "en-US" })
    {
        var path = Path.Combine(AppContext.BaseDirectory, "../../../../UpsMonitor.App/Resources", $"Strings.{language}.xaml");
        var doc = System.Xml.Linq.XDocument.Load(path);
        var key = System.Xml.Linq.XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml");
        var format = doc.Root!.Elements().Single(e => (string?)e.Attribute(key) == "AnalyticsSampleCountFormat").Value;
        var text = string.Format(CultureInfo.InvariantCulture, format, result.TotalSamples, result.EquivalentDays);
        if (!text.Contains("3.8")) throw new InvalidOperationException(text);
    }
}
static void WeeklyPatternAggregation()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsPatternTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        var store = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        store.InitializeAsync().GetAwaiter().GetResult();

        var device = new UpsDeviceInfo("test-path", 0x0764, 0x0601, "Vendor", "Product", "TEST_WEEKLY", 0x84, 0x04, 64, 64);
        var targetTime = new DateTimeOffset(2026, 8, 24, 14, 30, 0, TimeSpan.FromHours(9)); // Monday 14:30

        var snapshot1 = Snapshot(
            device: device,
            activePower: 350.0,
            apparentPower: 400.0,
            load: 45.0,
            inputVoltage: 100.0,
            timestamp: targetTime);
        var snapshot2 = Snapshot(
            device: device,
            activePower: 450.0,
            apparentPower: 500.0,
            load: 55.0,
            inputVoltage: 110.0,
            timestamp: targetTime.AddMinutes(15));

        store.WriteAsync(snapshot1, CancellationToken.None).GetAwaiter().GetResult();
        store.WriteAsync(snapshot2, CancellationToken.None).GetAwaiter().GetResult();
        store.WriteAsync(snapshot2 with { Timestamp = targetTime.AddMinutes(15).AddSeconds(10) }, CancellationToken.None).GetAwaiter().GetResult();
        store.FlushAsync().GetAwaiter().GetResult();

        var result = store.QueryWeeklyPatternAsync(
            UpsDeviceIdentity.Create(device),
            targetTime.AddDays(-7),
            targetTime.AddDays(7),
            WeeklyPatternMetric.ActivePower).GetAwaiter().GetResult();

        Equal(168, result.Grid.Count);
        Equal(WeeklyPatternMetric.ActivePower, result.Metric);
        Equal(2L, result.TotalSamples);
        Near(1250.0 / 3, result.OverallAvg);
        Near(350.0, result.OverallMin);
        Near(450.0, result.OverallMax);

        var mon14 = result.Grid.First(p => p.DayOfWeek == 1 && p.HourOfDay == 14);
        Equal(2L, mon14.SampleCount);
        Near(1250.0 / 3, mon14.Average);

        foreach (var metric in new[] { WeeklyPatternMetric.InputVoltage, WeeklyPatternMetric.LoadPercent })
        {
            var regression = store.QueryWeeklyPatternAsync(
                UpsDeviceIdentity.Create(device),
                targetTime.AddDays(-7),
                targetTime.AddDays(7),
                metric).GetAwaiter().GetResult();
            Equal(metric, regression.Metric);
            Equal(168, regression.Grid.Count);
            Equal(2L, regression.TotalSamples);
        }

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

static void WeeklyPatternPowerFactorAggregation()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsPowerFactorPatternTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        var store = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        store.InitializeAsync().GetAwaiter().GetResult();

        var device = new UpsDeviceInfo("test-pf", 0x0764, 0x0601, "Vendor", "Product", "TEST_PF", 0x84, 0x04, 64, 64);
        var monday = new DateTimeOffset(2026, 8, 24, 0, 30, 0, TimeSpan.FromHours(9));
        var snapshots = new[]
        {
            Snapshot(device: device, activePower: 50.0, apparentPower: 100.0, timestamp: monday),
            Snapshot(device: device, activePower: 100.0, apparentPower: 200.0, timestamp: monday.AddMinutes(1)),
            // The cell average must be the average of per-minute ratios, not a ratio of hourly totals.
            Snapshot(device: device, activePower: 90.0, apparentPower: 100.0, timestamp: monday.AddMinutes(6)),
            // VA <= 1 and missing VA are excluded from the derived metric.
            Snapshot(device: device, activePower: 80.0, apparentPower: 1.0, timestamp: monday.AddMinutes(2)),
            Snapshot(device: device, activePower: 80.0, timestamp: monday.AddMinutes(3)),
            // Clamp values outside the valid percentage range.
            Snapshot(device: device, activePower: 0.0, apparentPower: 100.0, timestamp: monday.AddHours(1)),
            Snapshot(device: device, activePower: 300.0, apparentPower: 100.0, timestamp: monday.AddHours(1).AddMinutes(1)),
            // Cross the local Sunday/Monday boundary.
            Snapshot(device: device, activePower: 25.0, apparentPower: 100.0, timestamp: monday.AddMinutes(-45)),
        };
        foreach (var snapshot in snapshots)
        {
            store.WriteAsync(snapshot, CancellationToken.None).GetAwaiter().GetResult();
        }
        store.FlushAsync().GetAwaiter().GetResult();

        var result = store.QueryWeeklyPatternAsync(
            UpsDeviceIdentity.Create(device),
            monday.AddHours(-1),
            monday.AddHours(2),
            WeeklyPatternMetric.PowerFactor).GetAwaiter().GetResult();

        Equal(WeeklyPatternMetric.PowerFactor, result.Metric);
        Equal(168, result.Grid.Count);
        Equal(6L, result.TotalSamples);
        Near(0.0, result.OverallMin);
        Near(100.0, result.OverallMax);
        var mondayMidnight = result.Grid.Single(p => p.DayOfWeek == 1 && p.HourOfDay == 0);
        Equal(3L, mondayMidnight.SampleCount);
        Near(50.0, mondayMidnight.Minimum);
        Near(190.0 / 3.0, mondayMidnight.Average);
        Near(90.0, mondayMidnight.Maximum);
        var sunday = result.Grid.Single(p => p.DayOfWeek == 0 && p.HourOfDay == 23);
        Equal(1L, sunday.SampleCount);
        Near(25.0, sunday.Average);

        foreach (var (from, to, expected) in new[]
        {
            (monday.AddMinutes(-45), monday, 2L),
            (monday.AddDays(-7), monday.AddDays(-1), 0L),
            (monday.AddDays(-7), monday.AddDays(7), 6L),
        })
        {
            var range = store.QueryWeeklyPatternAsync(UpsDeviceIdentity.Create(device), from, to,
                WeeklyPatternMetric.PowerFactor).GetAwaiter().GetResult();
            Equal(expected, range.TotalSamples);
            Near(expected / 1440.0, range.EquivalentDays);
        }
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
    Equal(5.0, config.Alerts.LoadHysteresisPercent);
    Equal(2.0, config.Alerts.VoltageHysteresisVolts);
    Equal(false, config.Webhook.Enabled);
    Equal(false, config.ExternalCommand.Enabled);
    Equal(string.Empty, config.ExternalCommand.CommandOnHighLoad);
    Equal(string.Empty, config.ExternalCommand.CommandOnVoltageAbnormal);

    var thresholds = config.Alerts.ToAlertThresholds();
    Equal(80.0, thresholds.HighLoadPercent);
    Equal(92.0, thresholds.LowVoltage);
    Equal(108.0, thresholds.HighVoltage);
    Equal(5.0, thresholds.LoadHysteresisPercent);
    Equal(2.0, thresholds.VoltageHysteresisVolts);

    // Modify and verify
    config.Ui.Theme = "dark";
    Equal("dark", config.Ui.Theme);
    config.Alerts.HighLoadWarningPercent = 85.0;
    Equal(85.0, config.Alerts.HighLoadWarningPercent);
    config.Alerts.LoadHysteresisPercent = 10.0;
    Equal(10.0, config.Alerts.LoadHysteresisPercent);
    config.Alerts.VoltageHysteresisVolts = 3.5;
    Equal(3.5, config.Alerts.VoltageHysteresisVolts);
    config.ExternalCommand.CommandOnHighLoad = "scripts/on_high_load.bat";
    Equal("scripts/on_high_load.bat", config.ExternalCommand.CommandOnHighLoad);
    config.ExternalCommand.CommandOnVoltageAbnormal = "scripts/on_voltage.bat";
    Equal("scripts/on_voltage.bat", config.ExternalCommand.CommandOnVoltageAbnormal);
}

static void DailyEnergyAndTroubleSummaryQueries()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsDailyTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);

    SqliteTelemetryStore? store = null;
    try
    {
        var paths = new AppPaths(testRoot);
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
        var today = DateTime.Today;
        var from = new DateTimeOffset(today.AddDays(-6));
        var to = new DateTimeOffset(today.AddDays(1)).AddTicks(-1);
        var reports = store.QueryEnergyReportsAsync(devId, from, to, EnergyReportPeriod.Day, 31.0).GetAwaiter().GetResult();
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
    double? inputVoltage = null,
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
        InputVoltage = inputVoltage,
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
        var paths = new AppPaths(testRoot);
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

            Explain("1. QueryEnergyReportsAsync (Batch daily/monthly active power via rollups)",
                """
                SELECT
                    strftime('%Y-%m-%d', datetime(bucket_utc_ms / 1000, 'unixepoch', 'localtime')) AS period_key,
                    SUM((value_sum * 1.0 / sample_count) / 60.0 / 1000.0) AS energy_kwh,
                    SUM(value_sum) / SUM(sample_count) AS avg_watts,
                    MAX(maximum) AS peak_watts
                FROM telemetry_rollups_1m
                WHERE device_id = $device
                    AND metric = $metric
                    AND bucket_utc_ms >= $from
                    AND bucket_utc_ms <= $to
                GROUP BY period_key;
                """,
                ("$device", devId), ("$metric", (int)TelemetryMetric.ActivePowerWatts), ("$from", start.ToUnixTimeMilliseconds()), ("$to", start.AddDays(30).ToUnixTimeMilliseconds()));

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
        var reportFrom = new DateTimeOffset(start.LocalDateTime.Date);
        var reportTo = new DateTimeOffset(start.LocalDateTime.Date.AddDays(30)).AddTicks(-1);
        var dailyReports = await store.QueryEnergyReportsAsync(devId, reportFrom, reportTo, EnergyReportPeriod.Day, 31.0);
        Console.WriteLine($"[BENCH] QueryEnergyReportsAsync(30 days, Day): {sw.ElapsedMilliseconds} ms, count={dailyReports.Count}");

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
        var weekly = await store.QueryWeeklyPatternAsync(devId, start, start.AddDays(30), WeeklyPatternMetric.ActivePower);
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

static void NavigationTabRefreshRoutingRules()
{
    Equal(0, NavigationTestRouter.DashboardIndex);
    Equal(1, NavigationTestRouter.HistoryIndex);
    Equal(2, NavigationTestRouter.UpsIndex);
    Equal(3, NavigationTestRouter.AnalyticsIndex);
    Equal(4, NavigationTestRouter.DevicesIndex);
    Equal(5, NavigationTestRouter.ActionsIndex);
    Equal(6, NavigationTestRouter.LogsIndex);
    Equal(7, NavigationTestRouter.SettingsIndex);

    // History refresh targets: Dashboard(0) and History(1)
    Equal(true, NavigationTestRouter.IsHistoryRefreshTarget(NavigationTestRouter.DashboardIndex));
    Equal(true, NavigationTestRouter.IsHistoryRefreshTarget(NavigationTestRouter.HistoryIndex));
    Equal(false, NavigationTestRouter.IsHistoryRefreshTarget(NavigationTestRouter.UpsIndex));
    Equal(false, NavigationTestRouter.IsHistoryRefreshTarget(NavigationTestRouter.AnalyticsIndex));
    Equal(false, NavigationTestRouter.IsHistoryRefreshTarget(NavigationTestRouter.DevicesIndex));
    Equal(false, NavigationTestRouter.IsHistoryRefreshTarget(NavigationTestRouter.ActionsIndex));
    Equal(false, NavigationTestRouter.IsHistoryRefreshTarget(NavigationTestRouter.LogsIndex));
    Equal(false, NavigationTestRouter.IsHistoryRefreshTarget(NavigationTestRouter.SettingsIndex));
    Equal(false, NavigationTestRouter.IsHistoryRefreshTarget(-1));
    Equal(false, NavigationTestRouter.IsHistoryRefreshTarget(99));

    // Analytics refresh target: Analytics(3) only
    Equal(false, NavigationTestRouter.IsAnalyticsRefreshTarget(NavigationTestRouter.DashboardIndex));
    Equal(false, NavigationTestRouter.IsAnalyticsRefreshTarget(NavigationTestRouter.HistoryIndex));
    Equal(false, NavigationTestRouter.IsAnalyticsRefreshTarget(NavigationTestRouter.UpsIndex));
    Equal(true, NavigationTestRouter.IsAnalyticsRefreshTarget(NavigationTestRouter.AnalyticsIndex));
    Equal(false, NavigationTestRouter.IsAnalyticsRefreshTarget(NavigationTestRouter.DevicesIndex));
    Equal(false, NavigationTestRouter.IsAnalyticsRefreshTarget(NavigationTestRouter.ActionsIndex));
    Equal(false, NavigationTestRouter.IsAnalyticsRefreshTarget(NavigationTestRouter.LogsIndex));
    Equal(false, NavigationTestRouter.IsAnalyticsRefreshTarget(NavigationTestRouter.SettingsIndex));
    Equal(false, NavigationTestRouter.IsAnalyticsRefreshTarget(-1));
    Equal(false, NavigationTestRouter.IsAnalyticsRefreshTarget(99));
}

static void NavigationRefreshOnTabSelection()
{
    var sim = new NavigationSessionSimulator();
    sim.ReceiveSnapshot();
    sim.IsWindowVisible = true;

    // Initially at Dashboard (0), history refresh triggered once
    Equal(1, sim.HistoryRefreshCount);
    Equal(0, sim.AnalyticsRefreshCount);

    // Switch to UPS (index 2): neither History nor Analytics should refresh
    sim.AdvanceTime(TimeSpan.FromSeconds(6));
    sim.SelectedNavigationIndex = NavigationTestRouter.UpsIndex;
    Equal(1, sim.HistoryRefreshCount);
    Equal(0, sim.AnalyticsRefreshCount);

    // Switch to Analytics (index 3): Analytics refreshes exactly once
    sim.AdvanceTime(TimeSpan.FromSeconds(6));
    sim.SelectedNavigationIndex = NavigationTestRouter.AnalyticsIndex;
    Equal(1, sim.HistoryRefreshCount);
    Equal(1, sim.AnalyticsRefreshCount);
    sim.CompleteAnalyticsRefresh();

    // Switch to History (index 1): History refreshes
    sim.AdvanceTime(TimeSpan.FromSeconds(6));
    sim.SelectedNavigationIndex = NavigationTestRouter.HistoryIndex;
    Equal(2, sim.HistoryRefreshCount);
    Equal(1, sim.AnalyticsRefreshCount);

    // Switch to Dashboard (index 0): History refreshes
    sim.AdvanceTime(TimeSpan.FromSeconds(6));
    sim.SelectedNavigationIndex = NavigationTestRouter.DashboardIndex;
    Equal(3, sim.HistoryRefreshCount);
    Equal(1, sim.AnalyticsRefreshCount);

    // Switch to Analytics (index 3) and trigger rapid multiple calls while pending -> verifies cancellation of previous request
    sim.AdvanceTime(TimeSpan.FromSeconds(6));
    sim.SelectedNavigationIndex = NavigationTestRouter.AnalyticsIndex;
    Equal(2, sim.AnalyticsRefreshCount);
    Equal(0, sim.CancelledAnalyticsCount);

    // Rapid second call while first is still running
    sim.SimulateRapidAnalyticsRefresh();
    Equal(3, sim.AnalyticsRefreshCount);
    Equal(1, sim.CancelledAnalyticsCount);
    sim.CompleteAnalyticsRefresh();
}

static void NavigationRefreshOnVisibilityChange()
{
    var sim = new NavigationSessionSimulator();
    sim.ReceiveSnapshot();
    sim.IsWindowVisible = false;

    // While hidden, switch to Analytics (index 3): no refresh should occur
    sim.SelectedNavigationIndex = NavigationTestRouter.AnalyticsIndex;
    Equal(0, sim.AnalyticsRefreshCount);
    Equal(0, sim.HistoryRefreshCount);

    // Window becomes visible: Analytics refresh should trigger once
    sim.AdvanceTime(TimeSpan.FromSeconds(6));
    sim.IsWindowVisible = true;
    Equal(1, sim.AnalyticsRefreshCount);
    Equal(0, sim.HistoryRefreshCount);

    // Hide window, switch to UPS (index 2), then make visible: no refresh
    sim.IsWindowVisible = false;
    sim.SelectedNavigationIndex = NavigationTestRouter.UpsIndex;
    sim.AdvanceTime(TimeSpan.FromSeconds(6));
    sim.IsWindowVisible = true;
    Equal(1, sim.AnalyticsRefreshCount);
    Equal(0, sim.HistoryRefreshCount);

    // Hide window, switch to History (index 1), then make visible: History refreshes once
    sim.IsWindowVisible = false;
    sim.SelectedNavigationIndex = NavigationTestRouter.HistoryIndex;
    sim.AdvanceTime(TimeSpan.FromSeconds(6));
    sim.IsWindowVisible = true;
    Equal(1, sim.AnalyticsRefreshCount);
    Equal(1, sim.HistoryRefreshCount);
}

static void NavigationRefreshOnLanguageChange()
{
    var sim = new NavigationSessionSimulator();
    sim.ReceiveSnapshot();
    sim.IsWindowVisible = true;

    // Reset counts from initial show
    sim.ResetCounts();

    // Language changed on Analytics tab (index 3) -> Analytics re-queried
    sim.SelectedNavigationIndex = NavigationTestRouter.AnalyticsIndex;
    sim.ResetCounts();
    sim.ChangeLanguage();
    Equal(1, sim.AnalyticsRefreshCount);
    Equal(0, sim.HistoryRefreshCount);

    // Language changed on History tab (index 1) -> History re-queried
    sim.SelectedNavigationIndex = NavigationTestRouter.HistoryIndex;
    sim.ResetCounts();
    sim.ChangeLanguage();
    Equal(0, sim.AnalyticsRefreshCount);
    Equal(1, sim.HistoryRefreshCount);

    // Language changed on Dashboard tab (index 0) -> History re-queried
    sim.SelectedNavigationIndex = NavigationTestRouter.DashboardIndex;
    sim.ResetCounts();
    sim.ChangeLanguage();
    Equal(0, sim.AnalyticsRefreshCount);
    Equal(1, sim.HistoryRefreshCount);

    // Language changed on UPS tab (index 2) -> no refresh
    sim.SelectedNavigationIndex = NavigationTestRouter.UpsIndex;
    sim.ResetCounts();
    sim.ChangeLanguage();
    Equal(0, sim.AnalyticsRefreshCount);
    Equal(0, sim.HistoryRefreshCount);

    // Language changed on Logs tab (index 6) -> no refresh
    sim.SelectedNavigationIndex = NavigationTestRouter.LogsIndex;
    sim.ResetCounts();
    sim.ChangeLanguage();
    Equal(0, sim.AnalyticsRefreshCount);
    Equal(0, sim.HistoryRefreshCount);

    // Language changed while hidden -> no refresh
    sim.IsWindowVisible = false;
    sim.SelectedNavigationIndex = NavigationTestRouter.AnalyticsIndex;
    sim.ResetCounts();
    sim.ChangeLanguage();
    Equal(0, sim.AnalyticsRefreshCount);
    Equal(0, sim.HistoryRefreshCount);
}

static void HighLoadAlertDetectorEdgeAndHysteresis()
{
    var thresholds = new UpsAlertThresholds(HighLoadPercent: 80.0, LoadHysteresisPercent: 5.0);
    var detector = new UpsEventDetector(TimeSpan.FromMinutes(3), thresholds);

    // Initial observation with high load (85% >= 80%) should trigger HighLoadWarning immediately
    var initialHigh = detector.Observe(Snapshot(connected: true, ac: true, load: 85.0));
    HasType(initialHigh, UpsEventType.HighLoadWarning);
    Equal(1, initialHigh.Count(e => e.Type == UpsEventType.HighLoadWarning));

    // Continued high load (85%) should NOT produce additional events (edge triggered)
    var continuedHigh = detector.Observe(Snapshot(connected: true, ac: true, load: 85.0));
    Equal(0, continuedHigh.Count(e => e.Type == UpsEventType.HighLoadWarning));

    // Inside hysteresis band (78%: between 75% and 80%) should NOT clear the alert or trigger new events
    var inHysteresis = detector.Observe(Snapshot(connected: true, ac: true, load: 78.0));
    Equal(0, inHysteresis.Count(e => e.Type == UpsEventType.HighLoadWarning));

    // Load drops below recovery threshold (74% < 75%) -> clears active state, no event
    var recovered = detector.Observe(Snapshot(connected: true, ac: true, load: 74.0));
    Equal(0, recovered.Count(e => e.Type == UpsEventType.HighLoadWarning));

    // Load rises again to 82% (>= 80%) -> triggers HighLoadWarning again!
    var retriggered = detector.Observe(Snapshot(connected: true, ac: true, load: 82.0));
    HasType(retriggered, UpsEventType.HighLoadWarning);
}

static void VoltageAbnormalMessageMatchesWarningThresholds()
{
    var previousCulture = CultureInfo.CurrentCulture;
    try
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        foreach (var (voltage, low, high, expected) in new[]
        {
            (92.0, 92.0, 108.0, "Input voltage is abnormal: 92V (warning: <= 92V or >= 108V)"),
            (108.0, 92.0, 108.0, "Input voltage is abnormal: 108V (warning: <= 92V or >= 108V)"),
            (92.4, 92.4, 108.6, "Input voltage is abnormal: 92.4V (warning: <= 92.4V or >= 108.6V)"),
            (108.6, 92.4, 108.6, "Input voltage is abnormal: 108.6V (warning: <= 92.4V or >= 108.6V)"),
            (91.46, 92.4, 108.6, "Input voltage is abnormal: 91.5V (warning: <= 92.4V or >= 108.6V)"),
        })
        {
            var detector = new UpsEventDetector(TimeSpan.FromMinutes(3), new UpsAlertThresholds(LowVoltage: low, HighVoltage: high));
            _ = detector.Observe(Snapshot(connected: true, ac: true, inputVoltage: 100.0));
            var events = detector.Observe(Snapshot(connected: true, ac: true, inputVoltage: voltage));
            Equal(expected, events.Single(e => e.Type == UpsEventType.VoltageAbnormal).Message);
        }
    }
    finally
    {
        CultureInfo.CurrentCulture = previousCulture;
    }
}

static void VoltageAbnormalAlertDetectorEdgeAndHysteresis()
{
    var thresholds = new UpsAlertThresholds(LowVoltage: 92.0, HighVoltage: 108.0, VoltageHysteresisVolts: 2.0);
    var detector = new UpsEventDetector(TimeSpan.FromMinutes(3), thresholds);

    // Initial normal voltage (100V) -> no alert
    var initialNormal = detector.Observe(Snapshot(connected: true, ac: true, inputVoltage: 100.0));
    Equal(0, initialNormal.Count(e => e.Type == UpsEventType.VoltageAbnormal));

    // Sag: Input voltage drops to 90V (<= 92V) -> triggers VoltageAbnormal
    var lowVoltage = detector.Observe(Snapshot(connected: true, ac: true, inputVoltage: 90.0));
    HasType(lowVoltage, UpsEventType.VoltageAbnormal);

    // Continued low voltage (90V) -> no additional event
    var lowContinued = detector.Observe(Snapshot(connected: true, ac: true, inputVoltage: 90.0));
    Equal(0, lowContinued.Count(e => e.Type == UpsEventType.VoltageAbnormal));

    // Inside low hysteresis band (93V: 92V < 93V <= 94V) -> remains active, no event
    var lowHysteresis = detector.Observe(Snapshot(connected: true, ac: true, inputVoltage: 93.0));
    Equal(0, lowHysteresis.Count(e => e.Type == UpsEventType.VoltageAbnormal));

    // Recovers above recovery threshold (95V > 94V) -> cleared, no event
    var lowRecovered = detector.Observe(Snapshot(connected: true, ac: true, inputVoltage: 95.0));
    Equal(0, lowRecovered.Count(e => e.Type == UpsEventType.VoltageAbnormal));

    // Surge: Input voltage rises to 110V (>= 108V) -> triggers VoltageAbnormal
    var highVoltage = detector.Observe(Snapshot(connected: true, ac: true, inputVoltage: 110.0));
    HasType(highVoltage, UpsEventType.VoltageAbnormal);

    // Continued high voltage (110V) -> no event
    var highContinued = detector.Observe(Snapshot(connected: true, ac: true, inputVoltage: 110.0));
    Equal(0, highContinued.Count(e => e.Type == UpsEventType.VoltageAbnormal));

    // Inside high hysteresis band (107V: 106V <= 107V < 108V) -> remains active, no event
    var highHysteresis = detector.Observe(Snapshot(connected: true, ac: true, inputVoltage: 107.0));
    Equal(0, highHysteresis.Count(e => e.Type == UpsEventType.VoltageAbnormal));

    // Recovers below recovery threshold (105V < 106V) -> cleared, no event
    var highRecovered = detector.Observe(Snapshot(connected: true, ac: true, inputVoltage: 105.0));
    Equal(0, highRecovered.Count(e => e.Type == UpsEventType.VoltageAbnormal));

    // During power outage (ac: false), 0V input voltage must NOT trigger VoltageAbnormal (it is a PowerLost event)
    var powerOutage = detector.Observe(Snapshot(connected: true, ac: false, inputVoltage: 0.0));
    HasType(powerOutage, UpsEventType.PowerLost);
    Equal(0, powerOutage.Count(e => e.Type == UpsEventType.VoltageAbnormal));
}

static void AlertDetectorDisconnectedNoAlerts()
{
    var detector = new UpsEventDetector(TimeSpan.FromMinutes(3));
    _ = detector.Observe(Snapshot(connected: true, ac: true));

    // Disconnected snapshot with abnormal load and voltage -> only UpsDisconnected, no alert events
    var disconnected = detector.Observe(Snapshot(connected: false, load: 99.0, inputVoltage: 150.0));
    HasSingle(disconnected, UpsEventType.UpsDisconnected);
    Equal(0, disconnected.Count(e => e.Type is UpsEventType.HighLoadWarning or UpsEventType.VoltageAbnormal));
}

static void DynamicAlertThresholdsUpdate()
{
    var detector = new UpsEventDetector(TimeSpan.FromMinutes(3), new UpsAlertThresholds(HighLoadPercent: 80.0, LowVoltage: 92.0, HighVoltage: 108.0));

    // Load at 75% -> no alert with threshold 80%
    var snap1 = Snapshot(connected: true, ac: true, load: 75.0, inputVoltage: 95.0);
    var ev1 = detector.Observe(snap1);
    Equal(0, ev1.Count(e => e.Type is UpsEventType.HighLoadWarning or UpsEventType.VoltageAbnormal));

    // Dynamically lower high load threshold to 70%
    detector.SetAlertThresholds(new UpsAlertThresholds(HighLoadPercent: 70.0, LowVoltage: 92.0, HighVoltage: 108.0));
    var snap2 = Snapshot(connected: true, ac: true, load: 75.0, inputVoltage: 95.0);
    var ev2 = detector.Observe(snap2);
    HasType(ev2, UpsEventType.HighLoadWarning);

    // Dynamically raise low voltage threshold to 96V
    detector.SetAlertThresholds(new UpsAlertThresholds(HighLoadPercent: 70.0, LowVoltage: 96.0, HighVoltage: 108.0));
    var snap3 = Snapshot(connected: true, ac: true, load: 75.0, inputVoltage: 95.0);
    var ev3 = detector.Observe(snap3);
    HasType(ev3, UpsEventType.VoltageAbnormal);
}

static void EngineAlertThresholdsPropagationAndPersistence() => EngineAlertThresholdsPropagationAndPersistenceAsync().GetAwaiter().GetResult();

static async Task EngineAlertThresholdsPropagationAndPersistenceAsync()
{
    var recordedEvents = new List<UpsEvent>();
    var mockSink = new RecordingEventSink(recordedEvents);
    var mockProvider = new TestAlertUpsProvider();

    var thresholds = new UpsAlertThresholds(HighLoadPercent: 70.0, LowVoltage: 90.0, HighVoltage: 110.0);
    await using var engine = new UpsMonitorEngine(
        mockProvider,
        mockSink,
        pollIntervalMs: 250,
        runtimeLowThreshold: TimeSpan.FromMinutes(3),
        snapshotSink: null,
        alertThresholds: thresholds);

    var detectedEvents = new List<UpsEvent>();
    engine.EventDetected += ev =>
    {
        lock (detectedEvents)
        {
            detectedEvents.Add(ev);
        }
    };

    // 1. Initial normal snapshot
    mockProvider.CurrentSnapshot = Snapshot(connected: true, ac: true, load: 50.0, inputVoltage: 100.0, device: mockProvider.Device);
    engine.Start();

    await WaitForConditionAsync(() => {
        lock (detectedEvents) { return detectedEvents.Any(e => e.Type == UpsEventType.UpsReconnected); }
    }, TimeSpan.FromSeconds(2));

    // 2. High load (75% >= 70%) -> HighLoadWarning emitted to EventDetected and mockSink
    mockProvider.CurrentSnapshot = Snapshot(connected: true, ac: true, load: 75.0, inputVoltage: 100.0, device: mockProvider.Device);
    engine.NotifyDeviceChange();

    await WaitForConditionAsync(() => {
        lock (detectedEvents) { return detectedEvents.Any(e => e.Type == UpsEventType.HighLoadWarning); }
    }, TimeSpan.FromSeconds(2));

    lock (detectedEvents)
    {
        HasType(detectedEvents, UpsEventType.HighLoadWarning);
    }
    lock (recordedEvents)
    {
        HasType(recordedEvents, UpsEventType.HighLoadWarning);
    }

    // 3. Dynamic alert threshold update from engine
    engine.SetAlertThresholds(new UpsAlertThresholds(HighLoadPercent: 90.0, LowVoltage: 98.0, HighVoltage: 110.0));
    // Input voltage is 95V (<= 98V) -> VoltageAbnormal
    mockProvider.CurrentSnapshot = Snapshot(connected: true, ac: true, load: 75.0, inputVoltage: 95.0, device: mockProvider.Device);
    engine.NotifyDeviceChange();

    await WaitForConditionAsync(() => {
        lock (detectedEvents) { return detectedEvents.Any(e => e.Type == UpsEventType.VoltageAbnormal); }
    }, TimeSpan.FromSeconds(2));

    lock (detectedEvents)
    {
        HasType(detectedEvents, UpsEventType.VoltageAbnormal);
    }
    lock (recordedEvents)
    {
        HasType(recordedEvents, UpsEventType.VoltageAbnormal);
    }

    await engine.StopAsync();
}

static void AlertEventsSqliteRoundTrip() => AlertEventsSqliteRoundTripAsync().GetAwaiter().GetResult();

static async Task AlertEventsSqliteRoundTripAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsAlertDbTests-{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        await using var store = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        await store.InitializeAsync();

        var device = new UpsDeviceInfo("test-path", 0x0764, 0x0601, "Vendor", "Product", "SN-ALERT", 0x84, 0x04, 64, 64);
        var devId = UpsDeviceIdentity.Create(device);
        var now = DateTimeOffset.UtcNow;

        var snap = Snapshot(connected: true, ac: true, device: device, timestamp: now.AddMinutes(-6), inputVoltage: 100.0);
        await store.WriteAsync(snap, CancellationToken.None);

        var highLoadEv = new UpsEvent(now.AddMinutes(-5), UpsEventType.HighLoadWarning, "UPS load is high: 85%", UpsPowerState.Online, UpsPowerState.Online);
        var voltageEv = new UpsEvent(now.AddMinutes(-2), UpsEventType.VoltageAbnormal, "Input voltage is abnormal: 88V", UpsPowerState.Online, UpsPowerState.Online);

        // Record via IUpsEventSink
        await ((IUpsEventSink)store).WriteAsync(highLoadEv, CancellationToken.None);
        await ((IUpsEventSink)store).WriteAsync(voltageEv, CancellationToken.None);
        await store.FlushAsync();

        var history = await store.QueryHistoryAsync(devId, now.AddMinutes(-10), now.AddMinutes(1), [TelemetryMetric.InputVoltage]);
        Equal(2, history.Events.Count);
        HasType(history.Events, UpsEventType.HighLoadWarning);
        HasType(history.Events, UpsEventType.VoltageAbnormal);
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

static void ConcurrentReaderDoesNotLockTelemetryStoreWriter() => ConcurrentReaderDoesNotLockTelemetryStoreWriterAsync().GetAwaiter().GetResult();

static async Task ConcurrentReaderDoesNotLockTelemetryStoreWriterAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsConcurrentReaderTest-{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        await using var store = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        var errors = new List<Exception>();
        store.StorageError += errors.Add;
        await store.InitializeAsync();

        var device = new UpsDeviceInfo("test-path", 0x0764, 0x0601, "CPS", "Test UPS", "TEST01", 0x84, 0x04, 64, 64);
        var snapshot = Snapshot(connected: true, ac: true, device: device, timestamp: DateTimeOffset.UtcNow, inputVoltage: 100.0);

        await using var readerConnection = new SqliteConnection(store.ConnectionString);
        await readerConnection.OpenAsync();

        await using var readerTransaction = readerConnection.BeginTransaction(deferred: true);
        await using var readerCmd = readerConnection.CreateCommand();
        readerCmd.Transaction = readerTransaction;
        readerCmd.CommandText = "SELECT COUNT(*) FROM telemetry_samples;";
        await using var reader = await readerCmd.ExecuteReaderAsync();
        _ = await reader.ReadAsync();

        await store.WriteAsync(snapshot, CancellationToken.None);
        await store.FlushAsync();

        await reader.DisposeAsync();
        await readerTransaction.CommitAsync();

        Equal(0, errors.Count);

        await using var verifyCmd = readerConnection.CreateCommand();
        verifyCmd.CommandText = "SELECT COUNT(*) FROM telemetry_samples;";
        var count = Convert.ToInt64(await verifyCmd.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
        Equal(1L, count);
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

static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
{
    var start = DateTimeOffset.UtcNow;
    while (!condition())
    {
        if (DateTimeOffset.UtcNow - start > timeout)
        {
            throw new TimeoutException("Condition was not met within timeout.");
        }
        await Task.Delay(20);
    }
}

static void AccurateEnergyReportsAndMonthlyAggregation() => AccurateEnergyReportsAndMonthlyAggregationAsync().GetAwaiter().GetResult();

static async Task AccurateEnergyReportsAndMonthlyAggregationAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsEnergyCoreTests_{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        // raw retention は 7 日に設定（30日超のデータは samples から消える想定）
        var config = new HistoryConfiguration { RawRetentionDays = 7, RawUsageCheckpointSeconds = 30 };
        var store = new SqliteTelemetryStore(paths, config);
        await store.InitializeAsync();

        var device = new UpsDeviceInfo("test-device", 0x0764, 0x0601, "CPS", "Test UPS", "TEST-E-01", 0x84, 0x04, 64, 64);
        var devId = UpsDeviceIdentity.Create(device);

        var localTz = TimeZoneInfo.Local;
        var localNow = DateTimeOffset.Now;

        // 1. 200W を 60 連続分 → 約 0.2kWh & 料金換算 (31.0 円/kWh -> 6.2 円)
        var baseDate = DateOnly.FromDateTime(localNow.LocalDateTime);
        var t1StartLocal = baseDate.ToDateTime(new TimeOnly(10, 0, 0));
        var t1Start = new DateTimeOffset(t1StartLocal, localTz.GetUtcOffset(t1StartLocal));

        using (var conn = new SqliteConnection($"Data Source={store.DatabasePath}"))
        {
            await conn.OpenAsync();
            using var tx = conn.BeginTransaction();
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;

            cmd.CommandText = """
                INSERT INTO telemetry_rollups_1m (
                    device_id, bucket_utc_ms, metric, minimum, maximum, value_sum, sample_count, last_value, last_timestamp_utc_ms
                ) VALUES ($dev, $bucket, $metric, $min, $max, $sum, $cnt, $lastVal, $lastTime);
                """;
            var pDev = cmd.Parameters.Add("$dev", SqliteType.Text);
            var pBucket = cmd.Parameters.Add("$bucket", SqliteType.Integer);
            var pMetric = cmd.Parameters.Add("$metric", SqliteType.Integer);
            var pMin = cmd.Parameters.Add("$min", SqliteType.Real);
            var pMax = cmd.Parameters.Add("$max", SqliteType.Real);
            var pSum = cmd.Parameters.Add("$sum", SqliteType.Real);
            var pCnt = cmd.Parameters.Add("$cnt", SqliteType.Integer);
            var pLastVal = cmd.Parameters.Add("$lastVal", SqliteType.Real);
            var pLastTime = cmd.Parameters.Add("$lastTime", SqliteType.Integer);

            pDev.Value = devId;
            pMetric.Value = (int)TelemetryMetric.ActivePowerWatts;

            // 1. 60 連続分 (200W)
            for (var m = 0; m < 60; m++)
            {
                var bucketTime = t1Start.AddMinutes(m);
                var bMs = bucketTime.ToUnixTimeMilliseconds();
                pBucket.Value = bMs;
                pMin.Value = 200.0;
                pMax.Value = 200.0;
                pSum.Value = 200.0 * 60; // 60 サンプル
                pCnt.Value = 60;
                pLastVal.Value = 200.0;
                pLastTime.Value = bMs + 59000;
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. 1 分のみ (200W) -> 昨日 14:00
            var yesterdayLocal = baseDate.AddDays(-1).ToDateTime(new TimeOnly(14, 0, 0));
            var tYesterday = new DateTimeOffset(yesterdayLocal, localTz.GetUtcOffset(yesterdayLocal));
            {
                var bMs = tYesterday.ToUnixTimeMilliseconds();
                pBucket.Value = bMs;
                pMin.Value = 200.0;
                pMax.Value = 200.0;
                pSum.Value = 200.0 * 60;
                pCnt.Value = 60;
                pLastVal.Value = 200.0;
                pLastTime.Value = bMs + 59000;
                await cmd.ExecuteNonQueryAsync();
            }

            // 3. 長い欠測を積分しない -> 2日前 00:00 に 200W (1分)、23:00 に 200W (1分) のみ
            var twoDaysAgo0Local = baseDate.AddDays(-2).ToDateTime(new TimeOnly(0, 0, 0));
            var twoDaysAgo23Local = baseDate.AddDays(-2).ToDateTime(new TimeOnly(23, 0, 0));
            var tTwoDaysAgo0 = new DateTimeOffset(twoDaysAgo0Local, localTz.GetUtcOffset(twoDaysAgo0Local));
            var tTwoDaysAgo23 = new DateTimeOffset(twoDaysAgo23Local, localTz.GetUtcOffset(twoDaysAgo23Local));
            foreach (var t in new[] { tTwoDaysAgo0, tTwoDaysAgo23 })
            {
                var bMs = t.ToUnixTimeMilliseconds();
                pBucket.Value = bMs;
                pMin.Value = 200.0;
                pMax.Value = 200.0;
                pSum.Value = 200.0 * 60;
                pCnt.Value = 60;
                pLastVal.Value = 200.0;
                pLastTime.Value = bMs + 59000;
                await cmd.ExecuteNonQueryAsync();
            }

            // 4. 当日部分期間 -> 3日前 (120分稼働 100W)
            var threeDaysAgoLocal = baseDate.AddDays(-3).ToDateTime(new TimeOnly(8, 0, 0));
            var tThreeDaysAgo = new DateTimeOffset(threeDaysAgoLocal, localTz.GetUtcOffset(threeDaysAgoLocal));
            for (var m = 0; m < 120; m++)
            {
                var bucketTime = tThreeDaysAgo.AddMinutes(m);
                var bMs = bucketTime.ToUnixTimeMilliseconds();
                pBucket.Value = bMs;
                pMin.Value = 100.0;
                pMax.Value = 100.0;
                pSum.Value = 100.0 * 60;
                pCnt.Value = 60;
                pLastVal.Value = 100.0;
                pLastTime.Value = bMs + 59000;
                await cmd.ExecuteNonQueryAsync();
            }

            // 5. 月跨ぎ / 月次集計 -> 2026年5月31日 12:00 (60分 100W) と 2026年6月1日 12:00 (60分 100W)
            var may31Local = new DateTime(2026, 5, 31, 12, 0, 0);
            var jun1Local = new DateTime(2026, 6, 1, 12, 0, 0);
            var tMay31 = new DateTimeOffset(may31Local, localTz.GetUtcOffset(may31Local));
            var tJun1 = new DateTimeOffset(jun1Local, localTz.GetUtcOffset(jun1Local));
            for (var m = 0; m < 60; m++)
            {
                var bMs = tMay31.AddMinutes(m).ToUnixTimeMilliseconds();
                pBucket.Value = bMs;
                pMin.Value = 100.0;
                pMax.Value = 100.0;
                pSum.Value = 100.0 * 60;
                pCnt.Value = 60;
                pLastVal.Value = 100.0;
                pLastTime.Value = bMs + 59000;
                await cmd.ExecuteNonQueryAsync();
            }
            for (var m = 0; m < 60; m++)
            {
                var bMs = tJun1.AddMinutes(m).ToUnixTimeMilliseconds();
                pBucket.Value = bMs;
                pMin.Value = 100.0;
                pMax.Value = 100.0;
                pSum.Value = 100.0 * 60;
                pCnt.Value = 60;
                pLastVal.Value = 100.0;
                pLastTime.Value = bMs + 59000;
                await cmd.ExecuteNonQueryAsync();
            }

            // 8. 35日前（30日超）の rollup データ (60分 200W = 0.2kWh)
            var thirtyFiveDaysAgoLocal = baseDate.AddDays(-35).ToDateTime(new TimeOnly(10, 0, 0));
            var tThirtyFive = new DateTimeOffset(thirtyFiveDaysAgoLocal, localTz.GetUtcOffset(thirtyFiveDaysAgoLocal));
            for (var m = 0; m < 60; m++)
            {
                var bMs = tThirtyFive.AddMinutes(m).ToUnixTimeMilliseconds();
                pBucket.Value = bMs;
                pMin.Value = 200.0;
                pMax.Value = 200.0;
                pSum.Value = 200.0 * 60;
                pCnt.Value = 60;
                pLastVal.Value = 200.0;
                pLastTime.Value = bMs + 59000;
                await cmd.ExecuteNonQueryAsync();
            }

            // 7. 停電イベントの登録 (5月に2回、6月に1回、baseDateに1回)
            using var evCmd = conn.CreateCommand();
            evCmd.Transaction = tx;
            evCmd.CommandText = """
                INSERT INTO ups_events (device_id, timestamp_utc_ms, event_type, message, previous_state, current_state)
                VALUES ($dev, $time, 0, 'Power lost', 1, 2);
                """;
            var epDev = evCmd.Parameters.Add("$dev", SqliteType.Text);
            var epTime = evCmd.Parameters.Add("$time", SqliteType.Integer);
            epDev.Value = devId;

            epTime.Value = tMay31.ToUnixTimeMilliseconds();
            await evCmd.ExecuteNonQueryAsync();
            epTime.Value = tMay31.AddMinutes(30).ToUnixTimeMilliseconds();
            await evCmd.ExecuteNonQueryAsync();
            epTime.Value = tJun1.ToUnixTimeMilliseconds();
            await evCmd.ExecuteNonQueryAsync();
            epTime.Value = t1Start.AddMinutes(10).ToUnixTimeMilliseconds();
            await evCmd.ExecuteNonQueryAsync();

            await tx.CommitAsync();
        }

        // --- 検証 1 & 料金換算 ---
        // 200W x 60分 = 0.2kWh, 31.0円/kWh -> 6.2円, 停電1回
        var day0Start = new DateTimeOffset(baseDate.ToDateTime(TimeOnly.MinValue), localTz.GetUtcOffset(baseDate.ToDateTime(TimeOnly.MinValue)));
        var day0End = day0Start.AddDays(1);
        var reportsDay0 = await store.QueryEnergyReportsAsync(devId, day0Start, day0End.AddTicks(-1), EnergyReportPeriod.Day, 31.0);
        Equal(1, reportsDay0.Count);
        Near(0.2, reportsDay0[0].EnergyKwh, 0.0001);
        Near(6.2, reportsDay0[0].EstimatedCost, 0.001);
        Near(200.0, reportsDay0[0].AvgWatts, 0.01);
        Near(200.0, reportsDay0[0].PeakWatts, 0.01);
        Equal(1, reportsDay0[0].OutageCount);

        // --- 検証 2 ---
        // 1分のみ = 200W x (1/60)h / 1000 = 0.003333... kWh
        var day1Start = day0Start.AddDays(-1);
        var reportsDay1 = await store.QueryEnergyReportsAsync(devId, day1Start, day1Start.AddDays(1).AddTicks(-1), EnergyReportPeriod.Day, 31.0);
        Equal(1, reportsDay1.Count);
        Near(200.0 / 60000.0, reportsDay1[0].EnergyKwh, 0.00001);
        Near(200.0, reportsDay1[0].AvgWatts, 0.01);
        Near(200.0, reportsDay1[0].PeakWatts, 0.01);

        // --- 検証 3 ---
        // 長い欠測を積分しない: 2分のみ観測 -> 2 x (200 / 60000) = 0.006666... kWh (旧AVG*24の4.8kWhにならない)
        var day2Start = day0Start.AddDays(-2);
        var reportsDay2 = await store.QueryEnergyReportsAsync(devId, day2Start, day2Start.AddDays(1).AddTicks(-1), EnergyReportPeriod.Day, 31.0);
        Equal(1, reportsDay2.Count);
        Near(2 * 200.0 / 60000.0, reportsDay2[0].EnergyKwh, 0.00001);

        // --- 検証 4 ---
        // 部分期間: 120分稼働 100W -> 120 x (100 / 60000) = 0.2 kWh (24時間換算の2.4kWhにならない)
        var day3Start = day0Start.AddDays(-3);
        var reportsDay3 = await store.QueryEnergyReportsAsync(devId, day3Start, day3Start.AddDays(1).AddTicks(-1), EnergyReportPeriod.Day, 31.0);
        Equal(1, reportsDay3.Count);
        Near(0.2, reportsDay3[0].EnergyKwh, 0.0001);
        Near(100.0, reportsDay3[0].AvgWatts, 0.01);

        // --- 検証 5 & 7 ---
        // 月跨ぎ / 月次集計 / 停電 period 集約
        var mayStart = new DateTimeOffset(new DateTime(2026, 5, 1), localTz.GetUtcOffset(new DateTime(2026, 5, 1)));
        var junEnd = new DateTimeOffset(new DateTime(2026, 6, 30, 23, 59, 59), localTz.GetUtcOffset(new DateTime(2026, 6, 30)));
        var monthReports = await store.QueryEnergyReportsAsync(devId, mayStart, junEnd, EnergyReportPeriod.Month, 30.0);
        Equal(2, monthReports.Count);

        // 5月: 0.1 kWh (60分 100W), 停電 2回
        Equal(EnergyReportPeriod.Month, monthReports[0].Period);
        Near(0.1, monthReports[0].EnergyKwh, 0.0001);
        Near(3.0, monthReports[0].EstimatedCost, 0.01);
        Equal(2, monthReports[0].OutageCount);

        // 6月: 0.1 kWh (60分 100W), 停電 1回
        Equal(EnergyReportPeriod.Month, monthReports[1].Period);
        Near(0.1, monthReports[1].EnergyKwh, 0.0001);
        Near(3.0, monthReports[1].EstimatedCost, 0.01);
        Equal(1, monthReports[1].OutageCount);

        // --- 検証 8 ---
        // 35日前 (30日超) の rollup 参照
        var day35Start = day0Start.AddDays(-35);
        var reportsDay35 = await store.QueryEnergyReportsAsync(devId, day35Start, day35Start.AddDays(1).AddTicks(-1), EnergyReportPeriod.Day, 31.0);
        Equal(1, reportsDay35.Count);
        Near(0.2, reportsDay35[0].EnergyKwh, 0.0001);

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

static void HealthHistoryDownsamplingCapsPointsAboveLimit() =>
    HealthHistoryDownsamplingCapsPointsAboveLimitAsync().GetAwaiter().GetResult();

static async Task HealthHistoryDownsamplingCapsPointsAboveLimitAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsHealthDownsample_{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        await using var store = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        await store.InitializeAsync();

        const string devId = "0764:0601:TEST_HEALTH_CAP";
        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var startMs = start.ToUnixTimeMilliseconds();

        await using (var conn = new SqliteConnection($"Data Source={store.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO battery_health_observations (
                    device_id, timestamp_utc_ms, health_percent, relative_performance_percent,
                    status, method, confidence, anchor_source, vendor_category, replacement_status)
                VALUES ($d, $t, $h, $r, 1, 7, 2, 'BHI', 4, 1);
                """;
            var pDev = cmd.Parameters.Add("$d", SqliteType.Text);
            var pTime = cmd.Parameters.Add("$t", SqliteType.Integer);
            var pHealth = cmd.Parameters.Add("$h", SqliteType.Real);
            var pRel = cmd.Parameters.Add("$r", SqliteType.Real);
            pDev.Value = devId;

            for (var i = 0; i < 1500; i++)
            {
                pTime.Value = startMs + (i * 1000L);
                pHealth.Value = 60.0 - (i * 0.01);
                pRel.Value = 95.0 - ((i % 10) * 0.5);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        var result60 = await store.QueryHistoryAsync(
            devId,
            start,
            start.AddSeconds(1499),
            [],
            maximumPoints: 60);

        // Downsampled path returns empty BatteryHealth and populates BatteryHealthSeries with <= 60 points.
        Equal(0, result60.BatteryHealth.Count);
        Equal(true, result60.BatteryHealthSeries is not null);
        Equal(true, result60.BatteryHealthSeries!.HealthPoints.Count is > 0 and <= 60);
        Equal(true, result60.BatteryHealthSeries.RelativePerformancePoints.Count is > 0 and <= 60);

        // Default maximumPoints (720) also caps 1500 points to <= 720 and leaves BatteryHealth empty.
        var resultDefault = await store.QueryHistoryAsync(
            devId,
            start,
            start.AddSeconds(1499),
            []);
        Equal(0, resultDefault.BatteryHealth.Count);
        Equal(true, resultDefault.BatteryHealthSeries!.HealthPoints.Count is > 0 and <= 720);
        Equal(true, resultDefault.BatteryHealthSeries.RelativePerformancePoints.Count is > 0 and <= 720);

        // Even when from/to straddle an epoch bucket boundary (partial buckets at both ends),
        // the bucket count never exceeds maximumPoints (e.g. duration=59000ms with unaligned offset +999ms).
        const string straddleDev = "0764:0601:TEST_HEALTH_STRADDLE";
        var straddleStart = start.AddMilliseconds(999);
        var straddleEnd = straddleStart.AddMilliseconds(59_000);
        await using (var conn = new SqliteConnection($"Data Source={store.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO battery_health_observations (
                    device_id, timestamp_utc_ms, health_percent, relative_performance_percent,
                    status, method, confidence, anchor_source, vendor_category, replacement_status)
                VALUES ($d, $t, 55.0, 90.0, 1, 7, 2, 'BHI', 4, 1);
                """;
            var pDev = cmd.Parameters.Add("$d", SqliteType.Text);
            var pTime = cmd.Parameters.Add("$t", SqliteType.Integer);
            pDev.Value = straddleDev;
            var sMs = straddleStart.ToUnixTimeMilliseconds();
            var eMs = straddleEnd.ToUnixTimeMilliseconds();
            for (var ms = sMs; ms <= eMs; ms += 500L)
            {
                pTime.Value = ms;
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        var straddleResult = await store.QueryHistoryAsync(straddleDev, straddleStart, straddleEnd, [], maximumPoints: 60);
        Equal(0, straddleResult.BatteryHealth.Count);
        Equal(true, straddleResult.BatteryHealthSeries!.HealthPoints.Count is > 0 and <= 60);
        Equal(true, straddleResult.BatteryHealthSeries.RelativePerformancePoints.Count is > 0 and <= 60);
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

static void HealthHistoryPreservesExactPointsAtOrBelowLimit() =>
    HealthHistoryPreservesExactPointsAtOrBelowLimitAsync().GetAwaiter().GetResult();

static async Task HealthHistoryPreservesExactPointsAtOrBelowLimitAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsHealthExact_{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        await using var store = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        await store.InitializeAsync();

        const string devId = "0764:0601:TEST_HEALTH_EXACT";
        var start = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var startMs = start.ToUnixTimeMilliseconds();

        // Insert 60 observations with timestamps inside the same second (same bucket if bucketed)
        // to verify no bucketing occurs when count <= maximumPoints (LIMIT N+1 boundary: N=60).
        await using (var conn = new SqliteConnection($"Data Source={store.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO battery_health_observations (
                    device_id, timestamp_utc_ms, health_percent, relative_performance_percent,
                    status, method, confidence, anchor_source, vendor_category, replacement_status)
                VALUES ($d, $t, $h, $r, 1, 7, 2, 'BHI', 4, 1);
                """;
            var pDev = cmd.Parameters.Add("$d", SqliteType.Text);
            var pTime = cmd.Parameters.Add("$t", SqliteType.Integer);
            var pHealth = cmd.Parameters.Add("$h", SqliteType.Real);
            var pRel = cmd.Parameters.Add("$r", SqliteType.Real);
            pDev.Value = devId;

            for (var i = 0; i < 60; i++)
            {
                pTime.Value = startMs + (i * 10L);
                pHealth.Value = 50.0 + (i * 0.5);
                pRel.Value = i % 2 == 0 ? 90.0 + i : DBNull.Value;
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        var result = await store.QueryHistoryAsync(
            devId,
            start,
            start.AddHours(1),
            [],
            maximumPoints: 60);

        Equal(60, result.BatteryHealth.Count);
        Equal(60, result.BatteryHealthSeries!.HealthPoints.Count);
        Equal(30, result.BatteryHealthSeries.RelativePerformancePoints.Count);

        for (var i = 0; i < 60; i++)
        {
            var expectedTime = start.AddMilliseconds(i * 10L);
            var expectedHealth = 50.0 + (i * 0.5);
            Equal(expectedTime, result.BatteryHealth[i].Timestamp);
            Near(expectedHealth, result.BatteryHealth[i].HealthPercent);

            var pt = result.BatteryHealthSeries.HealthPoints[i];
            Equal(expectedTime, pt.Timestamp);
            Near(expectedHealth, pt.Minimum);
            Near(expectedHealth, pt.Average);
            Near(expectedHealth, pt.Maximum);
            Near(expectedHealth, pt.Last);

            if (i % 2 == 0)
            {
                var expectedRel = 90.0 + i;
                Near(expectedRel, result.BatteryHealth[i].RelativePerformancePercent);
                var relPt = result.BatteryHealthSeries.RelativePerformancePoints[i / 2];
                Equal(expectedTime, relPt.Timestamp);
                Near(expectedRel, relPt.Minimum);
                Near(expectedRel, relPt.Average);
                Near(expectedRel, relPt.Maximum);
                Near(expectedRel, relPt.Last);
            }
            else
            {
                Equal(null, result.BatteryHealth[i].RelativePerformancePercent);
            }
        }

        // Adding the 61st observation (N+1) switches to the downsampled path (BatteryHealth becomes empty,
        // and the 61 observations within 1 second collapse into 1 epoch-aligned bucket).
        await using (var conn = new SqliteConnection($"Data Source={store.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO battery_health_observations (
                    device_id, timestamp_utc_ms, health_percent, relative_performance_percent,
                    status, method, confidence, anchor_source, vendor_category, replacement_status)
                VALUES ($d, $t, 80.0, 95.0, 1, 7, 2, 'BHI', 4, 1);
                """;
            cmd.Parameters.AddWithValue("$d", devId);
            cmd.Parameters.AddWithValue("$t", startMs + 600L);
            await cmd.ExecuteNonQueryAsync();
        }

        var result61 = await store.QueryHistoryAsync(
            devId,
            start,
            start.AddHours(1),
            [],
            maximumPoints: 60);
        Equal(0, result61.BatteryHealth.Count);
        Equal(1, result61.BatteryHealthSeries!.HealthPoints.Count);
        Equal(1, result61.BatteryHealthSeries.RelativePerformancePoints.Count);
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

static void HealthHistoryRangeBoundariesAndEmptyRange() =>
    HealthHistoryRangeBoundariesAndEmptyRangeAsync().GetAwaiter().GetResult();

static async Task HealthHistoryRangeBoundariesAndEmptyRangeAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsHealthBounds_{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        await using var store = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        await store.InitializeAsync();

        const string devId = "0764:0601:TEST_HEALTH_BOUNDS";
        var from = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        var to = from.AddMinutes(10);
        var fromMs = from.ToUnixTimeMilliseconds();
        var toMs = to.ToUnixTimeMilliseconds();

        await using (var conn = new SqliteConnection($"Data Source={store.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO battery_health_observations (
                    device_id, timestamp_utc_ms, health_percent, relative_performance_percent,
                    status, method, confidence, anchor_source, vendor_category, replacement_status)
                VALUES ($d, $t, $h, $r, 1, 7, 2, 'BHI', 4, 1);
                """;
            var pDev = cmd.Parameters.Add("$d", SqliteType.Text);
            var pTime = cmd.Parameters.Add("$t", SqliteType.Integer);
            var pHealth = cmd.Parameters.Add("$h", SqliteType.Real);
            var pRel = cmd.Parameters.Add("$r", SqliteType.Real);
            pDev.Value = devId;

            async Task Insert(long ms, double h, double r)
            {
                pTime.Value = ms;
                pHealth.Value = h;
                pRel.Value = r;
                await cmd.ExecuteNonQueryAsync();
            }

            // Outside range (before from and after to)
            await Insert(fromMs - 1, 10.0, 10.0);
            await Insert(toMs + 1, 99.0, 99.0);

            // Exact boundaries (inclusive)
            await Insert(fromMs, 80.0, 90.0);
            await Insert(toMs, 70.0, 85.0);

            await tx.CommitAsync();
        }

        // 1. Exact path boundary check
        var exact = await store.QueryHistoryAsync(devId, from, to, [], maximumPoints: 60);
        Equal(2, exact.BatteryHealth.Count);
        Equal(from, exact.BatteryHealth[0].Timestamp);
        Near(80.0, exact.BatteryHealth[0].HealthPercent);
        Near(90.0, exact.BatteryHealth[0].RelativePerformancePercent);
        Equal(to, exact.BatteryHealth[1].Timestamp);
        Near(70.0, exact.BatteryHealth[1].HealthPercent);
        Near(85.0, exact.BatteryHealth[1].RelativePerformancePercent);

        // 2. Empty range check
        var empty = await store.QueryHistoryAsync(devId, to.AddMinutes(5), to.AddMinutes(10), [], maximumPoints: 60);
        Equal(0, empty.BatteryHealth.Count);
        Equal(0, empty.BatteryHealthSeries!.HealthPoints.Count);
        Equal(0, empty.BatteryHealthSeries.RelativePerformancePoints.Count);

        // 3. Downsampled path boundary check (> 60 points inside [from, to])
        await using (var conn = new SqliteConnection($"Data Source={store.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO battery_health_observations (
                    device_id, timestamp_utc_ms, health_percent, relative_performance_percent,
                    status, method, confidence, anchor_source, vendor_category, replacement_status)
                VALUES ($d, $t, $h, $r, 1, 7, 2, 'BHI', 4, 1);
                """;
            var pDev = cmd.Parameters.Add("$d", SqliteType.Text);
            var pTime = cmd.Parameters.Add("$t", SqliteType.Integer);
            var pHealth = cmd.Parameters.Add("$h", SqliteType.Real);
            var pRel = cmd.Parameters.Add("$r", SqliteType.Real);
            pDev.Value = devId;

            for (var i = 1; i <= 100; i++)
            {
                pTime.Value = fromMs + (i * 5000L);
                pHealth.Value = 75.0;
                pRel.Value = 88.0;
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        var downsampled = await store.QueryHistoryAsync(devId, from, to, [], maximumPoints: 60);
        Equal(0, downsampled.BatteryHealth.Count);
        Equal(true, downsampled.BatteryHealthSeries!.HealthPoints.Count is > 0 and <= 60);
        Equal(from, downsampled.BatteryHealthSeries.HealthPoints[0].Timestamp);
        Equal(true, downsampled.BatteryHealthSeries.HealthPoints[^1].Timestamp <= to);
        // Values outside [from, to] (10.0 and 99.0) must not leak into any bucket
        Equal(false, downsampled.BatteryHealthSeries.HealthPoints.Any(p => p.Minimum <= 10.0 || p.Maximum >= 99.0));
        Near(80.0, downsampled.BatteryHealthSeries.HealthPoints[0].Maximum);
        Near(70.0, downsampled.BatteryHealthSeries.HealthPoints[^1].Minimum);
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

static void HealthHistoryBucketRepresentativeValues() =>
    HealthHistoryBucketRepresentativeValuesAsync().GetAwaiter().GetResult();

static async Task HealthHistoryBucketRepresentativeValuesAsync()
{
    var testRoot = Path.Combine(Path.GetTempPath(), $"UpsHealthRep_{Guid.NewGuid():N}");
    Directory.CreateDirectory(testRoot);
    try
    {
        var paths = new AppPaths(testRoot);
        await using var store = new SqliteTelemetryStore(paths, new HistoryConfiguration());
        await store.InitializeAsync();

        const string devId = "0764:0601:TEST_HEALTH_REP";
        var epochAlignedBase = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var baseMs = epochAlignedBase.ToUnixTimeMilliseconds();

        // Use unaligned from = base + 50ms, to = from + 58_000ms.
        // duration = 58_000 ms, maximumPoints = 60 -> minBucket = ceil(58_000 / 59) = 984 ms -> NiceBucketSize(984) = 1000 ms.
        var from = epochAlignedBase.AddMilliseconds(50);
        var to = from.AddMilliseconds(58_000);

        await using (var conn = new SqliteConnection($"Data Source={store.DatabasePath}"))
        {
            await conn.OpenAsync();
            await using var tx = conn.BeginTransaction();
            await using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO battery_health_observations (
                    device_id, timestamp_utc_ms, health_percent, relative_performance_percent,
                    status, method, confidence, anchor_source, vendor_category, replacement_status)
                VALUES ($d, $t, $h, $r, 1, 7, 2, 'BHI', 4, 1);
                """;
            var pDev = cmd.Parameters.Add("$d", SqliteType.Text);
            var pTime = cmd.Parameters.Add("$t", SqliteType.Integer);
            var pHealth = cmd.Parameters.Add("$h", SqliteType.Real);
            var pRel = cmd.Parameters.Add("$r", SqliteType.Real);
            pDev.Value = devId;

            // Bucket 0 ([baseMs, baseMs + 999]): 3 points -> health: 50, 56, 62 (min=50, avg=56, max=62), relative: 80, null, 100 (min=80, avg=90, max=100)
            pTime.Value = baseMs + 100; pHealth.Value = 50.0; pRel.Value = 80.0; await cmd.ExecuteNonQueryAsync();
            pTime.Value = baseMs + 400; pHealth.Value = 56.0; pRel.Value = DBNull.Value; await cmd.ExecuteNonQueryAsync();
            pTime.Value = baseMs + 800; pHealth.Value = 62.0; pRel.Value = 100.0; await cmd.ExecuteNonQueryAsync();

            // Bucket 1 ([baseMs + 1000, baseMs + 1999]): health null, relative only (70, 74 -> min=70, avg=72, max=74)
            pTime.Value = baseMs + 1100; pHealth.Value = DBNull.Value; pRel.Value = 70.0; await cmd.ExecuteNonQueryAsync();
            pTime.Value = baseMs + 1500; pHealth.Value = DBNull.Value; pRel.Value = 74.0; await cmd.ExecuteNonQueryAsync();

            // Add 56 more points in buckets 2..57 so total rows = 61 (> maximumPoints = 60)
            for (var b = 2; b < 58; b++)
            {
                pTime.Value = baseMs + (b * 1000L) + 200;
                pHealth.Value = 60.0;
                pRel.Value = 90.0;
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }

        var result = await store.QueryHistoryAsync(devId, from, to, [], maximumPoints: 60);
        Equal(0, result.BatteryHealth.Count);
        // Bucket 1 had no non-null health_percent, so HealthPoints has 57 points while RelativePerformancePoints has 58.
        Equal(57, result.BatteryHealthSeries!.HealthPoints.Count);
        Equal(58, result.BatteryHealthSeries.RelativePerformancePoints.Count);

        // Bucket timestamps are epoch-aligned (baseMs, baseMs + 1000, ...), not shifted by from's +50ms offset.
        var h0 = result.BatteryHealthSeries.HealthPoints[0];
        Equal(epochAlignedBase, h0.Timestamp);
        Near(50.0, h0.Minimum);
        Near(56.0, h0.Average);
        Near(62.0, h0.Maximum);
        Near(56.0, h0.Last);

        var r0 = result.BatteryHealthSeries.RelativePerformancePoints[0];
        Equal(epochAlignedBase, r0.Timestamp);
        Near(80.0, r0.Minimum);
        Near(90.0, r0.Average);
        Near(100.0, r0.Maximum);
        Near(90.0, r0.Last);

        var r1 = result.BatteryHealthSeries.RelativePerformancePoints[1];
        Equal(epochAlignedBase.AddSeconds(1), r1.Timestamp);
        Near(70.0, r1.Minimum);
        Near(72.0, r1.Average);
        Near(74.0, r1.Maximum);
        Near(72.0, r1.Last);

        // Shifting from/to by +20ms within the same range keeps the epoch-aligned bucket timestamps and averages identical.
        var shifted = await store.QueryHistoryAsync(devId, from.AddMilliseconds(20), to.AddMilliseconds(20), [], maximumPoints: 60);
        Equal(h0, shifted.BatteryHealthSeries!.HealthPoints[0]);
        Equal(r0, shifted.BatteryHealthSeries.RelativePerformancePoints[0]);
        Equal(r1, shifted.BatteryHealthSeries.RelativePerformancePoints[1]);
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

sealed class NavigationTestRouter
{
    public const int DashboardIndex = 0;
    public const int HistoryIndex = 1;
    public const int UpsIndex = 2;
    public const int AnalyticsIndex = 3;
    public const int DevicesIndex = 4;
    public const int ActionsIndex = 5;
    public const int LogsIndex = 6;
    public const int SettingsIndex = 7;

    public static bool IsHistoryRefreshTarget(int index) => index is DashboardIndex or HistoryIndex;
    public static bool IsAnalyticsRefreshTarget(int index) => index is AnalyticsIndex;
}

sealed class NavigationSessionSimulator
{
    public int HistoryRefreshCount { get; private set; }
    public int AnalyticsRefreshCount { get; private set; }
    public int CancelledAnalyticsCount { get; private set; }

    private bool _isWindowVisible;
    private int _selectedNavigationIndex;
    private bool _hasSnapshot;
    private DateTimeOffset _currentTime = new(2026, 8, 29, 12, 0, 0, TimeSpan.Zero);
    private DateTimeOffset _lastHistoryRefresh = DateTimeOffset.MinValue;
    private DateTimeOffset _lastAnalyticsRefresh = DateTimeOffset.MinValue;
    private readonly TimeSpan _debounce = TimeSpan.FromSeconds(5);
    private CancellationTokenSource? _analyticsCts;

    public void AdvanceTime(TimeSpan delta) => _currentTime += delta;

    public void ResetCounts()
    {
        HistoryRefreshCount = 0;
        AnalyticsRefreshCount = 0;
        CancelledAnalyticsCount = 0;
    }

    public bool IsWindowVisible
    {
        get => _isWindowVisible;
        set
        {
            if (_isWindowVisible != value)
            {
                _isWindowVisible = value;
                if (value)
                {
                    if (NavigationTestRouter.IsHistoryRefreshTarget(_selectedNavigationIndex) && _hasSnapshot && _currentTime - _lastHistoryRefresh >= _debounce)
                    {
                        TriggerHistoryRefresh();
                    }
                    else if (NavigationTestRouter.IsAnalyticsRefreshTarget(_selectedNavigationIndex) && _hasSnapshot && _currentTime - _lastAnalyticsRefresh >= _debounce)
                    {
                        TriggerAnalyticsRefresh();
                    }
                }
            }
        }
    }

    public int SelectedNavigationIndex
    {
        get => _selectedNavigationIndex;
        set
        {
            if (_selectedNavigationIndex != value)
            {
                _selectedNavigationIndex = value;
                if (NavigationTestRouter.IsHistoryRefreshTarget(value) && _isWindowVisible && _hasSnapshot && _currentTime - _lastHistoryRefresh >= _debounce)
                {
                    TriggerHistoryRefresh();
                }
                else if (NavigationTestRouter.IsAnalyticsRefreshTarget(value) && _isWindowVisible && _hasSnapshot && _currentTime - _lastAnalyticsRefresh >= _debounce)
                {
                    TriggerAnalyticsRefresh();
                }
            }
        }
    }

    public void ReceiveSnapshot()
    {
        _hasSnapshot = true;
    }

    public void ChangeLanguage()
    {
        if (_hasSnapshot && NavigationTestRouter.IsHistoryRefreshTarget(_selectedNavigationIndex) && _isWindowVisible)
        {
            TriggerHistoryRefresh();
        }
        else if (_hasSnapshot && NavigationTestRouter.IsAnalyticsRefreshTarget(_selectedNavigationIndex) && _isWindowVisible)
        {
            TriggerAnalyticsRefresh();
        }
    }

    public void CompleteAnalyticsRefresh()
    {
        _analyticsCts = null;
    }

    public void SimulateRapidAnalyticsRefresh()
    {
        TriggerAnalyticsRefresh();
    }

    private void TriggerHistoryRefresh()
    {
        HistoryRefreshCount++;
        _lastHistoryRefresh = _currentTime;
    }

    private void TriggerAnalyticsRefresh()
    {
        var prev = _analyticsCts;
        _analyticsCts = new CancellationTokenSource();
        if (prev != null)
        {
            CancelledAnalyticsCount++;
            prev.Cancel();
            prev.Dispose();
        }
        AnalyticsRefreshCount++;
        _lastAnalyticsRefresh = _currentTime;
    }
}

sealed class TestAlertUpsProvider : IUpsProvider
{
    public UpsDeviceInfo? Device { get; set; } = new UpsDeviceInfo("Path", 0x1234, 0x5678, "Mock", "Model", "SN123", 0x84, 0x04, 64, 64);
    public UpsSnapshot CurrentSnapshot { get; set; } = new UpsSnapshot { IsConnected = true, Timestamp = DateTimeOffset.Now };
    public Task<bool> ConnectAsync(CancellationToken cancellationToken) => Task.FromResult(true);
    public Task<UpsSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken) => Task.FromResult(CurrentSnapshot);
    public void Disconnect() { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

sealed class FailingOfficialContextSource(IOfficialHealthContextSource inner) : IOfficialHealthContextSource
{
    public bool Fail { get; set; }
    public int Calls { get; private set; }

    public Task<OfficialHealthContext> QueryOfficialHealthContextAsync(
        string deviceId, DateTimeOffset at, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Fail
            ? Task.FromException<OfficialHealthContext>(new IOException("Synthetic temporary history failure"))
            : inner.QueryOfficialHealthContextAsync(deviceId, at, cancellationToken);
    }
}

sealed class RecordingEventSink(List<UpsEvent> list) : IUpsEventSink
{
    public Task WriteAsync(UpsEvent upsEvent, CancellationToken cancellationToken)
    {
        lock (list)
        {
            list.Add(upsEvent);
        }
        return Task.CompletedTask;
    }
}
