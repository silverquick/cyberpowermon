using System.Collections.Frozen;

namespace UpsMonitor.Core;

/// <summary>Saved-event search. From is inclusive, To exclusive; null bounds are unbounded.
/// A null DeviceId searches all devices, and an empty type set matches no events.</summary>
public sealed record EventQuery
{
    public string? DeviceId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public IReadOnlySet<UpsEventType>? EventTypes { get; init; }
    public UpsEventSeverity? Severity { get; init; }
    public int Limit { get; init; } = 500;

    public EventQuery Snapshot()
    {
        if (Limit is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(Limit));
        if (From.HasValue && To.HasValue && From >= To) throw new ArgumentOutOfRangeException(nameof(To));
        if (Severity.HasValue && !Enum.IsDefined(Severity.Value)) throw new ArgumentOutOfRangeException(nameof(Severity));
        if (DeviceId is not null) ArgumentException.ThrowIfNullOrWhiteSpace(DeviceId);
        return this with { EventTypes = EventTypes?.ToFrozenSet() };
    }

    public bool Matches(UpsEvent value) =>
        (!From.HasValue || value.Timestamp.ToUnixTimeMilliseconds() >= From.Value.ToUnixTimeMilliseconds()) &&
        (!To.HasValue || value.Timestamp.ToUnixTimeMilliseconds() < To.Value.ToUnixTimeMilliseconds()) &&
        (EventTypes is null || EventTypes.Contains(value.Type)) &&
        (!Severity.HasValue || value.Severity == Severity);
}

public sealed record EventQueryResult(IReadOnlyList<UpsEvent> Events, bool HasMore);

/// <summary>Uses the persisted timestamp precision and original message, never localized UI text.</summary>
public readonly record struct EventIdentity(long UtcMilliseconds, UpsEventType Type, string Message,
    UpsPowerState PreviousState, UpsPowerState CurrentState)
{
    public static EventIdentity Create(UpsEvent value) => new(value.Timestamp.ToUnixTimeMilliseconds(),
        value.Type, value.Message, value.PreviousState, value.CurrentState);
}

public static class EventQueryResults
{
    public static EventQueryResult Merge(EventQuery query, EventQueryResult saved, IEnumerable<UpsEvent> live)
    {
        var events = saved.Events.Concat(live).Where(query.Matches)
            .DistinctBy(EventIdentity.Create).OrderByDescending(e => e.Timestamp.ToUnixTimeMilliseconds())
            .Take(query.Limit + 1).ToArray();
        return new(events.Take(query.Limit).ToArray(), saved.HasMore || events.Length > query.Limit);
    }
}
