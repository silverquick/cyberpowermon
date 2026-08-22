using UpsMonitor.Core;

namespace UpsMonitor.App;

public sealed record HistoryChartSeries(
    string DisplayName,
    string Color,
    IReadOnlyList<TelemetryHistoryPoint> Points);

public sealed record HistoryChartReferenceLine(
    string DisplayName,
    string Color,
    double Value);

public sealed record HistoryEventMarker(
    DateTimeOffset Timestamp,
    string DisplayName,
    string Color);

public sealed record HistoryChartData
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required IReadOnlyList<HistoryChartSeries> Series { get; init; }
    public IReadOnlyList<HistoryChartReferenceLine> ReferenceLines { get; init; } = [];
    public IReadOnlyList<HistoryEventMarker> Events { get; init; } = [];
}

public sealed record HistoryStateTimelineData
{
    public required DateTimeOffset From { get; init; }
    public required DateTimeOffset To { get; init; }
    public required IReadOnlyList<UpsStateChange> StateChanges { get; init; }
    public IReadOnlyList<HistoryEventMarker> Events { get; init; } = [];
}

public sealed record HistoryRangeOption(
    string Key,
    TimeSpan Duration,
    string DisplayName)
{
    public override string ToString() => DisplayName;
}
