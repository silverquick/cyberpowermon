using Microsoft.Data.Sqlite;
using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public sealed partial class SqliteTelemetryStore : IOfficialHealthContextSource
{
    private static readonly TimeSpan ContextInstantWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 公式値の記録時刻 at における状況を履歴から集計する。
    /// 瞬間値は at の前後5分の最近傍サンプル(raw が無ければ1分ロールアップ)、
    /// 直前統計は保持が無期限の1分ロールアップから集計する。該当データが無い項目は null。
    /// </summary>
    public async Task<OfficialHealthContext> QueryOfficialHealthContextAsync(
        string deviceId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var atMs = at.ToUniversalTime().ToUnixTimeMilliseconds();
        var context = new OfficialHealthContext();
        await FillInstantAsync(connection, deviceId, atMs, context, cancellationToken).ConfigureAwait(false);

        var tenMinutes = await QueryRollupStatsAsync(
            connection, deviceId, TelemetryMetric.LoadPercent, atMs - (long)TimeSpan.FromMinutes(10).TotalMilliseconds, atMs, cancellationToken)
            .ConfigureAwait(false);
        context.Load10MinuteMean = tenMinutes?.Mean;
        context.Load10MinuteMax = tenMinutes?.Max;

        var dayStart = atMs - (long)TimeSpan.FromHours(24).TotalMilliseconds;
        var day = await QueryRollupStatsAsync(
            connection, deviceId, TelemetryMetric.LoadPercent, dayStart, atMs, cancellationToken).ConfigureAwait(false);
        context.Load24HourMean = day?.Mean;
        context.Load24HourMax = day?.Max;
        context.BatteryVoltage24HourMean = (await QueryRollupStatsAsync(
            connection, deviceId, TelemetryMetric.BatteryVoltage, dayStart, atMs, cancellationToken).ConfigureAwait(false))?.Mean;

        await FillDischargeAsync(connection, deviceId, atMs, context, cancellationToken).ConfigureAwait(false);
        return context;
    }

    private static async Task FillInstantAsync(
        SqliteConnection connection,
        string deviceId,
        long atMs,
        OfficialHealthContext context,
        CancellationToken cancellationToken)
    {
        var window = (long)ContextInstantWindow.TotalMilliseconds;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT load_percent, active_power_watts, runtime_seconds, battery_voltage,
                       input_voltage, temperature_c, charging
                FROM telemetry_samples
                WHERE device_id = $device
                    AND is_connected = 1
                    AND timestamp_utc_ms BETWEEN $from AND $to
                ORDER BY ABS(timestamp_utc_ms - $at)
                LIMIT 1;
                """;
            command.Parameters.AddWithValue("$device", deviceId);
            command.Parameters.AddWithValue("$from", atMs - window);
            command.Parameters.AddWithValue("$to", atMs + window);
            command.Parameters.AddWithValue("$at", atMs);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                context.LoadPercent = NullableDouble(reader, 0);
                context.ActivePowerWatts = NullableDouble(reader, 1);
                context.RuntimeSeconds = NullableDouble(reader, 2);
                context.BatteryVoltage = NullableDouble(reader, 3);
                context.InputVoltage = NullableDouble(reader, 4);
                context.TemperatureCelsius = NullableDouble(reader, 5);
                context.Charging = reader.IsDBNull(6) ? null : reader.GetInt64(6) != 0;
                return;
            }
        }

        // raw サンプルの保持期間外: 1分ロールアップの最近傍バケットを使う(充電中フラグは無い)。
        context.LoadPercent = await NearestRollupAsync(connection, deviceId, TelemetryMetric.LoadPercent, atMs, window, cancellationToken).ConfigureAwait(false);
        context.ActivePowerWatts = await NearestRollupAsync(connection, deviceId, TelemetryMetric.ActivePowerWatts, atMs, window, cancellationToken).ConfigureAwait(false);
        context.RuntimeSeconds = await NearestRollupAsync(connection, deviceId, TelemetryMetric.RuntimeMinutes, atMs, window, cancellationToken).ConfigureAwait(false) * 60;
        context.BatteryVoltage = await NearestRollupAsync(connection, deviceId, TelemetryMetric.BatteryVoltage, atMs, window, cancellationToken).ConfigureAwait(false);
        context.InputVoltage = await NearestRollupAsync(connection, deviceId, TelemetryMetric.InputVoltage, atMs, window, cancellationToken).ConfigureAwait(false);
        context.TemperatureCelsius = await NearestRollupAsync(connection, deviceId, TelemetryMetric.TemperatureCelsius, atMs, window, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<double?> NearestRollupAsync(
        SqliteConnection connection,
        string deviceId,
        TelemetryMetric metric,
        long atMs,
        long windowMs,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        // バケットは1分幅。バケット中央との距離で最近傍を選ぶ。
        command.CommandText = """
            SELECT value_sum / sample_count
            FROM telemetry_rollups_1m
            WHERE device_id = $device
                AND metric = $metric
                AND sample_count > 0
                AND bucket_utc_ms BETWEEN $from AND $to
            ORDER BY ABS(bucket_utc_ms + 30000 - $at)
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$device", deviceId);
        command.Parameters.AddWithValue("$metric", (int)metric);
        command.Parameters.AddWithValue("$from", atMs - windowMs - 60_000);
        command.Parameters.AddWithValue("$to", atMs + windowMs);
        command.Parameters.AddWithValue("$at", atMs);
        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is null or DBNull ? null : Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<(double Mean, double Max)?> QueryRollupStatsAsync(
        SqliteConnection connection,
        string deviceId,
        TelemetryMetric metric,
        long fromMs,
        long toMs,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT SUM(value_sum), SUM(sample_count), MAX(maximum)
            FROM telemetry_rollups_1m
            WHERE device_id = $device
                AND metric = $metric
                AND bucket_utc_ms >= $from
                AND bucket_utc_ms <= $to;
            """;
        command.Parameters.AddWithValue("$device", deviceId);
        command.Parameters.AddWithValue("$metric", (int)metric);
        command.Parameters.AddWithValue("$from", fromMs);
        command.Parameters.AddWithValue("$to", toMs);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            || reader.IsDBNull(0) || reader.IsDBNull(1) || reader.GetInt64(1) <= 0)
        {
            return null;
        }

        return (reader.GetDouble(0) / reader.GetInt64(1), reader.GetDouble(2));
    }

    private static async Task FillDischargeAsync(
        SqliteConnection connection,
        string deviceId,
        long atMs,
        OfficialHealthContext context,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        // 電池運転(OnBattery/LowBattery/Critical)の最終状態変化と、その後にオンライン等へ戻った時刻。
        command.CommandText = """
            SELECT
                (SELECT MAX(timestamp_utc_ms) FROM ups_state_changes
                  WHERE device_id = $device AND timestamp_utc_ms <= $at AND power_state IN (2, 3, 4)),
                (SELECT MIN(timestamp_utc_ms) FROM ups_state_changes
                  WHERE device_id = $device AND power_state NOT IN (2, 3, 4) AND timestamp_utc_ms <= $at
                    AND timestamp_utc_ms > COALESCE(
                        (SELECT MAX(timestamp_utc_ms) FROM ups_state_changes
                          WHERE device_id = $device AND timestamp_utc_ms <= $at AND power_state IN (2, 3, 4)), 0)),
                (SELECT MIN(bucket_utc_ms) FROM telemetry_rollups_1m WHERE device_id = $device),
                (SELECT MIN(timestamp_utc_ms) FROM ups_state_changes WHERE device_id = $device);
            """;
        command.Parameters.AddWithValue("$device", deviceId);
        command.Parameters.AddWithValue("$at", atMs);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        var rollupStart = reader.IsDBNull(2) ? (long?)null : reader.GetInt64(2);
        var stateStart = reader.IsDBNull(3) ? (long?)null : reader.GetInt64(3);
        var historyStart = new[] { rollupStart, stateStart }.Where(item => item.HasValue).Select(item => item!.Value).DefaultIfEmpty(-1).Min();
        if (historyStart >= 0)
        {
            context.HistoryStartedAt = DateTimeOffset.FromUnixTimeMilliseconds(historyStart);
        }

        if (reader.IsDBNull(0))
        {
            return;
        }

        // 最後の電池運転状態の後に別状態へ戻った記録が無ければ、記録時点でも電池運転中(0日)。
        var endMs = reader.IsDBNull(1) ? atMs : reader.GetInt64(1);
        context.DaysSinceLastDischarge = Math.Max(0, (atMs - endMs) / 86_400_000.0);
    }

    private static double? NullableDouble(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
}
