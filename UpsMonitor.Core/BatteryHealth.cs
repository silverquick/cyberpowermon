namespace UpsMonitor.Core;

public enum TelemetryQuality
{
    Valid,
    Missing,
    Invalid,
}

public enum TelemetryValidationIssueCode
{
    Missing,
    NotFinite,
    OutOfRange,
    PercentageScaleIsNotPhysicalCapacity,
    CapacityUnitMismatch,
}

public readonly record struct ValidatedTelemetryValue<T>(
    T? Value,
    TelemetryQuality Quality,
    TelemetryValidationIssueCode? Issue = null)
    where T : struct
{
    public bool IsValid => Quality == TelemetryQuality.Valid && Value.HasValue;

    public static ValidatedTelemetryValue<T> Valid(T value) =>
        new(value, TelemetryQuality.Valid);

    public static ValidatedTelemetryValue<T> Missing() =>
        new(null, TelemetryQuality.Missing, TelemetryValidationIssueCode.Missing);

    public static ValidatedTelemetryValue<T> Invalid(T? value, TelemetryValidationIssueCode issue) =>
        new(value, TelemetryQuality.Invalid, issue);
}

public sealed record TelemetryValidationIssue(
    string Field,
    TelemetryValidationIssueCode Code,
    string? ReportedValue);

public enum UpsSelfTestResult
{
    Unknown,
    NotRun,
    InProgress,
    Passed,
    Warning,
    Failed,
    Aborted,
}

public sealed record UpsTelemetry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required bool IsConnected { get; init; }
    public required ValidatedTelemetryValue<double> BatteryChargePercent { get; init; }
    public required ValidatedTelemetryValue<double> BatteryVoltage { get; init; }
    public required ValidatedTelemetryValue<double> NominalBatteryVoltage { get; init; }
    public required ValidatedTelemetryValue<TimeSpan> RuntimeRemaining { get; init; }
    public required ValidatedTelemetryValue<double> LoadPercent { get; init; }
    public required ValidatedTelemetryValue<double> ActivePowerWatts { get; init; }
    public required ValidatedTelemetryValue<double> DesignCapacity { get; init; }
    public required ValidatedTelemetryValue<double> FullChargeCapacity { get; init; }
    public required ValidatedTelemetryValue<double> CycleCount { get; init; }
    public required ValidatedTelemetryValue<bool> FullyCharged { get; init; }
    public required ValidatedTelemetryValue<bool> NeedReplacement { get; init; }
    public required UpsSelfTestResult SelfTest { get; init; }
    public required IReadOnlyList<TelemetryValidationIssue> Issues { get; init; }
}

public static class UpsTelemetryValidator
{
    private const ushort BatterySystemPage = 0x85;

    public static UpsTelemetry Normalize(UpsSnapshot snapshot)
    {
        var issues = new List<TelemetryValidationIssue>();
        var designCapacity = ValidatePhysicalCapacity(
            nameof(UpsSnapshot.DesignCapacity),
            snapshot.DesignCapacity,
            FindTelemetry(snapshot, BatterySystemPage, 0x83),
            issues);
        var fullChargeCapacity = ValidatePhysicalCapacity(
            nameof(UpsSnapshot.FullChargeCapacity),
            snapshot.FullChargeCapacity,
            FindTelemetry(snapshot, BatterySystemPage, 0x67),
            issues);

        if (designCapacity.IsValid
            && fullChargeCapacity.IsValid
            && !CapacityUnitsMatch(
                FindTelemetry(snapshot, BatterySystemPage, 0x83),
                FindTelemetry(snapshot, BatterySystemPage, 0x67)))
        {
            designCapacity = ValidatedTelemetryValue<double>.Invalid(
                designCapacity.Value,
                TelemetryValidationIssueCode.CapacityUnitMismatch);
            fullChargeCapacity = ValidatedTelemetryValue<double>.Invalid(
                fullChargeCapacity.Value,
                TelemetryValidationIssueCode.CapacityUnitMismatch);
            issues.Add(new(
                "Capacity",
                TelemetryValidationIssueCode.CapacityUnitMismatch,
                $"{snapshot.FullChargeCapacity}/{snapshot.DesignCapacity}"));
        }

        return new UpsTelemetry
        {
            Timestamp = snapshot.Timestamp,
            IsConnected = snapshot.IsConnected,
            BatteryChargePercent = ValidateDouble(
                nameof(UpsSnapshot.BatteryPercent), snapshot.BatteryPercent, 0, 100, issues),
            BatteryVoltage = ValidateDouble(
                nameof(UpsSnapshot.BatteryVoltage), snapshot.BatteryVoltage, 1, 1_000, issues),
            NominalBatteryVoltage = ValidateDouble(
                nameof(UpsSnapshot.NominalBatteryVoltage), snapshot.NominalBatteryVoltage, 1, 1_000, issues),
            RuntimeRemaining = ValidateDuration(
                nameof(UpsSnapshot.RuntimeRemaining), snapshot.RuntimeRemaining, TimeSpan.FromDays(7), issues),
            LoadPercent = ValidateDouble(
                nameof(UpsSnapshot.PercentLoad), snapshot.PercentLoad, 0, 100, issues),
            ActivePowerWatts = ValidateDouble(
                nameof(UpsSnapshot.ActivePower), snapshot.ActivePower, 0, 1_000_000, issues),
            DesignCapacity = designCapacity,
            FullChargeCapacity = fullChargeCapacity,
            CycleCount = ValidateDouble(
                nameof(UpsSnapshot.CycleCount), snapshot.CycleCount, 0, 1_000_000, issues),
            FullyCharged = ValidateBoolean(snapshot.FullyCharged),
            NeedReplacement = ValidateBoolean(snapshot.NeedReplacement),
            SelfTest = ParseSelfTest(snapshot.SelfTestState),
            Issues = issues,
        };
    }

    private static ValidatedTelemetryValue<double> ValidateDouble(
        string field,
        double? value,
        double minimum,
        double maximum,
        ICollection<TelemetryValidationIssue> issues)
    {
        if (value is null)
        {
            return ValidatedTelemetryValue<double>.Missing();
        }

        if (!double.IsFinite(value.Value))
        {
            issues.Add(new(field, TelemetryValidationIssueCode.NotFinite, value.ToString()));
            return ValidatedTelemetryValue<double>.Invalid(value, TelemetryValidationIssueCode.NotFinite);
        }

        if (value < minimum || value > maximum)
        {
            issues.Add(new(field, TelemetryValidationIssueCode.OutOfRange, value.ToString()));
            return ValidatedTelemetryValue<double>.Invalid(value, TelemetryValidationIssueCode.OutOfRange);
        }

        return ValidatedTelemetryValue<double>.Valid(value.Value);
    }

    private static ValidatedTelemetryValue<TimeSpan> ValidateDuration(
        string field,
        TimeSpan? value,
        TimeSpan maximum,
        ICollection<TelemetryValidationIssue> issues)
    {
        if (value is null)
        {
            return ValidatedTelemetryValue<TimeSpan>.Missing();
        }

        if (value < TimeSpan.Zero || value > maximum)
        {
            issues.Add(new(field, TelemetryValidationIssueCode.OutOfRange, value.ToString()));
            return ValidatedTelemetryValue<TimeSpan>.Invalid(value, TelemetryValidationIssueCode.OutOfRange);
        }

        return ValidatedTelemetryValue<TimeSpan>.Valid(value.Value);
    }

    private static ValidatedTelemetryValue<bool> ValidateBoolean(bool? value) => value is { } boolean
        ? ValidatedTelemetryValue<bool>.Valid(boolean)
        : ValidatedTelemetryValue<bool>.Missing();

    private static ValidatedTelemetryValue<double> ValidatePhysicalCapacity(
        string field,
        double? value,
        UpsTelemetryItem? item,
        ICollection<TelemetryValidationIssue> issues)
    {
        var validated = ValidateDouble(field, value, double.Epsilon, double.MaxValue, issues);
        if (!validated.IsValid)
        {
            return validated;
        }

        if (item is null
            || item.HidUnit == 0
            || string.Equals(item.UnitSymbol, "%", StringComparison.Ordinal))
        {
            issues.Add(new(
                field,
                TelemetryValidationIssueCode.PercentageScaleIsNotPhysicalCapacity,
                value?.ToString()));
            return ValidatedTelemetryValue<double>.Invalid(
                value,
                TelemetryValidationIssueCode.PercentageScaleIsNotPhysicalCapacity);
        }

        return validated;
    }

    private static UpsTelemetryItem? FindTelemetry(UpsSnapshot snapshot, ushort page, ushort usage) =>
        snapshot.Telemetry
            .Where(item => item.UsagePage == page && item.Usage == usage && item.HasValue)
            .OrderBy(item => item.ReportType == "Input" ? 0 : 1)
            .ThenBy(item => item.ReportId)
            .FirstOrDefault();

    private static bool CapacityUnitsMatch(UpsTelemetryItem? design, UpsTelemetryItem? full) =>
        design is not null
        && full is not null
        && design.HidUnit == full.HidUnit
        && design.UnitExponent == full.UnitExponent;

    private static UpsSelfTestResult ParseSelfTest(string? value) => value switch
    {
        "Done - passed" => UpsSelfTestResult.Passed,
        "Done - warning" => UpsSelfTestResult.Warning,
        "Done - error" => UpsSelfTestResult.Failed,
        "Aborted" => UpsSelfTestResult.Aborted,
        "In progress" => UpsSelfTestResult.InProgress,
        "No test initiated" => UpsSelfTestResult.NotRun,
        _ => UpsSelfTestResult.Unknown,
    };
}

public enum BatteryHealthStatus
{
    Unknown,
    VendorReported,
    Excellent,
    Good,
    Fair,
    Degraded,
    Poor,
    Critical,
}

public enum BatteryHealthConfidence
{
    Unknown,
    Low,
    Medium,
    High,
}

public enum BatteryHealthMethod
{
    None,
    ControlledRuntimeTest,
    DischargedEnergy,
    CapacityRatio,
    RuntimeBaseline,
    RelativeRuntimeTrend,
    VendorAnchoredRuntime,
}

public enum BatteryHealthReasonCode
{
    NotConnected,
    NeedReplacementReported,
    SelfTestFailed,
    SelfTestPassed,
    ControlledMeasurementCompared,
    CapacityCompared,
    RuntimeComparedWithBaseline,
    NewBatteryBaselineApplied,
    CurrentRelativeBaselineApplied,
    KnownHealthAnchorApplied,
    KnownHealthAnchorInvalid,
    MeasurementAboveReference,
    CapacityDataIsPercentageOnly,
    BatteryNotFullyCharged,
    NoComparableRuntimeBaseline,
    BatteryAgeKnown,
    InvalidTelemetryIgnored,
    InsufficientData,
}

public sealed record BatteryHealthReason(
    BatteryHealthReasonCode Code,
    double? Observed = null,
    double? Reference = null);

public sealed record BatteryHealthMeasurement
{
    public required BatteryHealthMethod Method { get; init; }
    public required double MeasuredValue { get; init; }
    public required double BaselineValue { get; init; }
    public required DateTimeOffset MeasuredAt { get; init; }
}

public sealed record BatteryRuntimeBaselinePoint
{
    public required double LoadPercent { get; init; }
    public required TimeSpan Runtime { get; init; }
    public required DateTimeOffset MeasuredAt { get; init; }
}

public enum BatteryRuntimeBaselineKind
{
    Unspecified,
    NewBattery,
    CurrentRelative,
    KnownHealthAnchor,
}

public enum VendorBatteryHealthCategory
{
    Unknown,
    Good,
    Average,
    BelowAverage,
    Poor,
}

public enum BatteryReplacementStatus
{
    Unknown,
    NoSignal,
    CheckRequired,
    ConsiderReplacement,
    ReplacementRequested,
}

public enum BatteryReplacementReasonCode
{
    NeedReplacementReported,
    SelfTestFailed,
    PhysicalCapacityBelowReference,
    ControlledMeasurementBelowReference,
    NewBatteryRuntimeBelowReference,
    RelativeRuntimeDeclined,
}

public sealed record BatteryReplacementReason(
    BatteryReplacementReasonCode Code,
    double? Observed = null,
    double? Reference = null);

public sealed record BatteryReplacementAssessment
{
    public required BatteryReplacementStatus Status { get; init; }
    public required IReadOnlyList<BatteryReplacementReason> Reasons { get; init; }
}

public sealed record BatteryHealthProfile
{
    public required string DeviceId { get; init; }
    public DateTimeOffset? BatteryInstalledAt { get; set; }
    public BatteryRuntimeBaselineKind RuntimeBaselineKind { get; set; }
    public double? AnchorHealthPercent { get; set; }
    public string? AnchorSource { get; set; }
    public VendorBatteryHealthCategory VendorHealthCategory { get; set; }
    public DateTimeOffset? BaselineRecordedAt { get; set; }
    public List<BatteryRuntimeBaselinePoint> RuntimeBaselines { get; init; } = [];
    public List<BatteryHealthMeasurement> ControlledMeasurements { get; init; } = [];
}

public sealed record BatteryHealthOptions
{
    public double WarningThresholdPercent { get; init; } = 70;
    public double CriticalThresholdPercent { get; init; } = 60;
    public double ComparableLoadTolerancePercent { get; init; } = 5;
    public double MaximumInterpolationSpanPercent { get; init; } = 20;
    public double ReplacementPerformanceThresholdPercent { get; init; } = 80;
}

public sealed record BatteryHealthResult
{
    public double? HealthPercent { get; init; }
    public double? RelativePerformancePercent { get; init; }
    public double? AnchorHealthPercent { get; init; }
    public string? AnchorSource { get; init; }
    public VendorBatteryHealthCategory VendorHealthCategory { get; init; }
    public BatteryRuntimeBaselineKind BaselineKind { get; init; }
    public required BatteryHealthStatus Status { get; init; }
    public required BatteryHealthConfidence Confidence { get; init; }
    public required BatteryHealthMethod PrimaryMethod { get; init; }
    public required int EvidenceScore { get; init; }
    public required IReadOnlyList<BatteryHealthReason> Reasons { get; init; }
    public required BatteryReplacementAssessment Replacement { get; init; }
}

public static class BatteryHealthCalculator
{
    public static BatteryHealthResult Calculate(
        UpsTelemetry telemetry,
        BatteryHealthProfile? profile,
        BatteryHealthOptions? options = null)
    {
        options ??= new BatteryHealthOptions();
        ValidateOptions(options);
        var reasons = new List<BatteryHealthReason>();
        var hardFailure = telemetry.NeedReplacement.Value is true
            || telemetry.SelfTest == UpsSelfTestResult.Failed;

        if (!telemetry.IsConnected)
        {
            return Unknown(BatteryHealthReasonCode.NotConnected);
        }

        if (telemetry.NeedReplacement.Value is true)
        {
            reasons.Add(new(BatteryHealthReasonCode.NeedReplacementReported));
        }

        if (telemetry.SelfTest == UpsSelfTestResult.Failed)
        {
            reasons.Add(new(BatteryHealthReasonCode.SelfTestFailed));
        }
        else if (telemetry.SelfTest == UpsSelfTestResult.Passed)
        {
            reasons.Add(new(BatteryHealthReasonCode.SelfTestPassed));
        }

        if (telemetry.Issues.Count > 0)
        {
            reasons.Add(new(BatteryHealthReasonCode.InvalidTelemetryIgnored, telemetry.Issues.Count));
        }

        double? health = null;
        double? relativePerformance = null;
        var method = BatteryHealthMethod.None;
        var evidenceScore = 0;

        var controlled = profile?.ControlledMeasurements
            .Where(IsValidControlledMeasurement)
            .OrderByDescending(item => item.MeasuredAt)
            .FirstOrDefault();
        if (controlled is not null)
        {
            health = CalculateRatio(controlled.MeasuredValue, controlled.BaselineValue, reasons);
            method = controlled.Method;
            evidenceScore = 50;
            reasons.Add(new(
                BatteryHealthReasonCode.ControlledMeasurementCompared,
                controlled.MeasuredValue,
                controlled.BaselineValue));
        }
        else if (telemetry.DesignCapacity.IsValid && telemetry.FullChargeCapacity.IsValid)
        {
            health = CalculateRatio(
                telemetry.FullChargeCapacity.Value!.Value,
                telemetry.DesignCapacity.Value!.Value,
                reasons);
            method = BatteryHealthMethod.CapacityRatio;
            evidenceScore = 50;
            reasons.Add(new(
                BatteryHealthReasonCode.CapacityCompared,
                telemetry.FullChargeCapacity.Value,
                telemetry.DesignCapacity.Value));
        }
        else if (TryCalculateRuntimeRatio(telemetry, profile, options, reasons, out var runtimeRatio))
        {
            relativePerformance = runtimeRatio;
            switch (profile?.RuntimeBaselineKind)
            {
                case BatteryRuntimeBaselineKind.CurrentRelative:
                    method = BatteryHealthMethod.RelativeRuntimeTrend;
                    evidenceScore = 30;
                    reasons.Add(new(BatteryHealthReasonCode.CurrentRelativeBaselineApplied));
                    break;

                case BatteryRuntimeBaselineKind.KnownHealthAnchor
                    when IsValidHealthAnchor(profile.AnchorHealthPercent):
                    health = CalculateAnchoredHealth(profile.AnchorHealthPercent!.Value, runtimeRatio!.Value);
                    method = BatteryHealthMethod.VendorAnchoredRuntime;
                    evidenceScore = 60;
                    reasons.Add(new(
                        BatteryHealthReasonCode.KnownHealthAnchorApplied,
                        profile.AnchorHealthPercent,
                        runtimeRatio));
                    break;

                case BatteryRuntimeBaselineKind.KnownHealthAnchor:
                    method = BatteryHealthMethod.RelativeRuntimeTrend;
                    evidenceScore = 30;
                    reasons.Add(new(BatteryHealthReasonCode.KnownHealthAnchorInvalid));
                    break;

                case BatteryRuntimeBaselineKind.NewBattery:
                    health = runtimeRatio;
                    method = BatteryHealthMethod.RuntimeBaseline;
                    evidenceScore = 30;
                    reasons.Add(new(BatteryHealthReasonCode.NewBatteryBaselineApplied));
                    break;

                default:
                    // Profiles saved before baseline kinds were introduced used the
                    // original absolute-runtime behavior. Keep them compatible.
                    health = runtimeRatio;
                    method = BatteryHealthMethod.RuntimeBaseline;
                    evidenceScore = 30;
                    break;
            }
        }
        else if (telemetry.DesignCapacity.Issue == TelemetryValidationIssueCode.PercentageScaleIsNotPhysicalCapacity
                 || telemetry.FullChargeCapacity.Issue == TelemetryValidationIssueCode.PercentageScaleIsNotPhysicalCapacity)
        {
            reasons.Add(new(BatteryHealthReasonCode.CapacityDataIsPercentageOnly));
        }

        if (telemetry.SelfTest == UpsSelfTestResult.Passed)
        {
            evidenceScore += 10;
        }

        if (profile?.BatteryInstalledAt is not null)
        {
            evidenceScore += 5;
            reasons.Add(new(BatteryHealthReasonCode.BatteryAgeKnown));
        }

        if (health is null)
        {
            reasons.Add(new(BatteryHealthReasonCode.InsufficientData));
        }

        var confidence = hardFailure
            ? BatteryHealthConfidence.High
            : ConfidenceFromScore(evidenceScore, health.HasValue || relativePerformance.HasValue);
        var status = hardFailure
            ? BatteryHealthStatus.Critical
            : method == BatteryHealthMethod.VendorAnchoredRuntime
                ? BatteryHealthStatus.VendorReported
                : StatusFromHealth(health, options);
        var replacement = AssessReplacement(
            telemetry,
            profile,
            method,
            health,
            relativePerformance,
            options);

        return new BatteryHealthResult
        {
            HealthPercent = health,
            RelativePerformancePercent = relativePerformance,
            AnchorHealthPercent = IsValidHealthAnchor(profile?.AnchorHealthPercent)
                ? profile!.AnchorHealthPercent
                : null,
            AnchorSource = profile?.AnchorSource,
            VendorHealthCategory = profile?.VendorHealthCategory ?? VendorBatteryHealthCategory.Unknown,
            BaselineKind = profile?.RuntimeBaselineKind ?? BatteryRuntimeBaselineKind.Unspecified,
            Status = status,
            Confidence = confidence,
            PrimaryMethod = method,
            EvidenceScore = hardFailure ? 100 : evidenceScore,
            Reasons = reasons,
            Replacement = replacement,
        };

        BatteryHealthResult Unknown(BatteryHealthReasonCode code) => new()
        {
            HealthPercent = null,
            RelativePerformancePercent = null,
            AnchorHealthPercent = null,
            AnchorSource = null,
            VendorHealthCategory = VendorBatteryHealthCategory.Unknown,
            BaselineKind = BatteryRuntimeBaselineKind.Unspecified,
            Status = BatteryHealthStatus.Unknown,
            Confidence = BatteryHealthConfidence.Unknown,
            PrimaryMethod = BatteryHealthMethod.None,
            EvidenceScore = 0,
            Reasons = [new(code)],
            Replacement = new BatteryReplacementAssessment
            {
                Status = BatteryReplacementStatus.Unknown,
                Reasons = [],
            },
        };
    }

    private static BatteryReplacementAssessment AssessReplacement(
        UpsTelemetry telemetry,
        BatteryHealthProfile? profile,
        BatteryHealthMethod method,
        double? health,
        double? relativePerformance,
        BatteryHealthOptions options)
    {
        if (telemetry.NeedReplacement.Value is true)
        {
            return Assessment(
                BatteryReplacementStatus.ReplacementRequested,
                new(BatteryReplacementReasonCode.NeedReplacementReported));
        }

        if (telemetry.SelfTest == UpsSelfTestResult.Failed)
        {
            return Assessment(
                BatteryReplacementStatus.CheckRequired,
                new(BatteryReplacementReasonCode.SelfTestFailed));
        }

        if (health is { } measured
            && measured < options.ReplacementPerformanceThresholdPercent)
        {
            var reason = method switch
            {
                BatteryHealthMethod.CapacityRatio => BatteryReplacementReasonCode.PhysicalCapacityBelowReference,
                BatteryHealthMethod.ControlledRuntimeTest or BatteryHealthMethod.DischargedEnergy =>
                    BatteryReplacementReasonCode.ControlledMeasurementBelowReference,
                BatteryHealthMethod.RuntimeBaseline
                    when profile?.RuntimeBaselineKind == BatteryRuntimeBaselineKind.NewBattery =>
                    BatteryReplacementReasonCode.NewBatteryRuntimeBelowReference,
                _ => (BatteryReplacementReasonCode?)null,
            };

            if (reason is { } measuredReason)
            {
                return Assessment(
                    BatteryReplacementStatus.ConsiderReplacement,
                    new(measuredReason, measured, options.ReplacementPerformanceThresholdPercent));
            }
        }

        if (relativePerformance is { } relative
            && relative < options.ReplacementPerformanceThresholdPercent
            && profile?.RuntimeBaselineKind is BatteryRuntimeBaselineKind.CurrentRelative
                or BatteryRuntimeBaselineKind.KnownHealthAnchor)
        {
            return Assessment(
                BatteryReplacementStatus.CheckRequired,
                new(
                    BatteryReplacementReasonCode.RelativeRuntimeDeclined,
                    relative,
                    options.ReplacementPerformanceThresholdPercent));
        }

        return new BatteryReplacementAssessment
        {
            Status = BatteryReplacementStatus.NoSignal,
            Reasons = [],
        };

        static BatteryReplacementAssessment Assessment(
            BatteryReplacementStatus status,
            BatteryReplacementReason reason) => new()
            {
                Status = status,
                Reasons = [reason],
            };
    }

    private static bool TryCalculateRuntimeRatio(
        UpsTelemetry telemetry,
        BatteryHealthProfile? profile,
        BatteryHealthOptions options,
        ICollection<BatteryHealthReason> reasons,
        out double? health)
    {
        health = null;
        if (!telemetry.RuntimeRemaining.IsValid || !telemetry.LoadPercent.IsValid)
        {
            reasons.Add(new(BatteryHealthReasonCode.NoComparableRuntimeBaseline));
            return false;
        }

        var fullyCharged = telemetry.FullyCharged.Value is true
            || telemetry.BatteryChargePercent is { IsValid: true, Value: >= 95 };
        if (!fullyCharged)
        {
            reasons.Add(new(BatteryHealthReasonCode.BatteryNotFullyCharged));
            return false;
        }

        var load = telemetry.LoadPercent.Value!.Value;
        var expected = ExpectedRuntime(profile?.RuntimeBaselines, load, options);
        if (expected is null)
        {
            reasons.Add(new(BatteryHealthReasonCode.NoComparableRuntimeBaseline, load));
            return false;
        }

        var observedSeconds = telemetry.RuntimeRemaining.Value!.Value.TotalSeconds;
        var expectedSeconds = expected.Value.TotalSeconds;
        health = CalculateRatio(observedSeconds, expectedSeconds, reasons);
        reasons.Add(new(
            BatteryHealthReasonCode.RuntimeComparedWithBaseline,
            observedSeconds,
            expectedSeconds));
        return health.HasValue;
    }

    private static double CalculateAnchoredHealth(double anchorHealth, double relativePerformance) =>
        Math.Clamp(anchorHealth * relativePerformance / 100, 0, 100);

    private static bool IsValidHealthAnchor(double? value) =>
        value is > 0 and <= 100 && double.IsFinite(value.Value);

    private static TimeSpan? ExpectedRuntime(
        IReadOnlyList<BatteryRuntimeBaselinePoint>? baselines,
        double load,
        BatteryHealthOptions options)
    {
        if (baselines is null || baselines.Count == 0)
        {
            return null;
        }

        var points = baselines
            .Where(item => item.LoadPercent is >= 0 and <= 100 && item.Runtime > TimeSpan.Zero)
            .OrderBy(item => item.LoadPercent)
            .ToArray();
        var nearest = points.MinBy(item => Math.Abs(item.LoadPercent - load));
        if (nearest is not null
            && Math.Abs(nearest.LoadPercent - load) <= options.ComparableLoadTolerancePercent)
        {
            return nearest.Runtime;
        }

        var lower = points.LastOrDefault(item => item.LoadPercent < load);
        var upper = points.FirstOrDefault(item => item.LoadPercent > load);
        if (lower is null
            || upper is null
            || upper.LoadPercent - lower.LoadPercent > options.MaximumInterpolationSpanPercent)
        {
            return null;
        }

        var fraction = (load - lower.LoadPercent) / (upper.LoadPercent - lower.LoadPercent);
        var seconds = lower.Runtime.TotalSeconds
            + ((upper.Runtime.TotalSeconds - lower.Runtime.TotalSeconds) * fraction);
        return seconds > 0 ? TimeSpan.FromSeconds(seconds) : null;
    }

    private static bool IsValidControlledMeasurement(BatteryHealthMeasurement measurement) =>
        measurement.Method is BatteryHealthMethod.ControlledRuntimeTest or BatteryHealthMethod.DischargedEnergy
        && double.IsFinite(measurement.MeasuredValue)
        && double.IsFinite(measurement.BaselineValue)
        && measurement.MeasuredValue > 0
        && measurement.BaselineValue > 0;

    private static double? CalculateRatio(
        double observed,
        double reference,
        ICollection<BatteryHealthReason> reasons)
    {
        if (!double.IsFinite(observed)
            || !double.IsFinite(reference)
            || observed <= 0
            || reference <= 0)
        {
            return null;
        }

        var percent = observed / reference * 100;
        if (!double.IsFinite(percent) || percent is <= 0 or > 200)
        {
            return null;
        }

        if (percent > 100)
        {
            reasons.Add(new(BatteryHealthReasonCode.MeasurementAboveReference, percent, 100));
            return 100;
        }

        return percent;
    }

    private static BatteryHealthStatus StatusFromHealth(double? health, BatteryHealthOptions options)
    {
        if (health is null)
        {
            return BatteryHealthStatus.Unknown;
        }

        if (health >= 90)
        {
            return BatteryHealthStatus.Excellent;
        }

        if (health >= 80)
        {
            return BatteryHealthStatus.Good;
        }

        if (health >= options.WarningThresholdPercent)
        {
            return BatteryHealthStatus.Fair;
        }

        return health >= options.CriticalThresholdPercent
            ? BatteryHealthStatus.Degraded
            : BatteryHealthStatus.Poor;
    }

    private static BatteryHealthConfidence ConfidenceFromScore(int score, bool hasHealth) => !hasHealth
        ? BatteryHealthConfidence.Unknown
        : score switch
        {
            >= 70 => BatteryHealthConfidence.High,
            >= 40 => BatteryHealthConfidence.Medium,
            _ => BatteryHealthConfidence.Low,
        };

    private static void ValidateOptions(BatteryHealthOptions options)
    {
        if (options.CriticalThresholdPercent is < 1 or >= 100
            || options.WarningThresholdPercent <= options.CriticalThresholdPercent
            || options.WarningThresholdPercent > 100
            || options.ComparableLoadTolerancePercent is < 0 or > 25
            || options.MaximumInterpolationSpanPercent is <= 0 or > 100
            || options.ReplacementPerformanceThresholdPercent is <= 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }
}
