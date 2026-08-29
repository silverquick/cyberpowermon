using System.Text;
using Microsoft.Data.Sqlite;
using UpsMonitor.Core;
using UpsMonitor.Hid;
using UpsMonitor.Infrastructure;

Console.OutputEncoding = Encoding.UTF8;
if (args.Contains("--health", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine("================================================================================");
    Console.WriteLine("               UPS BATTERY HEALTH & TELEMETRY ANALYSIS REPORT                   ");
    Console.WriteLine("================================================================================");
    Console.WriteLine($"Generated at: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}\n");

    var paths = new AppPaths();
    Console.WriteLine($"Configuration File : {paths.ConfigurationFile}");
    Console.WriteLine($"Telemetry DB       : {paths.TelemetryDatabaseFile}");
    Console.WriteLine($"Logs Directory     : {paths.LogsDirectory}\n");

    var configStore = new JsonConfigurationStore(paths);
    var config = await configStore.LoadAsync();
    Console.WriteLine("--- 1. CONFIGURATION & BATTERY HEALTH PROFILES ---");
    Console.WriteLine($"Warning Threshold  : {config.BatteryHealth.WarningThresholdPercent} %");
    Console.WriteLine($"Critical Threshold : {config.BatteryHealth.CriticalThresholdPercent} %");
    Console.WriteLine($"Load Tolerance     : {config.BatteryHealth.ComparableLoadTolerancePercent} %");
    foreach (var profile in config.BatteryHealth.Profiles)
    {
        Console.WriteLine($"Profile Device ID    : {profile.DeviceId}");
        Console.WriteLine($"  Baseline Kind      : {profile.RuntimeBaselineKind}");
        Console.WriteLine($"  Anchor SOH         : {profile.AnchorHealthPercent?.ToString() ?? "N/A"} % (Source: {profile.AnchorSource ?? "N/A"})");
        Console.WriteLine($"  Baseline Recorded  : {profile.BaselineRecordedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A"}");
        Console.WriteLine($"  Vendor Category    : {profile.VendorHealthCategory}");
        foreach (var b in profile.RuntimeBaselines)
        {
            Console.WriteLine($"  -> Baseline Point  : Load={b.LoadPercent}% -> Runtime={b.Runtime.TotalMinutes:F1} min ({b.Runtime.TotalSeconds} s) recorded {b.MeasuredAt:yyyy-MM-dd HH:mm}");
        }
    }

    if (File.Exists(paths.TelemetryDatabaseFile))
    {
        using var conn = new SqliteConnection($"Data Source={paths.TelemetryDatabaseFile};Mode=ReadOnly");
        conn.Open();

        Console.WriteLine("\n--- 2. BATTERY HEALTH HISTORY (LATEST 15 EVALUATIONS) ---");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT 
                    datetime(timestamp_utc_ms / 1000, 'unixepoch', 'localtime'),
                    health_percent,
                    relative_performance_percent,
                    status,
                    method,
                    confidence,
                    anchor_source,
                    vendor_category,
                    replacement_status
                FROM battery_health_observations 
                ORDER BY timestamp_utc_ms DESC 
                LIMIT 15;";
            using var reader = cmd.ExecuteReader();
            var hasHealth = false;
            while (reader.Read())
            {
                hasHealth = true;
                var ts = reader.GetString(0);
                var health = reader.IsDBNull(1) ? "N/A" : $"{reader.GetDouble(1):F1}%";
                var rel = reader.IsDBNull(2) ? "N/A" : $"{reader.GetDouble(2):F1}%";
                var status = (BatteryHealthStatus)reader.GetInt32(3);
                var method = (BatteryHealthMethod)reader.GetInt32(4);
                var conf = (BatteryHealthConfidence)reader.GetInt32(5);
                var anchor = reader.IsDBNull(6) ? "-" : reader.GetString(6);
                var repl = (BatteryReplacementStatus)reader.GetInt32(8);
                Console.WriteLine($"[{ts}] SOH: {health,6} | RelPerf: {rel,6} | Status: {status,-10} | Method: {method,-20} | Conf: {conf} | Repl: {repl} | Anchor: {anchor}");
            }
            if (!hasHealth)
            {
                Console.WriteLine("No health observations recorded in database.");
            }
        }

        Console.WriteLine("\n--- 3. RECENT TELEMETRY STATS & DRIFT ---");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT 
                    COUNT(*),
                    MIN(battery_voltage), AVG(battery_voltage), MAX(battery_voltage),
                    MIN(battery_percent), AVG(battery_percent), MAX(battery_percent),
                    MIN(runtime_seconds)/60.0, AVG(runtime_seconds)/60.0, MAX(runtime_seconds)/60.0,
                    MIN(load_percent), AVG(load_percent), MAX(load_percent),
                    MIN(active_power_watts), AVG(active_power_watts), MAX(active_power_watts)
                FROM telemetry_samples;";
            using var reader = cmd.ExecuteReader();
            if (reader.Read() && reader.GetInt64(0) > 0)
            {
                var sampleCount = reader.GetInt64(0);
                Console.WriteLine($"Total Samples Stored : {sampleCount:N0}");
                Console.WriteLine($"Battery Voltage (V)  : Min={reader.GetDouble(1):F2} V | Avg={reader.GetDouble(2):F2} V | Max={reader.GetDouble(3):F2} V");
                Console.WriteLine($"Per-Cell Voltage (V) : Min={reader.GetDouble(1)/12.0:F3} V | Avg={reader.GetDouble(2)/12.0:F3} V | Max={reader.GetDouble(3)/12.0:F3} V");
                Console.WriteLine($"Battery Charge (%)   : Min={reader.GetDouble(4):F1} % | Avg={reader.GetDouble(5):F1} % | Max={reader.GetDouble(6):F1} %");
                Console.WriteLine($"Runtime (min)        : Min={reader.GetDouble(7):F1} m | Avg={reader.GetDouble(8):F1} m | Max={reader.GetDouble(9):F1} m");
                Console.WriteLine($"Load Percent (%)     : Min={reader.GetDouble(10):F1} % | Avg={reader.GetDouble(11):F1} % | Max={reader.GetDouble(12):F1} %");
                Console.WriteLine($"Active Power (W)     : Min={reader.GetDouble(13):F1} W | Avg={reader.GetDouble(14):F1} W | Max={reader.GetDouble(15):F1} W");
            }
        }

        Console.WriteLine("\n--- 4. BATTERY VOLTAGE & DISCHARGE DRIFT ANALYSIS ---");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT 
                    MIN(minimum), AVG(value_sum/sample_count), MAX(maximum) 
                FROM telemetry_rollups_1m 
                WHERE metric = 2;"; // 2 = BatteryVoltage
            using var reader = cmd.ExecuteReader();
            if (reader.Read() && !reader.IsDBNull(0))
            {
                var minV = reader.GetDouble(0);
                var avgV = reader.GetDouble(1);
                var maxV = reader.GetDouble(2);
                Console.WriteLine($"Battery Voltage (Total) : Min = {minV:F2} V, Avg = {avgV:F2} V, Max = {maxV:F2} V");
                Console.WriteLine($"Per-cell (12 cells)     : Min = {minV/12.0:F3} V/cell, Avg = {avgV/12.0:F3} V/cell, Max = {maxV/12.0:F3} V/cell");
                Console.WriteLine($"Float Charge Stability  : {(maxV - minV < 0.5 ? "Very Stable (Good float charge condition)" : $"Voltage fluctuation observed (Δ = {maxV - minV:F2} V)")}");
            }
        }

        Console.WriteLine("\n--- 5. OUTAGE & BATTERY EVENTS HISTORY ---");
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
                SELECT datetime(timestamp_utc_ms / 1000, 'unixepoch', 'localtime'), event_type, message 
                FROM ups_events 
                ORDER BY timestamp_utc_ms DESC 
                LIMIT 20;";
            using var reader = cmd.ExecuteReader();
            var hasEvents = false;
            while (reader.Read())
            {
                hasEvents = true;
                var eventType = (UpsEventType)reader.GetInt32(1);
                Console.WriteLine($"[{reader.GetString(0)}] {eventType,-18} : {reader.GetString(2)}");
            }
            if (!hasEvents)
            {
                Console.WriteLine("No event history recorded in database.");
            }
        }
    }
    else
    {
        Console.WriteLine("\nTelemetry database file not found yet.");
    }

    Console.WriteLine("\n================================================================================");
    return;
}

var showDescriptor = args.Contains("--descriptor", StringComparer.OrdinalIgnoreCase);
Console.WriteLine("UPS Monitor HID Probe");
Console.WriteLine("Enumerating HID top-level collections with Usage Page 0x84 / Usage 0x04 (UPS)...");
Console.WriteLine();

IReadOnlyList<HidUpsDeviceDescription> devices;
try
{
    devices = HidDeviceEnumerator.EnumerateUpsDevices();
}
catch (Exception exception)
{
    Console.Error.WriteLine($"HID enumeration failed: {exception.Message}");
    Environment.ExitCode = 1;
    return;
}

if (devices.Count == 0)
{
    Console.WriteLine("No compatible USB HID UPS is currently connected.");
    return;
}

for (var index = 0; index < devices.Count; index++)
{
    var description = devices[index];
    var device = description.Device;
    Console.WriteLine($"[{index + 1}] {device.DisplayName}");
    Console.WriteLine($"  Manufacturer : {Value(device.Manufacturer)}");
    Console.WriteLine($"  Product      : {Value(device.Product)}");
    Console.WriteLine($"  Serial       : {Value(device.SerialNumber)}");
    Console.WriteLine($"  VID / PID    : {device.VendorId:X4} / {device.ProductId:X4}");
    Console.WriteLine($"  Usage        : 0x{device.UsagePage:X2} / 0x{device.Usage:X2}");
    Console.WriteLine($"  Report bytes : input={device.InputReportByteLength}, feature={device.FeatureReportByteLength}");
    Console.WriteLine($"  Data items   : {description.DescriptorItems.Count}");
    Console.WriteLine();

    if (!showDescriptor)
    {
        continue;
    }

    foreach (var item in description.DescriptorItems
                 .Where(item => item.UsagePage is 0x84 or 0x85)
                 .OrderBy(item => item.ReportType)
                 .ThenBy(item => item.ReportId)
                 .ThenBy(item => item.UsagePage)
                 .ThenBy(item => item.Usage))
    {
        Console.WriteLine(
            $"    {item.ReportType,-7} ID={item.ReportId,3} Page=0x{item.UsagePage:X2} Usage=0x{item.Usage:X2} " +
            $"Bits={item.BitSize} Count={item.ReportCount} Logical={item.LogicalMinimum}..{item.LogicalMaximum} " +
            $"Unit=0x{item.Unit:X8} Exp={item.UnitExponent}");
    }
}

Console.WriteLine();
Console.WriteLine("Reading the first compatible UPS...");
await using var provider = new WindowsHidUpsProvider();
try
{
    if (!await provider.ConnectAsync(CancellationToken.None))
    {
        Console.WriteLine("The UPS disappeared before it could be opened.");
        return;
    }

    // Allow an interrupt-IN report to arrive; feature reports are also queried directly.
    await Task.Delay(500);
    var snapshot = await provider.ReadSnapshotAsync(CancellationToken.None);
    PrintSnapshot(snapshot);
    if (showDescriptor)
    {
        PrintTelemetry(snapshot.Telemetry);
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine($"UPS read failed: {exception.Message}");
    Environment.ExitCode = 1;
}

static void PrintSnapshot(UpsSnapshot snapshot)
{
    var telemetry = UpsTelemetryValidator.Normalize(snapshot);
    var health = BatteryHealthCalculator.Calculate(telemetry, profile: null);
    Console.WriteLine($"  State        : {UpsPowerStateEvaluator.Evaluate(snapshot)}");
    Console.WriteLine($"  Battery      : {Percent(telemetry.BatteryChargePercent.IsValid ? telemetry.BatteryChargePercent.Value : null)}");
    Console.WriteLine($"  Batt. health : {Percent(health.HealthPercent)} ({health.Status}, {health.Confidence}, {health.PrimaryMethod})");
    Console.WriteLine($"  Runtime      : {Duration(snapshot.RuntimeRemaining)}");
    Console.WriteLine($"  AC present   : {Boolean(snapshot.AcPresent)}");
    Console.WriteLine($"  Charging     : {Boolean(snapshot.Charging)}");
    Console.WriteLine($"  Discharging  : {Boolean(snapshot.Discharging)}");
    Console.WriteLine($"  Low battery  : {Boolean(snapshot.LowBattery)}");
    Console.WriteLine($"  Critical     : {Boolean(snapshot.ShutdownImminent)}");
    Console.WriteLine($"  Overload     : {Boolean(snapshot.Overload)}");
    Console.WriteLine($"  Voltage      : {Number(snapshot.Voltage, "V")}");
    Console.WriteLine($"  Current      : {Number(snapshot.Current, "A")}");
    Console.WriteLine($"  Frequency    : {Number(snapshot.Frequency, "Hz")}");
    Console.WriteLine($"  Temperature  : {Number(snapshot.Temperature, "°C")}");
    Console.WriteLine($"  Input voltage: {Number(snapshot.InputVoltage, "V")}");
    Console.WriteLine($"  Output volt. : {Number(snapshot.OutputVoltage, "V")}");
    Console.WriteLine($"  Battery volt.: {Number(snapshot.BatteryVoltage, "V")}");
    Console.WriteLine($"  Load         : {Number(snapshot.PercentLoad, "%")}");
    Console.WriteLine($"  Active power : {Number(snapshot.ActivePower, "W")}");
    Console.WriteLine($"  Apparent pwr.: {Number(snapshot.ApparentPower, "VA")}");
    Console.WriteLine($"  Telemetry    : {snapshot.Telemetry.Count} descriptor items");
    Console.WriteLine($"  Data quality : {telemetry.Issues.Count} invalid value(s) ignored");
    foreach (var reason in health.Reasons)
    {
        Console.WriteLine($"  Health basis : {reason.Code}");
    }
}

static void PrintTelemetry(IReadOnlyList<UpsTelemetryItem> telemetry)
{
    Console.WriteLine();
    Console.WriteLine("All descriptor-backed telemetry:");
    foreach (var item in telemetry)
    {
        Console.WriteLine(
            $"  {item.UsagePage:X4}:{item.Usage:X4} {item.UsageName,-30} " +
            $"{item.DisplayValue,-24} {item.ReportType}#{item.ReportId} " +
            $"Link={item.LinkCollection} [{item.CollectionPath}] Raw={item.RawValue?.ToString() ?? "N/A"}");
    }
}

static string Value(string? value) => string.IsNullOrWhiteSpace(value) ? "N/A" : value;
static string Boolean(bool? value) => value?.ToString() ?? "N/A";
static string Percent(double? value) => value is { } number ? $"{number:0.#} %" : "N/A";
static string Duration(TimeSpan? value) => value is { } duration ? $"{duration.TotalSeconds:0} sec" : "N/A";
static string Number(double? value, string unit) => value is { } number ? $"{number:0.###} {unit}" : "N/A";
