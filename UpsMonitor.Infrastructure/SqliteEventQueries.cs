using System.Text.Json;
using Microsoft.Data.Sqlite;
using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public sealed partial class SqliteTelemetryStore
{
    internal const string EventFilterSql = """
            device_id IN (
                SELECT DISTINCT device_id FROM ups_events WHERE $device IS NULL OR device_id = $device
            )
                AND timestamp_utc_ms >= $from AND timestamp_utc_ms < $to
                AND ($types IS NULL OR event_type IN (SELECT value FROM json_each($types)))
                AND ($excludedTypes IS NULL OR event_type NOT IN (SELECT value FROM json_each($excludedTypes)))
            """;

    internal const string SavedEventQuerySql = """
            SELECT DISTINCT timestamp_utc_ms, event_type, message, previous_state, current_state
            FROM ups_events WHERE
            """ + " " + EventFilterSql + " " + """

            ORDER BY timestamp_utc_ms DESC, event_type, message, previous_state, current_state
            LIMIT $limit;
            """;

    public async Task<EventQueryResult> QueryEventsAsync(EventQuery query, CancellationToken cancellationToken = default)
    {
        query = query.Snapshot();
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = SavedEventQuerySql;
        BindEventQuery(command, query);
        command.Parameters.AddWithValue("$limit", query.Limit + 1);
        var events = new List<UpsEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();
            events.Add(new(DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(0)),
                (UpsEventType)reader.GetInt32(1), reader.GetString(2),
                (UpsPowerState)reader.GetInt32(3), (UpsPowerState)reader.GetInt32(4)));
        }
        return new(events.Take(query.Limit).ToArray(), events.Count > query.Limit);
    }

    internal static void BindEventQuery(SqliteCommand command, EventQuery query)
    {
        var types = query.EventTypes;
        UpsEventType[]? excludedTypes = null;
        if (query.Severity.HasValue)
        {
            UpsEventSeverity SeverityOf(UpsEventType type) =>
                new UpsEvent(DateTimeOffset.UnixEpoch, type, string.Empty, UpsPowerState.Unknown, UpsPowerState.Unknown).Severity;
            if (query.Severity == UpsEventSeverity.Information)
                excludedTypes = Enum.GetValues<UpsEventType>().Where(t => SeverityOf(t) != query.Severity).ToArray();
            else
                types = Enum.GetValues<UpsEventType>().Where(t => SeverityOf(t) == query.Severity &&
                    (types is null || types.Contains(t))).ToHashSet();
        }
        command.Parameters.AddWithValue("$device", (object?)query.DeviceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$from", query.From?.ToUnixTimeMilliseconds() ?? long.MinValue);
        command.Parameters.AddWithValue("$to", query.To?.ToUnixTimeMilliseconds() ?? long.MaxValue);
        command.Parameters.AddWithValue("$types", types is null ? DBNull.Value :
            JsonSerializer.Serialize(types.Select(t => (int)t)));
        command.Parameters.AddWithValue("$excludedTypes", excludedTypes is null ? DBNull.Value :
            JsonSerializer.Serialize(excludedTypes.Select(t => (int)t)));
    }
}
