using System.Text;
using UpsMonitor.Core;
using UpsMonitor.Hid;

Console.OutputEncoding = Encoding.UTF8;
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
    Console.WriteLine($"  State        : {UpsPowerStateEvaluator.Evaluate(snapshot)}");
    Console.WriteLine($"  Battery      : {Percent(snapshot.BatteryPercent)}");
    Console.WriteLine($"  Batt. health : {Percent(snapshot.BatteryHealthPercent)} (not exposed by this UPS HID)");
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
