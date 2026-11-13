using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public static class TelemetryExporter
{
    // Same-directory staging keeps successful publication on the same volume.
    internal static async Task WriteAtomicallyAsync(string destination, Func<string, Task> write, CancellationToken token)
    {
        var fullPath = Path.GetFullPath(destination);
        var temporary = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            token.ThrowIfCancellationRequested();
            await write(temporary).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            File.Move(temporary, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public static Task ExportTelemetryCsvAsync(string databasePath, string destinationFilePath,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
        WriteAtomicallyAsync(destinationFilePath, path => WriteTelemetryCsvAsync(databasePath, path, from, to, cancellationToken), cancellationToken);

    public static Task ExportTelemetryJsonAsync(string databasePath, string destinationFilePath,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
        WriteAtomicallyAsync(destinationFilePath, path => WriteTelemetryJsonAsync(databasePath, path, from, to, cancellationToken), cancellationToken);

    // Preserve the legacy inclusive upper bound on this overload.
    public static Task ExportEventsCsvAsync(string databasePath, string destinationFilePath,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default) =>
        WriteAtomicallyAsync(destinationFilePath, path => WriteEventsCsvAsync(databasePath, path, from, to, cancellationToken), cancellationToken);

    public static Task ExportEventsCsvAsync(string databasePath, string destinationFilePath,
        EventQuery query, CancellationToken cancellationToken = default)
    {
        query = query.Snapshot();
        return WriteAtomicallyAsync(destinationFilePath, path => WriteEventsCsvAsync(databasePath, path,
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue, cancellationToken, query), cancellationToken);
    }

    public static Task ExportAllTelemetryCsvAsync(
        string databasePath,
        string destinationFilePath,
        CancellationToken cancellationToken = default) =>
        ExportTelemetryCsvAsync(databasePath, destinationFilePath, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, cancellationToken);

    public static Task ExportAllTelemetryJsonAsync(
        string databasePath,
        string destinationFilePath,
        CancellationToken cancellationToken = default) =>
        ExportTelemetryJsonAsync(databasePath, destinationFilePath, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, cancellationToken);

    public static Task ExportAllEventsCsvAsync(
        string databasePath,
        string destinationFilePath,
        CancellationToken cancellationToken = default) =>
        ExportEventsCsvAsync(databasePath, destinationFilePath, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, cancellationToken);

    private static async Task WriteTelemetryCsvAsync(
        string databasePath,
        string destinationFilePath,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            DefaultTimeout = 5,
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                timestamp_utc_ms,
                device_id,
                input_voltage,
                output_voltage,
                battery_voltage,
                battery_percent,
                runtime_seconds,
                load_percent,
                active_power_watts,
                apparent_power_va,
                frequency_hz,
                temperature_c,
                ac_present,
                charging,
                discharging,
                low_battery,
                overload
            FROM telemetry_samples
            WHERE timestamp_utc_ms >= $from AND timestamp_utc_ms <= $to
            ORDER BY timestamp_utc_ms ASC;
            """;
        command.Parameters.AddWithValue("$from", from.ToUniversalTime().ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$to", to.ToUniversalTime().ToUnixTimeMilliseconds());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var writer = new StreamWriter(destinationFilePath, append: false, Encoding.UTF8);

        // Header
        await writer.WriteLineAsync(
            "TimestampUtc,TimestampLocal,DeviceId,InputVoltage_V,OutputVoltage_V,BatteryVoltage_V,BatteryPercent,RuntimeMinutes,LoadPercent,ActivePower_W,ApparentPower_VA,Frequency_Hz,Temperature_C,AcPresent,Charging,Discharging,LowBattery,Overload");

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var timestampMs = reader.GetInt64(0);
            var timestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
            var timestampLocal = timestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var deviceId = reader.GetString(1);
            var inputV = FormatNullableDouble(reader, 2);
            var outputV = FormatNullableDouble(reader, 3);
            var batteryV = FormatNullableDouble(reader, 4);
            var batteryPct = FormatNullableDouble(reader, 5);
            var runtimeMin = reader.IsDBNull(6) ? string.Empty : (reader.GetDouble(6) / 60.0).ToString("F1", CultureInfo.InvariantCulture);
            var loadPct = FormatNullableDouble(reader, 7);
            var activeW = FormatNullableDouble(reader, 8);
            var apparentVa = FormatNullableDouble(reader, 9);
            var freqHz = FormatNullableDouble(reader, 10);
            var tempC = FormatNullableDouble(reader, 11);
            var acPresent = FormatNullableBool(reader, 12);
            var charging = FormatNullableBool(reader, 13);
            var discharging = FormatNullableBool(reader, 14);
            var lowBattery = FormatNullableBool(reader, 15);
            var overload = FormatNullableBool(reader, 16);

            await writer.WriteLineAsync(
                $"{timestampUtc:O},{timestampLocal},{EscapeCsv(deviceId)},{inputV},{outputV},{batteryV},{batteryPct},{runtimeMin},{loadPct},{activeW},{apparentVa},{freqHz},{tempC},{acPresent},{charging},{discharging},{lowBattery},{overload}");
        }
    }

    private static async Task WriteTelemetryJsonAsync(
        string databasePath,
        string destinationFilePath,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            DefaultTimeout = 5,
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                timestamp_utc_ms,
                device_id,
                input_voltage,
                output_voltage,
                battery_voltage,
                battery_percent,
                runtime_seconds,
                load_percent,
                active_power_watts,
                apparent_power_va,
                frequency_hz,
                temperature_c,
                ac_present,
                charging,
                discharging,
                low_battery,
                overload
            FROM telemetry_samples
            WHERE timestamp_utc_ms >= $from AND timestamp_utc_ms <= $to
            ORDER BY timestamp_utc_ms ASC;
            """;
        command.Parameters.AddWithValue("$from", from.ToUniversalTime().ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$to", to.ToUniversalTime().ToUnixTimeMilliseconds());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var fileStream = File.Create(destinationFilePath);
        using var jsonWriter = new Utf8JsonWriter(fileStream, new JsonWriterOptions { Indented = true });
        jsonWriter.WriteStartArray();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var timestampMs = reader.GetInt64(0);
            var timestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);

            jsonWriter.WriteStartObject();
            jsonWriter.WriteString("timestampUtc", timestampUtc.ToString("O"));
            jsonWriter.WriteString("timestampLocal", timestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            jsonWriter.WriteString("deviceId", reader.GetString(1));
            WriteNumber("inputVoltage", 2);
            WriteNumber("outputVoltage", 3);
            WriteNumber("batteryVoltage", 4);
            WriteNumber("batteryPercent", 5);
            WriteNumber("runtimeSeconds", 6);
            if (reader.IsDBNull(6)) jsonWriter.WriteNull("runtimeMinutes");
            else jsonWriter.WriteNumber("runtimeMinutes", Math.Round(reader.GetDouble(6) / 60.0, 1));
            WriteNumber("loadPercent", 7);
            WriteNumber("activePowerWatts", 8);
            WriteNumber("apparentPowerVa", 9);
            WriteNumber("frequencyHz", 10);
            WriteNumber("temperatureCelsius", 11);
            WriteBoolean("acPresent", 12);
            WriteBoolean("charging", 13);
            WriteBoolean("discharging", 14);
            WriteBoolean("lowBattery", 15);
            WriteBoolean("overload", 16);
            jsonWriter.WriteEndObject();
            // Keep the writer buffer bounded independently of the exported row count.
            await jsonWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        void WriteNumber(string name, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) jsonWriter.WriteNull(name);
            else jsonWriter.WriteNumber(name, reader.GetDouble(ordinal));
        }
        void WriteBoolean(string name, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) jsonWriter.WriteNull(name);
            else jsonWriter.WriteBoolean(name, reader.GetInt64(ordinal) == 1);
        }
        jsonWriter.WriteEndArray();
        await jsonWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task WriteEventsCsvAsync(
        string databasePath,
        string destinationFilePath,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default,
        EventQuery? query = null)
    {
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            DefaultTimeout = 5,
        }.ToString();

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                timestamp_utc_ms,
                device_id,
                event_type,
                previous_state,
                current_state,
                message
            FROM ups_events
            WHERE timestamp_utc_ms >= $from AND timestamp_utc_ms <= $to
            ORDER BY timestamp_utc_ms ASC;
            """;
        if (query is null)
        {
            command.Parameters.AddWithValue("$from", from.ToUniversalTime().ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("$to", to.ToUniversalTime().ToUnixTimeMilliseconds());
        }
        else
        {
            command.CommandText = "SELECT timestamp_utc_ms, device_id, event_type, previous_state, current_state, message FROM ups_events WHERE "
                + SqliteTelemetryStore.EventFilterSql + " ORDER BY timestamp_utc_ms ASC;";
            SqliteTelemetryStore.BindEventQuery(command, query);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await using var writer = new StreamWriter(destinationFilePath, append: false, Encoding.UTF8);

        // Header
        await writer.WriteLineAsync("TimestampUtc,TimestampLocal,DeviceId,EventType,PreviousState,CurrentState,Message");

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var timestampMs = reader.GetInt64(0);
            var timestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);
            var timestampLocal = timestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            var deviceId = reader.GetString(1);
            var eventType = ((UpsEventType)reader.GetInt32(2)).ToString();
            var previousState = ((UpsPowerState)reader.GetInt32(3)).ToString();
            var currentState = ((UpsPowerState)reader.GetInt32(4)).ToString();
            var message = reader.GetString(5);

            await writer.WriteLineAsync(
                $"{timestampUtc:O},{timestampLocal},{EscapeCsv(deviceId)},{eventType},{previousState},{currentState},{EscapeCsv(message)}");
        }
    }

    private static string FormatNullableDouble(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : reader.GetDouble(ordinal).ToString("F2", CultureInfo.InvariantCulture);

    private static string FormatNullableBool(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? string.Empty : (reader.GetInt64(ordinal) == 1).ToString();

    private static string EscapeCsv(string text) =>
        text.Contains(',') || text.Contains('"') || text.Contains('\n') || text.Contains('\r')
            ? $"\"{text.Replace("\"", "\"\"")}\""
            : text;
}
