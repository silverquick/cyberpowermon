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

public enum WeeklyPatternMetric
{
    ActivePower,
    InputVoltage,
    LoadPercent,
    PowerFactor,
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

/// <summary>
/// 健全性グラフ（公式値／相対性能）の描画用データ系列。
/// 件数が上限以下のときは生の観測点、上限を超えるときはバケットごとの最小・平均・最大を表す。
/// </summary>
public sealed record BatteryHealthHistory(
    IReadOnlyList<TelemetryHistoryPoint> HealthPoints,
    IReadOnlyList<TelemetryHistoryPoint> RelativePerformancePoints)
{
    public static BatteryHealthHistory Empty { get; } = new([], []);
}

public sealed record TelemetryHistoryResult
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required IReadOnlyDictionary<TelemetryMetric, TelemetryMetricHistory> Metrics { get; init; }
    public required IReadOnlyList<UpsEvent> Events { get; init; }
    public required IReadOnlyList<UpsStateChange> StateChanges { get; init; }

    /// <summary>
    /// 期間内の健全性観測一覧。観測件数が <c>maximumPoints</c> 以下のときだけ生の観測行を返し、
    /// 上限を超えて間引いたときは空リストになる（グラフの描画には <see cref="BatteryHealthSeries"/> を使う）。
    /// </summary>
    public required IReadOnlyList<BatteryHealthObservation> BatteryHealth { get; init; }

    /// <summary>
    /// 健全性グラフ描画用のデータ系列（<see cref="BatteryHealthHistory"/>）。
    /// 上限以下のときは生の観測点、上限を超えるときは紀元揃えのバケットで間引いた点が入る。
    /// </summary>
    public BatteryHealthHistory? BatteryHealthSeries { get; init; }

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

public enum EnergyReportPeriod
{
    Day,
    Month,
}

public sealed record EnergyReportItem(
    EnergyReportPeriod Period,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
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

public sealed record HourlyPatternPoint(
    int DayOfWeek,
    int HourOfDay,
    double Average,
    double Minimum,
    double Maximum,
    long SampleCount);

public sealed record WeeklyPatternResult
{
    public required WeeklyPatternMetric Metric { get; init; }
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required double OverallMin { get; init; }
    public required double OverallMax { get; init; }
    public required double OverallAvg { get; init; }
    public required IReadOnlyList<HourlyPatternPoint> Grid { get; init; }
    public HourlyPatternPoint? PeakHour { get; init; }
    public HourlyPatternPoint? LowestHour { get; init; }
    public required long TotalSamples { get; init; }
    // Each sample represents one populated minute bucket, not elapsed calendar days.
    public double EquivalentDays => TotalSamples / 1440.0;
}

