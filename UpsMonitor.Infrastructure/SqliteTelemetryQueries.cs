using Microsoft.Data.Sqlite;
using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public sealed partial class SqliteTelemetryStore
{
    private static readonly IReadOnlyDictionary<TelemetryMetric, string> SampleColumns =
        new Dictionary<TelemetryMetric, string>
        {
            [TelemetryMetric.InputVoltage] = "input_voltage",
            [TelemetryMetric.OutputVoltage] = "output_voltage",
            [TelemetryMetric.BatteryVoltage] = "battery_voltage",
            [TelemetryMetric.BatteryPercent] = "battery_percent",
            [TelemetryMetric.RuntimeMinutes] = "runtime_seconds / 60.0",
            [TelemetryMetric.LoadPercent] = "load_percent",
            [TelemetryMetric.ActivePowerWatts] = "active_power_watts",
            [TelemetryMetric.ApparentPowerVoltAmperes] = "apparent_power_va",
            [TelemetryMetric.FrequencyHertz] = "frequency_hz",
            [TelemetryMetric.TemperatureCelsius] = "temperature_c",
        };

    public async Task<WeeklyPatternResult> QueryWeeklyPatternAsync(
        string deviceId,
        DateTimeOffset from,
        DateTimeOffset to,
        TelemetryMetric metric,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        if (to <= from)
        {
            throw new ArgumentOutOfRangeException(nameof(to));
        }

        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await QueryWeeklyPatternAsync(
            connection,
            deviceId,
            from,
            to,
            metric,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<TelemetryHistoryResult> QueryHistoryAsync(
        string deviceId,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyCollection<TelemetryMetric> metrics,
        int maximumPoints = 720,
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        if (to <= from)
        {
            throw new ArgumentOutOfRangeException(nameof(to));
        }

        if (maximumPoints is < 60 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumPoints));
        }

        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var fromMilliseconds = from.ToUniversalTime().ToUnixTimeMilliseconds();
        var toMilliseconds = to.ToUniversalTime().ToUnixTimeMilliseconds();
        var durationMilliseconds = toMilliseconds - fromMilliseconds;
        var useRawSamples = durationMilliseconds <= TimeSpan.FromHours(24).TotalMilliseconds;
        var minimumBucket = useRawSamples ? 1_000L : 60_000L;
        var bucketMilliseconds = NiceBucketSize(
            Math.Max(minimumBucket, durationMilliseconds / maximumPoints));

        var histories = new Dictionary<TelemetryMetric, TelemetryMetricHistory>();
        foreach (var metric in metrics.Distinct())
        {
            var points = useRawSamples
                ? await QueryRawMetricAsync(
                    connection,
                    deviceId,
                    fromMilliseconds,
                    toMilliseconds,
                    bucketMilliseconds,
                    metric,
                    cancellationToken).ConfigureAwait(false)
                : await QueryRollupMetricAsync(
                    connection,
                    deviceId,
                    fromMilliseconds,
                    toMilliseconds,
                    bucketMilliseconds,
                    metric,
                    cancellationToken).ConfigureAwait(false);
            histories[metric] = new TelemetryMetricHistory(metric, points);
        }

        var events = await QueryEventsAsync(
            connection,
            deviceId,
            fromMilliseconds,
            toMilliseconds,
            cancellationToken).ConfigureAwait(false);
        var stateChanges = await QueryStateChangesAsync(
            connection,
            deviceId,
            fromMilliseconds,
            toMilliseconds,
            cancellationToken).ConfigureAwait(false);
        var health = await QueryHealthAsync(
            connection,
            deviceId,
            fromMilliseconds,
            toMilliseconds,
            cancellationToken).ConfigureAwait(false);
        var sourceSampleCount = await QuerySourceSampleCountAsync(
            connection,
            deviceId,
            fromMilliseconds,
            toMilliseconds,
            useRawSamples,
            cancellationToken).ConfigureAwait(false);

        var summary = BuildPeriodSummary(from, to, events, stateChanges, histories);

        return new TelemetryHistoryResult
        {
            From = from,
            To = to,
            Metrics = histories,
            Events = events,
            StateChanges = stateChanges,
            BatteryHealth = health,
            SourceSampleCount = sourceSampleCount,
            Summary = summary,
        };
    }

    public async Task<TelemetryDatabaseStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (SELECT COUNT(*) FROM telemetry_samples),
                (SELECT COUNT(*) FROM raw_telemetry_values),
                (SELECT COUNT(*) FROM ups_events),
                (SELECT MIN(timestamp_utc_ms) FROM telemetry_samples),
                (SELECT MAX(timestamp_utc_ms) FROM telemetry_samples);
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
        return new TelemetryDatabaseStatistics
        {
            SampleCount = reader.GetInt64(0),
            RawValueCount = reader.GetInt64(1),
            EventCount = reader.GetInt64(2),
            FirstSample = reader.IsDBNull(3) ? null : FromMilliseconds(reader.GetInt64(3)),
            LastSample = reader.IsDBNull(4) ? null : FromMilliseconds(reader.GetInt64(4)),
        };
    }

    private static async Task<IReadOnlyList<TelemetryHistoryPoint>> QueryRawMetricAsync(
        SqliteConnection connection,
        string deviceId,
        long fromMilliseconds,
        long toMilliseconds,
        long bucketMilliseconds,
        TelemetryMetric metric,
        CancellationToken cancellationToken)
    {
        var column = SampleColumns[metric];
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                (timestamp_utc_ms / $bucket) * $bucket AS bucket,
                MIN({column}),
                AVG({column}),
                MAX({column})
            FROM telemetry_samples
            WHERE device_id = $device
                AND timestamp_utc_ms >= $from
                AND timestamp_utc_ms <= $to
                AND {column} IS NOT NULL
            GROUP BY bucket
            ORDER BY bucket;
            """;
        AddRangeParameters(command, deviceId, fromMilliseconds, toMilliseconds);
        command.Parameters.AddWithValue("$bucket", bucketMilliseconds);
        return await ReadHistoryPointsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<TelemetryHistoryPoint>> QueryRollupMetricAsync(
        SqliteConnection connection,
        string deviceId,
        long fromMilliseconds,
        long toMilliseconds,
        long bucketMilliseconds,
        TelemetryMetric metric,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                (bucket_utc_ms / $bucket) * $bucket AS bucket,
                MIN(minimum),
                SUM(value_sum) / SUM(sample_count),
                MAX(maximum)
            FROM telemetry_rollups_1m
            WHERE device_id = $device
                AND metric = $metric
                AND bucket_utc_ms >= $from
                AND bucket_utc_ms <= $to
            GROUP BY (bucket_utc_ms / $bucket)
            ORDER BY bucket;
            """;
        AddRangeParameters(command, deviceId, fromMilliseconds, toMilliseconds);
        command.Parameters.AddWithValue("$bucket", bucketMilliseconds);
        command.Parameters.AddWithValue("$metric", (int)metric);
        return await ReadHistoryPointsAsync(command, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<TelemetryHistoryPoint>> ReadHistoryPointsAsync(
        SqliteCommand command,
        CancellationToken cancellationToken)
    {
        var points = new List<TelemetryHistoryPoint>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var average = reader.GetDouble(2);
            points.Add(new(
                FromMilliseconds(reader.GetInt64(0)),
                reader.GetDouble(1),
                average,
                reader.GetDouble(3),
                average));
        }

        return points;
    }

    private static async Task<IReadOnlyList<UpsEvent>> QueryEventsAsync(
        SqliteConnection connection,
        string deviceId,
        long fromMilliseconds,
        long toMilliseconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT timestamp_utc_ms, event_type, message, previous_state, current_state
            FROM ups_events
            WHERE device_id = $device
                AND timestamp_utc_ms >= $from
                AND timestamp_utc_ms <= $to
            ORDER BY timestamp_utc_ms;
            """;
        AddRangeParameters(command, deviceId, fromMilliseconds, toMilliseconds);
        var events = new List<UpsEvent>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            events.Add(new(
                FromMilliseconds(reader.GetInt64(0)),
                (UpsEventType)reader.GetInt32(1),
                reader.GetString(2),
                (UpsPowerState)reader.GetInt32(3),
                (UpsPowerState)reader.GetInt32(4)));
        }

        return events;
    }

    private static async Task<IReadOnlyList<UpsStateChange>> QueryStateChangesAsync(
        SqliteConnection connection,
        string deviceId,
        long fromMilliseconds,
        long toMilliseconds,
        CancellationToken cancellationToken)
    {
        var changes = new List<UpsStateChange>();
        await using (var initialCommand = connection.CreateCommand())
        {
            initialCommand.CommandText = """
                SELECT timestamp_utc_ms, power_state
                FROM ups_state_changes
                WHERE device_id = $device AND timestamp_utc_ms <= $from
                ORDER BY timestamp_utc_ms DESC
                LIMIT 1;
                """;
            initialCommand.Parameters.AddWithValue("$device", deviceId);
            initialCommand.Parameters.AddWithValue("$from", fromMilliseconds);
            await using var reader = await initialCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                changes.Add(new(FromMilliseconds(fromMilliseconds), (UpsPowerState)reader.GetInt32(1)));
            }
        }

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                SELECT timestamp_utc_ms, power_state
                FROM ups_state_changes
                WHERE device_id = $device
                    AND timestamp_utc_ms > $from
                    AND timestamp_utc_ms <= $to
                ORDER BY timestamp_utc_ms;
                """;
            AddRangeParameters(command, deviceId, fromMilliseconds, toMilliseconds);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var state = (UpsPowerState)reader.GetInt32(1);
                if (changes.Count == 0 || changes[^1].State != state)
                {
                    changes.Add(new(FromMilliseconds(reader.GetInt64(0)), state));
                }
            }
        }

        return changes;
    }

    private static async Task<IReadOnlyList<BatteryHealthObservation>> QueryHealthAsync(
        SqliteConnection connection,
        string deviceId,
        long fromMilliseconds,
        long toMilliseconds,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT timestamp_utc_ms, health_percent, relative_performance_percent,
                   status, method, confidence, anchor_source, vendor_category, replacement_status
            FROM battery_health_observations
            WHERE device_id = $device
                AND timestamp_utc_ms >= $from
                AND timestamp_utc_ms <= $to
            ORDER BY timestamp_utc_ms;
            """;
        AddRangeParameters(command, deviceId, fromMilliseconds, toMilliseconds);
        var observations = new List<BatteryHealthObservation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            observations.Add(new()
            {
                DeviceId = deviceId,
                Timestamp = FromMilliseconds(reader.GetInt64(0)),
                HealthPercent = reader.IsDBNull(1) ? null : reader.GetDouble(1),
                RelativePerformancePercent = reader.IsDBNull(2) ? null : reader.GetDouble(2),
                Status = (BatteryHealthStatus)reader.GetInt32(3),
                Method = (BatteryHealthMethod)reader.GetInt32(4),
                Confidence = (BatteryHealthConfidence)reader.GetInt32(5),
                AnchorSource = reader.IsDBNull(6) ? null : reader.GetString(6),
                VendorCategory = (VendorBatteryHealthCategory)reader.GetInt32(7),
                ReplacementStatus = (BatteryReplacementStatus)reader.GetInt32(8),
            });
        }

        return observations;
    }

    private static async Task<long> QuerySourceSampleCountAsync(
        SqliteConnection connection,
        string deviceId,
        long fromMilliseconds,
        long toMilliseconds,
        bool useRawSamples,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = useRawSamples
            ? """
                SELECT COUNT(*) FROM telemetry_samples
                WHERE device_id = $device AND timestamp_utc_ms >= $from AND timestamp_utc_ms <= $to;
                """
            : """
                SELECT COALESCE(MAX(samples), 0)
                FROM (
                    SELECT SUM(sample_count) AS samples
                    FROM telemetry_rollups_1m
                    WHERE device_id = $device AND bucket_utc_ms >= $from AND bucket_utc_ms <= $to
                    GROUP BY metric
                );
                """;
        AddRangeParameters(command, deviceId, fromMilliseconds, toMilliseconds);
        return Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)
            ?? 0,
            System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void AddRangeParameters(
        SqliteCommand command,
        string deviceId,
        long fromMilliseconds,
        long toMilliseconds)
    {
        command.Parameters.AddWithValue("$device", deviceId);
        command.Parameters.AddWithValue("$from", fromMilliseconds);
        command.Parameters.AddWithValue("$to", toMilliseconds);
    }

    private static long NiceBucketSize(long requestedMilliseconds)
    {
        long[] sizes = [1_000, 5_000, 10_000, 30_000, 60_000, 300_000, 900_000, 3_600_000, 21_600_000, 86_400_000];
        return sizes.FirstOrDefault(size => size >= requestedMilliseconds, sizes[^1]);
    }

    private static DateTimeOffset FromMilliseconds(long milliseconds) =>
        DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);

    private static TelemetryPeriodSummary BuildPeriodSummary(
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<UpsEvent> events,
        IReadOnlyList<UpsStateChange> stateChanges,
        IReadOnlyDictionary<TelemetryMetric, TelemetryMetricHistory> metrics)
    {
        var outageCount = events.Count(e => e.Type == UpsEventType.PowerLost);
        if (outageCount == 0 && stateChanges.Count > 0)
        {
            outageCount = stateChanges.Count(s => s.State is UpsPowerState.OnBattery or UpsPowerState.LowBattery or UpsPowerState.Critical);
        }

        var totalOutageMs = 0.0;
        if (stateChanges.Count > 0)
        {
            for (var i = 0; i < stateChanges.Count; i++)
            {
                var current = stateChanges[i];
                if (current.State is UpsPowerState.OnBattery or UpsPowerState.LowBattery or UpsPowerState.Critical)
                {
                    var nextTime = (i + 1 < stateChanges.Count) ? stateChanges[i + 1].Timestamp : to;
                    var duration = nextTime - current.Timestamp;
                    if (duration > TimeSpan.Zero)
                    {
                        totalOutageMs += duration.TotalMilliseconds;
                    }
                }
            }
        }

        var inputPoints = metrics.GetValueOrDefault(TelemetryMetric.InputVoltage)?.Points ?? [];
        var outputPoints = metrics.GetValueOrDefault(TelemetryMetric.OutputVoltage)?.Points ?? [];
        var loadPoints = metrics.GetValueOrDefault(TelemetryMetric.LoadPercent)?.Points ?? [];
        var activePoints = metrics.GetValueOrDefault(TelemetryMetric.ActivePowerWatts)?.Points ?? [];
        var apparentPoints = metrics.GetValueOrDefault(TelemetryMetric.ApparentPowerVoltAmperes)?.Points ?? [];
        var batteryPoints = metrics.GetValueOrDefault(TelemetryMetric.BatteryPercent)?.Points ?? [];
        var batVoltPoints = metrics.GetValueOrDefault(TelemetryMetric.BatteryVoltage)?.Points ?? [];
        var freqPoints = metrics.GetValueOrDefault(TelemetryMetric.FrequencyHertz)?.Points ?? [];
        var tempPoints = metrics.GetValueOrDefault(TelemetryMetric.TemperatureCelsius)?.Points ?? [];

        double? totalEnergyKwh = null;
        if (activePoints.Count > 0)
        {
            var energySumWattHours = 0.0;
            for (var i = 0; i < activePoints.Count - 1; i++)
            {
                var p1 = activePoints[i];
                var p2 = activePoints[i + 1];
                var dtHours = (p2.Timestamp - p1.Timestamp).TotalHours;
                if (dtHours is > 0 and <= 2.0)
                {
                    energySumWattHours += ((p1.Average + p2.Average) / 2.0) * dtHours;
                }
            }

            if (energySumWattHours == 0.0 && activePoints.Count > 0)
            {
                var avgW = activePoints.Average(p => p.Average);
                var hours = (to - from).TotalHours;
                energySumWattHours = avgW * hours;
            }

            totalEnergyKwh = energySumWattHours / 1000.0;
        }

        double? avgPowerFactor = null;
        if (activePoints.Count > 0 && apparentPoints.Count > 0)
        {
            var avgW = activePoints.Average(p => p.Average);
            var avgVa = apparentPoints.Average(p => p.Average);
            if (avgVa > 1.0)
            {
                avgPowerFactor = Math.Clamp(avgW / avgVa * 100.0, 0.0, 100.0);
            }
        }

        return new TelemetryPeriodSummary
        {
            OutageCount = outageCount,
            TotalOutageDuration = TimeSpan.FromMilliseconds(totalOutageMs),
            MinInputVoltage = inputPoints.Count > 0 ? inputPoints.Min(p => p.Minimum) : null,
            AvgInputVoltage = inputPoints.Count > 0 ? inputPoints.Average(p => p.Average) : null,
            MaxInputVoltage = inputPoints.Count > 0 ? inputPoints.Max(p => p.Maximum) : null,
            MinOutputVoltage = outputPoints.Count > 0 ? outputPoints.Min(p => p.Minimum) : null,
            AvgOutputVoltage = outputPoints.Count > 0 ? outputPoints.Average(p => p.Average) : null,
            MaxOutputVoltage = outputPoints.Count > 0 ? outputPoints.Max(p => p.Maximum) : null,
            PeakLoadPercent = loadPoints.Count > 0 ? loadPoints.Max(p => p.Maximum) : null,
            AvgLoadPercent = loadPoints.Count > 0 ? loadPoints.Average(p => p.Average) : null,
            PeakActivePowerWatts = activePoints.Count > 0 ? activePoints.Max(p => p.Maximum) : null,
            AvgActivePowerWatts = activePoints.Count > 0 ? activePoints.Average(p => p.Average) : null,
            TotalEnergyKwh = totalEnergyKwh,
            MinBatteryPercent = batteryPoints.Count > 0 ? batteryPoints.Min(p => p.Minimum) : null,
            AvgBatteryVoltage = batVoltPoints.Count > 0 ? batVoltPoints.Average(p => p.Average) : null,
            AvgFrequencyHertz = freqPoints.Count > 0 ? freqPoints.Average(p => p.Average) : null,
            AvgTemperatureCelsius = tempPoints.Count > 0 && tempPoints.Any(p => p.Average > -50) ? tempPoints.Where(p => p.Average > -50).Average(p => p.Average) : null,
            AvgPowerFactor = avgPowerFactor,
        };
    }

    public static async Task<WeeklyPatternResult> QueryWeeklyPatternAsync(
        SqliteConnection connection,
        string deviceId,
        DateTimeOffset from,
        DateTimeOffset to,
        TelemetryMetric metric,
        CancellationToken cancellationToken = default)
    {
        var fromMs = from.ToUniversalTime().ToUnixTimeMilliseconds();
        var toMs = to.ToUniversalTime().ToUnixTimeMilliseconds();

        var cellMap = new Dictionary<(int DOW, int HOD), HourlyPatternPoint>();
        var totalSamples = 0L;

        if (metric == TelemetryMetric.FrequencyHertz || metric == TelemetryMetric.TemperatureCelsius)
        {
            // For general single metric query
            await QuerySingleMetricPatternAsync(connection, deviceId, fromMs, toMs, metric, cellMap, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await QuerySingleMetricPatternAsync(connection, deviceId, fromMs, toMs, metric, cellMap, cancellationToken).ConfigureAwait(false);
        }

        var grid = new List<HourlyPatternPoint>(168);
        // DayOfWeek: 1 (Monday) .. 6 (Saturday), 0 (Sunday)
        int[] dayOrder = [1, 2, 3, 4, 5, 6, 0];
        foreach (var dow in dayOrder)
        {
            for (var hod = 0; hod < 24; hod++)
            {
                if (cellMap.TryGetValue((dow, hod), out var point))
                {
                    grid.Add(point);
                    totalSamples += point.SampleCount;
                }
                else
                {
                    grid.Add(new HourlyPatternPoint(dow, hod, 0.0, 0.0, 0.0, 0));
                }
            }
        }

        var validPoints = grid.Where(p => p.SampleCount > 0).ToList();
        var overallMin = validPoints.Count > 0 ? validPoints.Min(p => p.Minimum) : 0.0;
        var overallMax = validPoints.Count > 0 ? validPoints.Max(p => p.Maximum) : 0.0;
        var overallAvg = validPoints.Count > 0 ? validPoints.Average(p => p.Average) : 0.0;
        var peakHour = validPoints.Count > 0 ? validPoints.MaxBy(p => p.Average) : null;
        var lowestHour = validPoints.Count > 0 ? validPoints.MinBy(p => p.Average) : null;

        return new WeeklyPatternResult
        {
            Metric = metric,
            From = from,
            To = to,
            OverallMin = overallMin,
            OverallMax = overallMax,
            OverallAvg = overallAvg,
            Grid = grid,
            PeakHour = peakHour,
            LowestHour = lowestHour,
            TotalSamples = totalSamples,
        };
    }

    private static async Task QuerySingleMetricPatternAsync(
        SqliteConnection connection,
        string deviceId,
        long fromMs,
        long toMs,
        TelemetryMetric metric,
        IDictionary<(int DOW, int HOD), HourlyPatternPoint> cellMap,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
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
            """;
        AddRangeParameters(command, deviceId, fromMs, toMs);
        command.Parameters.AddWithValue("$metric", (int)metric);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var dow = reader.GetInt32(0);
            var hod = reader.GetInt32(1);
            var min = reader.GetDouble(2);
            var avg = reader.GetDouble(3);
            var max = reader.GetDouble(4);
            var count = reader.GetInt64(5);
            cellMap[(dow, hod)] = new HourlyPatternPoint(dow, hod, avg, min, max, count);
        }
    }
}

