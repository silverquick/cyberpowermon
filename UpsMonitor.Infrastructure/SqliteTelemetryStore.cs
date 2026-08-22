using System.Globalization;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public sealed partial class SqliteTelemetryStore : IUpsSnapshotSink, IUpsEventSink, IAsyncDisposable
{
    private static readonly TimeSpan HealthCheckpointInterval = TimeSpan.FromHours(24);
    private readonly string _databasePath;
    private readonly string _connectionString;
    private readonly TimeSpan _rawRetention;
    private readonly TimeSpan _rawUsageCheckpoint;
    private readonly Channel<HistoryWriteRequest> _writeQueue = Channel.CreateUnbounded<HistoryWriteRequest>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
    private readonly Dictionary<string, RawValueCheckpoint> _rawValueCheckpoints = [];
    private readonly Dictionary<string, BatteryHealthObservation> _healthCheckpoints = [];
    private readonly Dictionary<string, UpsPowerState> _lastStates = [];
    private SqliteConnection? _writeConnection;
    private Task? _writerTask;
    private DateTimeOffset _lastCleanup = DateTimeOffset.MinValue;
    private string? _lastDeviceId;
    private bool _initialized;
    private bool _disposed;

    public SqliteTelemetryStore(AppPaths paths, HistoryConfiguration configuration)
    {
        _databasePath = paths.TelemetryDatabaseFile;
        _rawRetention = TimeSpan.FromDays(configuration.RawRetentionDays);
        _rawUsageCheckpoint = TimeSpan.FromSeconds(configuration.RawUsageCheckpointSeconds);
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true,
            DefaultTimeout = 5,
        }.ToString();
    }

    public event Action<Exception>? StorageError;

    public string DatabasePath => _databasePath;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_initialized)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
        _writeConnection = new SqliteConnection(_connectionString);
        await _writeConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecuteSchemaAsync(_writeConnection, cancellationToken).ConfigureAwait(false);
        _initialized = true;
        _writerTask = Task.Run(ProcessWriteQueueAsync);
    }

    public Task WriteAsync(UpsSnapshot snapshot, CancellationToken cancellationToken)
    {
        Queue(new SnapshotWriteRequest(snapshot), cancellationToken);
        return Task.CompletedTask;
    }

    public Task WriteAsync(UpsEvent upsEvent, CancellationToken cancellationToken)
    {
        Queue(new EventWriteRequest(upsEvent), cancellationToken);
        return Task.CompletedTask;
    }

    public Task RecordBatteryHealthAsync(
        BatteryHealthObservation observation,
        CancellationToken cancellationToken = default)
    {
        Queue(new HealthWriteRequest(observation), cancellationToken);
        return Task.CompletedTask;
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        EnsureInitialized();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Queue(new FlushWriteRequest(completion), cancellationToken);
        await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _writeQueue.Writer.TryComplete();
        if (_writerTask is not null)
        {
            await _writerTask.ConfigureAwait(false);
        }

        if (_writeConnection is not null)
        {
            await _writeConnection.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void Queue(HistoryWriteRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureInitialized();
        if (!_writeQueue.Writer.TryWrite(request))
        {
            throw new InvalidOperationException("The telemetry history write queue is closed.");
        }
    }

    private void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!_initialized)
        {
            throw new InvalidOperationException("The telemetry history store has not been initialized.");
        }
    }

    private async Task ProcessWriteQueueAsync()
    {
        try
        {
            await foreach (var request in _writeQueue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    switch (request)
                    {
                        case SnapshotWriteRequest snapshot:
                            await WriteSnapshotCoreAsync(snapshot.Snapshot).ConfigureAwait(false);
                            break;
                        case EventWriteRequest upsEvent:
                            await WriteEventCoreAsync(upsEvent.Event).ConfigureAwait(false);
                            break;
                        case HealthWriteRequest health:
                            await WriteHealthCoreAsync(health.Observation).ConfigureAwait(false);
                            break;
                        case FlushWriteRequest flush:
                            flush.Completion.TrySetResult();
                            break;
                    }
                }
                catch (Exception exception)
                {
                    if (request is FlushWriteRequest flush)
                    {
                        flush.Completion.TrySetException(exception);
                    }

                    StorageError?.Invoke(exception);
                }
            }
        }
        catch (Exception exception)
        {
            StorageError?.Invoke(exception);
        }
    }

    private async Task WriteSnapshotCoreAsync(UpsSnapshot snapshot)
    {
        var connection = _writeConnection!;
        var deviceId = snapshot.Device is { } device
            ? UpsDeviceIdentity.Create(device)
            : _lastDeviceId;
        if (deviceId is null)
        {
            return;
        }

        _lastDeviceId = deviceId;
        var timestampMilliseconds = snapshot.Timestamp.ToUniversalTime().ToUnixTimeMilliseconds();
        var state = UpsPowerStateEvaluator.Evaluate(snapshot);
        var telemetry = UpsTelemetryValidator.Normalize(snapshot);
        var values = MetricValues(snapshot, telemetry);

        await using var transaction = connection.BeginTransaction();
        await InsertSnapshotAsync(
            connection,
            transaction,
            deviceId,
            timestampMilliseconds,
            snapshot,
            telemetry,
            state).ConfigureAwait(false);

        foreach (var (metric, value) in values)
        {
            if (value is not { } numericValue)
            {
                continue;
            }

            await UpsertRollupAsync(
                connection,
                transaction,
                deviceId,
                timestampMilliseconds,
                metric,
                numericValue).ConfigureAwait(false);
        }

        if (!_lastStates.TryGetValue(deviceId, out var previousState) || previousState != state)
        {
            await InsertStateChangeAsync(
                connection,
                transaction,
                deviceId,
                timestampMilliseconds,
                state).ConfigureAwait(false);
            _lastStates[deviceId] = state;
        }

        foreach (var item in snapshot.Telemetry)
        {
            if (!item.HasValue || item.NumericValue is not { } value || !double.IsFinite(value))
            {
                continue;
            }

            var checkpointKey = $"{deviceId}:{item.Key}";
            var changed = !_rawValueCheckpoints.TryGetValue(checkpointKey, out var previous)
                || previous.NumericValue != value
                || previous.RawValue != item.RawValue;
            var checkpointDue = previous is null
                || snapshot.Timestamp - previous.Timestamp >= _rawUsageCheckpoint;
            if (!changed && !checkpointDue)
            {
                continue;
            }

            await InsertRawValueAsync(
                connection,
                transaction,
                deviceId,
                timestampMilliseconds,
                item,
                value).ConfigureAwait(false);
            _rawValueCheckpoints[checkpointKey] = new(value, item.RawValue, snapshot.Timestamp);
        }

        await transaction.CommitAsync().ConfigureAwait(false);

        if (snapshot.Timestamp - _lastCleanup >= TimeSpan.FromHours(1))
        {
            await CleanupAsync(connection, snapshot.Timestamp).ConfigureAwait(false);
            _lastCleanup = snapshot.Timestamp;
        }
    }

    private static async Task InsertSnapshotAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string deviceId,
        long timestampMilliseconds,
        UpsSnapshot snapshot,
        UpsTelemetry telemetry,
        UpsPowerState state)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT OR REPLACE INTO telemetry_samples (
                device_id, timestamp_utc_ms, is_connected, power_state,
                input_voltage, output_voltage, battery_voltage, battery_percent,
                runtime_seconds, load_percent, active_power_watts, apparent_power_va,
                frequency_hz, temperature_c, ac_present, charging, discharging,
                low_battery, shutdown_imminent, overload, boost)
            VALUES (
                $device, $timestamp, $connected, $state,
                $inputVoltage, $outputVoltage, $batteryVoltage, $batteryPercent,
                $runtime, $load, $activePower, $apparentPower,
                $frequency, $temperature, $acPresent, $charging, $discharging,
                $lowBattery, $shutdownImminent, $overload, $boost);
            """;
        command.Parameters.AddWithValue("$device", deviceId);
        command.Parameters.AddWithValue("$timestamp", timestampMilliseconds);
        command.Parameters.AddWithValue("$connected", snapshot.IsConnected ? 1 : 0);
        command.Parameters.AddWithValue("$state", (int)state);
        AddNullable(command, "$inputVoltage", ValidNumber(snapshot.InputVoltage, 0, 1_000));
        AddNullable(command, "$outputVoltage", ValidNumber(snapshot.OutputVoltage, 0, 1_000));
        AddNullable(command, "$batteryVoltage", Validated(telemetry.BatteryVoltage));
        AddNullable(command, "$batteryPercent", Validated(telemetry.BatteryChargePercent));
        AddNullable(command, "$runtime", telemetry.RuntimeRemaining.IsValid
            ? telemetry.RuntimeRemaining.Value?.TotalSeconds
            : null);
        AddNullable(command, "$load", Validated(telemetry.LoadPercent));
        AddNullable(command, "$activePower", Validated(telemetry.ActivePowerWatts));
        AddNullable(command, "$apparentPower", ValidNumber(snapshot.ApparentPower, 0, 1_000_000));
        AddNullable(command, "$frequency", ValidNumber(snapshot.Frequency, 0, 1_000));
        AddNullable(command, "$temperature", ValidNumber(snapshot.Temperature, -100, 300));
        AddNullable(command, "$acPresent", BooleanValue(snapshot.AcPresent));
        AddNullable(command, "$charging", BooleanValue(snapshot.Charging));
        AddNullable(command, "$discharging", BooleanValue(snapshot.Discharging));
        AddNullable(command, "$lowBattery", BooleanValue(snapshot.LowBattery));
        AddNullable(command, "$shutdownImminent", BooleanValue(snapshot.ShutdownImminent));
        AddNullable(command, "$overload", BooleanValue(snapshot.Overload));
        AddNullable(command, "$boost", BooleanValue(snapshot.Boost));
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task UpsertRollupAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string deviceId,
        long timestampMilliseconds,
        TelemetryMetric metric,
        double value)
    {
        var bucket = timestampMilliseconds - (timestampMilliseconds % 60_000);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO telemetry_rollups_1m (
                device_id, bucket_utc_ms, metric, minimum, maximum, value_sum,
                sample_count, last_value, last_timestamp_utc_ms)
            VALUES ($device, $bucket, $metric, $value, $value, $value, 1, $value, $timestamp)
            ON CONFLICT(device_id, bucket_utc_ms, metric) DO UPDATE SET
                minimum = MIN(minimum, excluded.minimum),
                maximum = MAX(maximum, excluded.maximum),
                value_sum = value_sum + excluded.value_sum,
                sample_count = sample_count + 1,
                last_value = excluded.last_value,
                last_timestamp_utc_ms = excluded.last_timestamp_utc_ms;
            """;
        command.Parameters.AddWithValue("$device", deviceId);
        command.Parameters.AddWithValue("$bucket", bucket);
        command.Parameters.AddWithValue("$metric", (int)metric);
        command.Parameters.AddWithValue("$value", value);
        command.Parameters.AddWithValue("$timestamp", timestampMilliseconds);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task InsertStateChangeAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string deviceId,
        long timestampMilliseconds,
        UpsPowerState state)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO ups_state_changes (device_id, timestamp_utc_ms, power_state)
            VALUES ($device, $timestamp, $state);
            """;
        command.Parameters.AddWithValue("$device", deviceId);
        command.Parameters.AddWithValue("$timestamp", timestampMilliseconds);
        command.Parameters.AddWithValue("$state", (int)state);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task InsertRawValueAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string deviceId,
        long timestampMilliseconds,
        UpsTelemetryItem item,
        double value)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO raw_telemetry_values (
                device_id, timestamp_utc_ms, metric_key, usage_page, usage,
                report_type, report_id, numeric_value, raw_value, unit_symbol,
                usage_name, collection_path, is_vendor_defined)
            VALUES (
                $device, $timestamp, $key, $page, $usage,
                $reportType, $reportId, $value, $raw, $unit,
                $name, $collection, $vendorDefined);
            """;
        command.Parameters.AddWithValue("$device", deviceId);
        command.Parameters.AddWithValue("$timestamp", timestampMilliseconds);
        command.Parameters.AddWithValue("$key", item.Key);
        command.Parameters.AddWithValue("$page", item.UsagePage);
        command.Parameters.AddWithValue("$usage", item.Usage);
        command.Parameters.AddWithValue("$reportType", item.ReportType);
        command.Parameters.AddWithValue("$reportId", item.ReportId);
        command.Parameters.AddWithValue("$value", value);
        AddNullable(command, "$raw", item.RawValue);
        AddNullable(command, "$unit", item.UnitSymbol);
        command.Parameters.AddWithValue("$name", item.UsageName);
        command.Parameters.AddWithValue("$collection", item.CollectionPath);
        command.Parameters.AddWithValue("$vendorDefined", item.IsVendorDefined ? 1 : 0);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task WriteEventCoreAsync(UpsEvent upsEvent)
    {
        if (_lastDeviceId is null)
        {
            return;
        }

        await using var command = _writeConnection!.CreateCommand();
        command.CommandText = """
            INSERT INTO ups_events (
                device_id, timestamp_utc_ms, event_type, message, previous_state, current_state)
            VALUES ($device, $timestamp, $type, $message, $previous, $current);
            """;
        command.Parameters.AddWithValue("$device", _lastDeviceId);
        command.Parameters.AddWithValue("$timestamp", upsEvent.Timestamp.ToUniversalTime().ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("$type", (int)upsEvent.Type);
        command.Parameters.AddWithValue("$message", upsEvent.Message);
        command.Parameters.AddWithValue("$previous", (int)upsEvent.PreviousState);
        command.Parameters.AddWithValue("$current", (int)upsEvent.CurrentState);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task WriteHealthCoreAsync(BatteryHealthObservation observation)
    {
        var shouldWrite = !_healthCheckpoints.TryGetValue(observation.DeviceId, out var previous)
            || !HealthEquivalent(previous, observation)
            || observation.Timestamp - previous.Timestamp >= HealthCheckpointInterval;
        if (!shouldWrite)
        {
            return;
        }

        await using var command = _writeConnection!.CreateCommand();
        command.CommandText = """
            INSERT INTO battery_health_observations (
                device_id, timestamp_utc_ms, health_percent, relative_performance_percent,
                status, method, confidence, anchor_source, vendor_category, replacement_status)
            VALUES (
                $device, $timestamp, $health, $relative,
                $status, $method, $confidence, $source, $category, $replacement);
            """;
        command.Parameters.AddWithValue("$device", observation.DeviceId);
        command.Parameters.AddWithValue("$timestamp", observation.Timestamp.ToUniversalTime().ToUnixTimeMilliseconds());
        AddNullable(command, "$health", observation.HealthPercent);
        AddNullable(command, "$relative", observation.RelativePerformancePercent);
        command.Parameters.AddWithValue("$status", (int)observation.Status);
        command.Parameters.AddWithValue("$method", (int)observation.Method);
        command.Parameters.AddWithValue("$confidence", (int)observation.Confidence);
        AddNullable(command, "$source", observation.AnchorSource);
        command.Parameters.AddWithValue("$category", (int)observation.VendorCategory);
        command.Parameters.AddWithValue("$replacement", (int)observation.ReplacementStatus);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        _healthCheckpoints[observation.DeviceId] = observation;
    }

    private async Task CleanupAsync(SqliteConnection connection, DateTimeOffset now)
    {
        var cutoff = (now - _rawRetention).ToUniversalTime().ToUnixTimeMilliseconds();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM telemetry_samples WHERE timestamp_utc_ms < $cutoff;
            DELETE FROM raw_telemetry_values WHERE timestamp_utc_ms < $cutoff;
            PRAGMA wal_checkpoint(PASSIVE);
            """;
        command.Parameters.AddWithValue("$cutoff", cutoff);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static IReadOnlyDictionary<TelemetryMetric, double?> MetricValues(
        UpsSnapshot snapshot,
        UpsTelemetry telemetry) => new Dictionary<TelemetryMetric, double?>
        {
            [TelemetryMetric.InputVoltage] = ValidNumber(snapshot.InputVoltage, 0, 1_000),
            [TelemetryMetric.OutputVoltage] = ValidNumber(snapshot.OutputVoltage, 0, 1_000),
            [TelemetryMetric.BatteryVoltage] = Validated(telemetry.BatteryVoltage),
            [TelemetryMetric.BatteryPercent] = Validated(telemetry.BatteryChargePercent),
            [TelemetryMetric.RuntimeMinutes] = telemetry.RuntimeRemaining.IsValid
                ? telemetry.RuntimeRemaining.Value?.TotalMinutes
                : null,
            [TelemetryMetric.LoadPercent] = Validated(telemetry.LoadPercent),
            [TelemetryMetric.ActivePowerWatts] = Validated(telemetry.ActivePowerWatts),
            [TelemetryMetric.ApparentPowerVoltAmperes] = ValidNumber(snapshot.ApparentPower, 0, 1_000_000),
            [TelemetryMetric.FrequencyHertz] = ValidNumber(snapshot.Frequency, 0, 1_000),
            [TelemetryMetric.TemperatureCelsius] = ValidNumber(snapshot.Temperature, -100, 300),
        };

    private static bool HealthEquivalent(BatteryHealthObservation left, BatteryHealthObservation right) =>
        left.HealthPercent == right.HealthPercent
        && left.RelativePerformancePercent == right.RelativePerformancePercent
        && left.Status == right.Status
        && left.Method == right.Method
        && left.Confidence == right.Confidence
        && left.AnchorSource == right.AnchorSource
        && left.VendorCategory == right.VendorCategory
        && left.ReplacementStatus == right.ReplacementStatus;

    private static double? Validated(ValidatedTelemetryValue<double> value) =>
        value.IsValid ? value.Value : null;

    private static double? ValidNumber(double? value, double minimum, double maximum) =>
        value is { } number && double.IsFinite(number) && number >= minimum && number <= maximum
            ? number
            : null;

    private static int? BooleanValue(bool? value) => value switch
    {
        true => 1,
        false => 0,
        _ => null,
    };

    private static void AddNullable(SqliteCommand command, string name, object? value) =>
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);

    private static async Task ExecuteSchemaAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            PRAGMA journal_mode=WAL;
            PRAGMA synchronous=NORMAL;
            PRAGMA foreign_keys=ON;
            PRAGMA busy_timeout=5000;

            CREATE TABLE IF NOT EXISTS telemetry_samples (
                id INTEGER PRIMARY KEY,
                device_id TEXT NOT NULL,
                timestamp_utc_ms INTEGER NOT NULL,
                is_connected INTEGER NOT NULL,
                power_state INTEGER NOT NULL,
                input_voltage REAL,
                output_voltage REAL,
                battery_voltage REAL,
                battery_percent REAL,
                runtime_seconds REAL,
                load_percent REAL,
                active_power_watts REAL,
                apparent_power_va REAL,
                frequency_hz REAL,
                temperature_c REAL,
                ac_present INTEGER,
                charging INTEGER,
                discharging INTEGER,
                low_battery INTEGER,
                shutdown_imminent INTEGER,
                overload INTEGER,
                boost INTEGER,
                UNIQUE(device_id, timestamp_utc_ms)
            );
            CREATE INDEX IF NOT EXISTS ix_telemetry_samples_device_time
                ON telemetry_samples(device_id, timestamp_utc_ms);

            CREATE TABLE IF NOT EXISTS telemetry_rollups_1m (
                device_id TEXT NOT NULL,
                bucket_utc_ms INTEGER NOT NULL,
                metric INTEGER NOT NULL,
                minimum REAL NOT NULL,
                maximum REAL NOT NULL,
                value_sum REAL NOT NULL,
                sample_count INTEGER NOT NULL,
                last_value REAL NOT NULL,
                last_timestamp_utc_ms INTEGER NOT NULL,
                PRIMARY KEY(device_id, bucket_utc_ms, metric)
            );
            CREATE INDEX IF NOT EXISTS ix_rollups_device_metric_time
                ON telemetry_rollups_1m(device_id, metric, bucket_utc_ms);

            CREATE TABLE IF NOT EXISTS raw_telemetry_values (
                id INTEGER PRIMARY KEY,
                device_id TEXT NOT NULL,
                timestamp_utc_ms INTEGER NOT NULL,
                metric_key TEXT NOT NULL,
                usage_page INTEGER NOT NULL,
                usage INTEGER NOT NULL,
                report_type TEXT NOT NULL,
                report_id INTEGER NOT NULL,
                numeric_value REAL NOT NULL,
                raw_value INTEGER,
                unit_symbol TEXT,
                usage_name TEXT NOT NULL,
                collection_path TEXT NOT NULL,
                is_vendor_defined INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_raw_values_device_metric_time
                ON raw_telemetry_values(device_id, metric_key, timestamp_utc_ms);

            CREATE TABLE IF NOT EXISTS ups_events (
                id INTEGER PRIMARY KEY,
                device_id TEXT NOT NULL,
                timestamp_utc_ms INTEGER NOT NULL,
                event_type INTEGER NOT NULL,
                message TEXT NOT NULL,
                previous_state INTEGER NOT NULL,
                current_state INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_events_device_time
                ON ups_events(device_id, timestamp_utc_ms);

            CREATE TABLE IF NOT EXISTS ups_state_changes (
                id INTEGER PRIMARY KEY,
                device_id TEXT NOT NULL,
                timestamp_utc_ms INTEGER NOT NULL,
                power_state INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_state_changes_device_time
                ON ups_state_changes(device_id, timestamp_utc_ms);

            CREATE TABLE IF NOT EXISTS battery_health_observations (
                id INTEGER PRIMARY KEY,
                device_id TEXT NOT NULL,
                timestamp_utc_ms INTEGER NOT NULL,
                health_percent REAL,
                relative_performance_percent REAL,
                status INTEGER NOT NULL,
                method INTEGER NOT NULL,
                confidence INTEGER NOT NULL,
                anchor_source TEXT,
                vendor_category INTEGER NOT NULL,
                replacement_status INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_health_device_time
                ON battery_health_observations(device_id, timestamp_utc_ms);

            PRAGMA user_version=1;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private abstract record HistoryWriteRequest;

    private sealed record SnapshotWriteRequest(UpsSnapshot Snapshot) : HistoryWriteRequest;

    private sealed record EventWriteRequest(UpsEvent Event) : HistoryWriteRequest;

    private sealed record HealthWriteRequest(BatteryHealthObservation Observation) : HistoryWriteRequest;

    private sealed record FlushWriteRequest(TaskCompletionSource Completion) : HistoryWriteRequest;

    private sealed record RawValueCheckpoint(
        double NumericValue,
        long? RawValue,
        DateTimeOffset Timestamp);
}
