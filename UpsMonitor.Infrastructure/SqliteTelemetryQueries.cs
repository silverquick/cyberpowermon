using System.Globalization;
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
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
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
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
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

        var distinctMetrics = metrics.Distinct().ToList();
        var histories = useRawSamples
            ? await QueryRawMetricsAsync(
                connection,
                deviceId,
                fromMilliseconds,
                toMilliseconds,
                bucketMilliseconds,
                distinctMetrics,
                cancellationToken).ConfigureAwait(false)
            : await QueryRollupMetricsAsync(
                connection,
                deviceId,
                fromMilliseconds,
                toMilliseconds,
                bucketMilliseconds,
                distinctMetrics,
                cancellationToken).ConfigureAwait(false);

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
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
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

    private static async Task<Dictionary<TelemetryMetric, TelemetryMetricHistory>> QueryRawMetricsAsync(
        SqliteConnection connection,
        string deviceId,
        long fromMilliseconds,
        long toMilliseconds,
        long bucketMilliseconds,
        IReadOnlyList<TelemetryMetric> metrics,
        CancellationToken cancellationToken)
    {
        var histories = new Dictionary<TelemetryMetric, TelemetryMetricHistory>();
        var pointLists = new Dictionary<TelemetryMetric, List<TelemetryHistoryPoint>>();
        foreach (var metric in metrics)
        {
            pointLists[metric] = new List<TelemetryHistoryPoint>();
        }

        if (metrics.Count == 0)
        {
            return histories;
        }

        var selectColumns = new List<string>();
        for (var i = 0; i < metrics.Count; i++)
        {
            var col = SampleColumns[metrics[i]];
            selectColumns.Add($"MIN({col})");
            selectColumns.Add($"AVG({col})");
            selectColumns.Add($"MAX({col})");
            selectColumns.Add($"COUNT({col})");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                (timestamp_utc_ms / $bucket) * $bucket AS bucket,
                {string.Join(",\n                ", selectColumns)}
            FROM telemetry_samples
            WHERE device_id = $device
                AND timestamp_utc_ms >= $from
                AND timestamp_utc_ms <= $to
            GROUP BY bucket
            ORDER BY bucket;
            """;
        AddRangeParameters(command, deviceId, fromMilliseconds, toMilliseconds);
        command.Parameters.AddWithValue("$bucket", bucketMilliseconds);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var bucketTime = FromMilliseconds(reader.GetInt64(0));
            var colOffset = 1;
            for (var i = 0; i < metrics.Count; i++)
            {
                var metric = metrics[i];
                var count = reader.GetInt64(colOffset + 3);
                if (count > 0)
                {
                    var min = reader.GetDouble(colOffset);
                    var avg = reader.GetDouble(colOffset + 1);
                    var max = reader.GetDouble(colOffset + 2);
                    pointLists[metric].Add(new TelemetryHistoryPoint(bucketTime, min, avg, max, avg));
                }

                colOffset += 4;
            }
        }

        foreach (var metric in metrics)
        {
            histories[metric] = new TelemetryMetricHistory(metric, pointLists[metric]);
        }

        return histories;
    }

    private static async Task<Dictionary<TelemetryMetric, TelemetryMetricHistory>> QueryRollupMetricsAsync(
        SqliteConnection connection,
        string deviceId,
        long fromMilliseconds,
        long toMilliseconds,
        long bucketMilliseconds,
        IReadOnlyList<TelemetryMetric> metrics,
        CancellationToken cancellationToken)
    {
        var histories = new Dictionary<TelemetryMetric, TelemetryMetricHistory>();
        var pointLists = new Dictionary<TelemetryMetric, List<TelemetryHistoryPoint>>();
        foreach (var metric in metrics)
        {
            pointLists[metric] = new List<TelemetryHistoryPoint>();
        }

        if (metrics.Count == 0)
        {
            return histories;
        }

        var metricParams = new List<string>();
        for (var i = 0; i < metrics.Count; i++)
        {
            metricParams.Add($"$m{i}");
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                metric,
                (bucket_utc_ms / $bucket) * $bucket AS bucket,
                MIN(minimum),
                SUM(value_sum) / SUM(sample_count),
                MAX(maximum)
            FROM telemetry_rollups_1m
            WHERE device_id = $device
                AND metric IN ({string.Join(", ", metricParams)})
                AND bucket_utc_ms >= $from
                AND bucket_utc_ms <= $to
            GROUP BY metric, (bucket_utc_ms / $bucket)
            ORDER BY metric, bucket;
            """;
        AddRangeParameters(command, deviceId, fromMilliseconds, toMilliseconds);
        command.Parameters.AddWithValue("$bucket", bucketMilliseconds);
        for (var i = 0; i < metrics.Count; i++)
        {
            command.Parameters.AddWithValue($"$m{i}", (int)metrics[i]);
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var metric = (TelemetryMetric)reader.GetInt32(0);
            var bucketTime = FromMilliseconds(reader.GetInt64(1));
            var min = reader.GetDouble(2);
            var avg = reader.GetDouble(3);
            var max = reader.GetDouble(4);
            if (pointLists.TryGetValue(metric, out var list))
            {
                list.Add(new TelemetryHistoryPoint(bucketTime, min, avg, max, avg));
            }
        }

        foreach (var metric in metrics)
        {
            histories[metric] = new TelemetryMetricHistory(metric, pointLists[metric]);
        }

        return histories;
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

        await QuerySingleMetricPatternAsync(connection, deviceId, fromMs, toMs, metric, cellMap, cancellationToken).ConfigureAwait(false);

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

    public async Task<IReadOnlyList<EnergyReportItem>> QueryEnergyReportsAsync(
        string deviceId,
        DateTimeOffset from,
        DateTimeOffset to,
        EnergyReportPeriod granularity,
        double electricityRatePerKwh,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceId);
        if (to <= from)
        {
            throw new ArgumentOutOfRangeException(nameof(to));
        }

        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        return await QueryEnergyReportsAsync(
            connection,
            deviceId,
            from,
            to,
            granularity,
            electricityRatePerKwh,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<EnergyReportItem>> QueryEnergyReportsAsync(
        SqliteConnection connection,
        string deviceId,
        DateTimeOffset from,
        DateTimeOffset to,
        EnergyReportPeriod granularity,
        double electricityRatePerKwh,
        CancellationToken cancellationToken)
    {
        var periods = new List<(string Key, DateTimeOffset Start, DateTimeOffset End)>();
        string strftimeFormat;

        if (granularity == EnergyReportPeriod.Day)
        {
            strftimeFormat = "%Y-%m-%d";
            var fromDate = DateOnly.FromDateTime(from.LocalDateTime);
            var toDate = DateOnly.FromDateTime(to.LocalDateTime);
            var current = fromDate;
            while (current <= toDate)
            {
                var localStart = current.ToDateTime(TimeOnly.MinValue);
                var localEnd = current.AddDays(1).ToDateTime(TimeOnly.MinValue);
                var startOffset = TimeZoneInfo.Local.GetUtcOffset(localStart);
                var endOffset = TimeZoneInfo.Local.GetUtcOffset(localEnd);
                var start = new DateTimeOffset(localStart, startOffset);
                var end = new DateTimeOffset(localEnd, endOffset);
                var key = current.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                periods.Add((key, start, end));
                current = current.AddDays(1);
            }
        }
        else
        {
            strftimeFormat = "%Y-%m";
            var startMonth = new DateTime(from.LocalDateTime.Year, from.LocalDateTime.Month, 1);
            var endMonth = new DateTime(to.LocalDateTime.Year, to.LocalDateTime.Month, 1);
            var current = startMonth;
            while (current <= endMonth)
            {
                var localStart = current;
                var localEnd = current.AddMonths(1);
                var startOffset = TimeZoneInfo.Local.GetUtcOffset(localStart);
                var endOffset = TimeZoneInfo.Local.GetUtcOffset(localEnd);
                var start = new DateTimeOffset(localStart, startOffset);
                var end = new DateTimeOffset(localEnd, endOffset);
                var key = current.ToString("yyyy-MM", CultureInfo.InvariantCulture);
                periods.Add((key, start, end));
                current = current.AddMonths(1);
            }
        }

        if (periods.Count == 0)
        {
            return [];
        }

        var fromMs = from.ToUniversalTime().ToUnixTimeMilliseconds();
        var toMs = to.ToUniversalTime().ToUnixTimeMilliseconds();

        // 1. telemetry_rollups_1m から ActivePowerWatts の集計を取得
        // 各 1 分 bucket の電力量 (kWh) = (value_sum / sample_count) / 60.0 / 1000.0
        // 観測された bucket だけ SUM を計算
        var powerData = new Dictionary<string, (double EnergyKwh, double AvgWatts, double PeakWatts)>();
        await using (var powerCmd = connection.CreateCommand())
        {
            powerCmd.CommandText = $"""
                SELECT
                    strftime('{strftimeFormat}', datetime(bucket_utc_ms / 1000, 'unixepoch', 'localtime')) AS period_key,
                    SUM((value_sum * 1.0 / sample_count) / 60.0 / 1000.0) AS energy_kwh,
                    SUM(value_sum) / SUM(sample_count) AS avg_watts,
                    MAX(maximum) AS peak_watts
                FROM telemetry_rollups_1m
                WHERE device_id = $device
                    AND metric = $metric
                    AND bucket_utc_ms >= $from
                    AND bucket_utc_ms <= $to
                GROUP BY period_key;
                """;
            AddRangeParameters(powerCmd, deviceId, fromMs, toMs);
            powerCmd.Parameters.AddWithValue("$metric", (int)TelemetryMetric.ActivePowerWatts);
            await using var reader = await powerCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = reader.GetString(0);
                var energyKwh = reader.GetDouble(1);
                var avgWatts = reader.GetDouble(2);
                var peakWatts = reader.GetDouble(3);
                powerData[key] = (energyKwh, avgWatts, peakWatts);
            }
        }

        // 2. ups_events から停電イベント件数を取得
        var outageData = new Dictionary<string, int>();
        await using (var outageCmd = connection.CreateCommand())
        {
            outageCmd.CommandText = $"""
                SELECT
                    strftime('{strftimeFormat}', datetime(timestamp_utc_ms / 1000, 'unixepoch', 'localtime')) AS period_key,
                    COUNT(*) AS outage_count
                FROM ups_events
                WHERE device_id = $device
                    AND timestamp_utc_ms >= $from
                    AND timestamp_utc_ms <= $to
                    AND event_type = $eventType
                GROUP BY period_key;
                """;
            AddRangeParameters(outageCmd, deviceId, fromMs, toMs);
            outageCmd.Parameters.AddWithValue("$eventType", (int)UpsEventType.PowerLost);
            await using var reader = await outageCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var key = reader.GetString(0);
                var count = reader.GetInt32(1);
                outageData[key] = count;
            }
        }

        var result = new List<EnergyReportItem>(periods.Count);
        foreach (var (key, start, end) in periods)
        {
            var outageCount = outageData.GetValueOrDefault(key, 0);
            if (powerData.TryGetValue(key, out var power))
            {
                var cost = power.EnergyKwh * electricityRatePerKwh;
                result.Add(new EnergyReportItem(
                    granularity,
                    start,
                    end,
                    power.EnergyKwh,
                    cost,
                    power.PeakWatts,
                    power.AvgWatts,
                    outageCount));
            }
            else
            {
                result.Add(new EnergyReportItem(
                    granularity,
                    start,
                    end,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    outageCount));
            }
        }

        return result;
    }

    public async Task<IReadOnlyList<DailyEnergyReportItem>> QueryDailyEnergyReportsAsync(
        string deviceId,
        int days,
        double electricityRatePerKwh,
        CancellationToken cancellationToken = default)
    {
        if (days <= 0)
        {
            return [];
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var fromDate = today.AddDays(-(days - 1));
        var localNow = DateTimeOffset.Now;
        var from = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), localNow.Offset);
        var to = new DateTimeOffset(today.ToDateTime(TimeOnly.MaxValue), localNow.Offset);

        var items = await QueryEnergyReportsAsync(
            deviceId,
            from,
            to,
            EnergyReportPeriod.Day,
            electricityRatePerKwh,
            cancellationToken).ConfigureAwait(false);

        return items.Select(i => new DailyEnergyReportItem(
            DateOnly.FromDateTime(i.PeriodStart.LocalDateTime),
            i.EnergyKwh,
            i.EstimatedCost,
            i.PeakWatts,
            i.AvgWatts,
            i.OutageCount)).ToList();
    }

    public async Task<PowerTroubleSummary> QueryPowerTroubleSummaryAsync(
        string deviceId,
        DateTimeOffset from,
        DateTimeOffset to,
        double lowVoltageSagThreshold = 95.0,
        double highVoltageSurgeThreshold = 105.0,
        CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        await FlushAsync(cancellationToken).ConfigureAwait(false);
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var fromMs = from.ToUniversalTime().ToUnixTimeMilliseconds();
        var toMs = to.ToUniversalTime().ToUnixTimeMilliseconds();

        // 1. サグ・サージ件数カウント (単一クエリ・単一スキャン)
        long sagCount = 0;
        long surgeCount = 0;
        await using (var voltCmd = connection.CreateCommand())
        {
            voltCmd.CommandText = """
                SELECT
                    COUNT(CASE WHEN input_voltage > 0 AND input_voltage < $sag THEN 1 END),
                    COUNT(CASE WHEN input_voltage > $surge THEN 1 END)
                FROM telemetry_samples
                WHERE device_id = $device
                    AND timestamp_utc_ms >= $from
                    AND timestamp_utc_ms <= $to;
                """;
            AddRangeParameters(voltCmd, deviceId, fromMs, toMs);
            voltCmd.Parameters.AddWithValue("$sag", lowVoltageSagThreshold);
            voltCmd.Parameters.AddWithValue("$surge", highVoltageSurgeThreshold);

            await using var reader = await voltCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                sagCount = reader.GetInt64(0);
                surgeCount = reader.GetInt64(1);
            }
        }

        // 2. トラブルイベントの取得
        var events = await QueryEventsAsync(connection, deviceId, fromMs, toMs, cancellationToken).ConfigureAwait(false);
        var troubleEvents = events.Where(e => e.Severity != UpsEventSeverity.Information).ToList();

        var stateChanges = await QueryStateChangesAsync(connection, deviceId, fromMs, toMs, cancellationToken).ConfigureAwait(false);
        var totalOutageMs = 0.0;
        var outageCount = events.Count(e => e.Type == UpsEventType.PowerLost);

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

        return new PowerTroubleSummary
        {
            TotalOutages = outageCount,
            TotalOutageDuration = TimeSpan.FromMilliseconds(totalOutageMs),
            VoltageSagCount = (int)sagCount,
            VoltageSurgeCount = (int)surgeCount,
            TroubleEvents = troubleEvents,
        };
    }
}

