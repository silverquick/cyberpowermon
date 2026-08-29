namespace UpsMonitor.Core;

public enum TelemetryMetric
{
    InputVoltage,
    OutputVoltage,
    BatteryVoltage,
    BatteryPercent,
    RuntimeMinutes,
    LoadPercent,
    ActivePowerWatts,
    ApparentPowerVoltAmperes,
    FrequencyHertz,
    TemperatureCelsius,
}

public sealed record TelemetryHistoryPoint(
    DateTimeOffset Timestamp,
    double Minimum,
    double Average,
    double Maximum,
    double Last);

public sealed record TelemetryMetricHistory(
    TelemetryMetric Metric,
    IReadOnlyList<TelemetryHistoryPoint> Points);

public sealed record UpsStateChange(
    DateTimeOffset Timestamp,
    UpsPowerState State);

public sealed record BatteryHealthObservation
{
    public required string DeviceId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public double? HealthPercent { get; init; }
    public double? RelativePerformancePercent { get; init; }
    public required BatteryHealthStatus Status { get; init; }
    public required BatteryHealthMethod Method { get; init; }
    public required BatteryHealthConfidence Confidence { get; init; }
    public string? AnchorSource { get; init; }
    public required VendorBatteryHealthCategory VendorCategory { get; init; }
    public required BatteryReplacementStatus ReplacementStatus { get; init; }
}

public sealed record TelemetryPeriodSummary
{
    public int OutageCount { get; init; }
    public TimeSpan TotalOutageDuration { get; init; }
    public double? MinInputVoltage { get; init; }
    public double? AvgInputVoltage { get; init; }
    public double? MaxInputVoltage { get; init; }
    public double? MinOutputVoltage { get; init; }
    public double? AvgOutputVoltage { get; init; }
    public double? MaxOutputVoltage { get; init; }
    public double? PeakLoadPercent { get; init; }
    public double? AvgLoadPercent { get; init; }
    public double? PeakActivePowerWatts { get; init; }
    public double? AvgActivePowerWatts { get; init; }
    public double? TotalEnergyKwh { get; init; }
    public double? MinBatteryPercent { get; init; }
    public double? AvgBatteryVoltage { get; init; }
    public double? AvgFrequencyHertz { get; init; }
    public double? AvgTemperatureCelsius { get; init; }
    public double? AvgPowerFactor { get; init; }
}

public sealed record TelemetryHistoryResult
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required IReadOnlyDictionary<TelemetryMetric, TelemetryMetricHistory> Metrics { get; init; }
    public required IReadOnlyList<UpsEvent> Events { get; init; }
    public required IReadOnlyList<UpsStateChange> StateChanges { get; init; }
    public required IReadOnlyList<BatteryHealthObservation> BatteryHealth { get; init; }
    public required long SourceSampleCount { get; init; }
    public TelemetryPeriodSummary? Summary { get; init; }
}

public sealed record TelemetryDatabaseStatistics
{
    public required long SampleCount { get; init; }
    public required long RawValueCount { get; init; }
    public required long EventCount { get; init; }
    public DateTimeOffset? FirstSample { get; init; }
    public DateTimeOffset? LastSample { get; init; }
}

public sealed record DailyEnergyReportItem(
    DateOnly Date,
    double EnergyKwh,
    double EstimatedCost,
    double PeakWatts,
    double AvgWatts,
    int OutageCount);

public sealed record PowerTroubleSummary
{
    public int TotalOutages { get; init; }
    public TimeSpan TotalOutageDuration { get; init; }
    public int VoltageSagCount { get; init; }
    public int VoltageSurgeCount { get; init; }
    public required IReadOnlyList<UpsEvent> TroubleEvents { get; init; }
}

public static class UpsDeviceIdentity
{
    public static string Create(UpsDeviceInfo device) =>
        $"{device.VendorId:X4}:{device.ProductId:X4}:{(string.IsNullOrWhiteSpace(device.SerialNumber) ? "N/A" : device.SerialNumber)}";
}
