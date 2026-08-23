using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public static class TelemetryExporter
{
    public static async Task ExportTelemetryCsvAsync(
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

    public static async Task ExportTelemetryJsonAsync(
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
        var records = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var timestampMs = reader.GetInt64(0);
            var timestampUtc = DateTimeOffset.FromUnixTimeMilliseconds(timestampMs);

            records.Add(new Dictionary<string, object?>
            {
                ["timestampUtc"] = timestampUtc.ToString("O"),
                ["timestampLocal"] = timestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                ["deviceId"] = reader.GetString(1),
                ["inputVoltage"] = reader.IsDBNull(2) ? null : reader.GetDouble(2),
                ["outputVoltage"] = reader.IsDBNull(3) ? null : reader.GetDouble(3),
                ["batteryVoltage"] = reader.IsDBNull(4) ? null : reader.GetDouble(4),
                ["batteryPercent"] = reader.IsDBNull(5) ? null : reader.GetDouble(5),
                ["runtimeSeconds"] = reader.IsDBNull(6) ? null : reader.GetDouble(6),
                ["runtimeMinutes"] = reader.IsDBNull(6) ? null : Math.Round(reader.GetDouble(6) / 60.0, 1),
                ["loadPercent"] = reader.IsDBNull(7) ? null : reader.GetDouble(7),
                ["activePowerWatts"] = reader.IsDBNull(8) ? null : reader.GetDouble(8),
                ["apparentPowerVa"] = reader.IsDBNull(9) ? null : reader.GetDouble(9),
                ["frequencyHz"] = reader.IsDBNull(10) ? null : reader.GetDouble(10),
                ["temperatureCelsius"] = reader.IsDBNull(11) ? null : reader.GetDouble(11),
                ["acPresent"] = reader.IsDBNull(12) ? null : reader.GetInt64(12) == 1,
                ["charging"] = reader.IsDBNull(13) ? null : reader.GetInt64(13) == 1,
                ["discharging"] = reader.IsDBNull(14) ? null : reader.GetInt64(14) == 1,
                ["lowBattery"] = reader.IsDBNull(15) ? null : reader.GetInt64(15) == 1,
                ["overload"] = reader.IsDBNull(16) ? null : reader.GetInt64(16) == 1,
            });
        }

        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        await using var fileStream = File.Create(destinationFilePath);
        await JsonSerializer.SerializeAsync(fileStream, records, jsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public static async Task ExportEventsCsvAsync(
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
                event_type,
                previous_state,
                current_state,
                message
            FROM ups_events
            WHERE timestamp_utc_ms >= $from AND timestamp_utc_ms <= $to
            ORDER BY timestamp_utc_ms ASC;
            """;
        command.Parameters.AddWithValue("$from", from.ToUniversalTime().ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$to", to.ToUniversalTime().ToUnixTimeMilliseconds());

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
