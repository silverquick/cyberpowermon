using System.Globalization;
using UpsMonitor.Core;

namespace UpsMonitor.Hid;

internal static class UpsHidMapper
{
    private const ushort PowerDevicePage = 0x84;
    private const ushort BatterySystemPage = 0x85;
    private const uint SecondsUnit = 0x00001001;
    private const uint KelvinUnit = 0x00010001;

    internal static UpsSnapshot Map(
        UpsDeviceInfo device,
        HidDescriptor descriptor,
        IReadOnlyList<HidDataValue> values)
    {
        var valueByCapability = new Dictionary<HidCapability, HidDataValue>(values.Count);
        var valuesByPageUsage = new Dictionary<(ushort Page, ushort Usage), List<HidDataValue>>();

        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i];
            valueByCapability[value.Capability] = value;

            var key = (value.Capability.UsagePage, value.Capability.Usage);
            if (!valuesByPageUsage.TryGetValue(key, out var list))
            {
                list = [];
                valuesByPageUsage[key] = list;
            }
            list.Add(value);
        }

        var capabilities = descriptor.Capabilities;
        var telemetry = new UpsTelemetryItem[capabilities.Count];
        for (var i = 0; i < capabilities.Count; i++)
        {
            var capability = capabilities[i];
            valueByCapability.TryGetValue(capability, out var value);
            telemetry[i] = ToTelemetry(capability, value);
        }

        Array.Sort(telemetry, static (a, b) =>
        {
            var cmp = a.UsagePage.CompareTo(b.UsagePage);
            if (cmp != 0) return cmp;
            cmp = string.Compare(a.CollectionPath, b.CollectionPath, StringComparison.OrdinalIgnoreCase);
            if (cmp != 0) return cmp;
            cmp = a.Usage.CompareTo(b.Usage);
            if (cmp != 0) return cmp;
            cmp = string.Compare(a.ReportType, b.ReportType, StringComparison.Ordinal);
            if (cmp != 0) return cmp;
            return a.ReportId.CompareTo(b.ReportId);
        });

        var relativeCharge = Get(BatterySystemPage, 0x64);
        var absoluteCharge = Get(BatterySystemPage, 0x65);
        var remainingCapacity = Get(BatterySystemPage, 0x66);
        var batteryPercent = relativeCharge?.ScaledValue
            ?? absoluteCharge?.ScaledValue
            ?? (remainingCapacity?.Capability.LogicalMaximum <= 100 ? remainingCapacity.ScaledValue : null);

        var batteryVoltage = Get(PowerDevicePage, 0x30, "Battery")?.ScaledValue
            ?? Get(PowerDevicePage, 0x30, "PowerSummary")?.ScaledValue;
        var inputVoltage = Get(PowerDevicePage, 0x30, "Input")?.ScaledValue;
        var outputVoltage = Get(PowerDevicePage, 0x30, "Output")?.ScaledValue;
        var anyVoltage = Get(PowerDevicePage, 0x30)?.ScaledValue;

        return new UpsSnapshot
        {
            Timestamp = DateTimeOffset.Now,
            IsConnected = true,
            Device = device,
            AcPresent = GetBoolean(BatterySystemPage, 0xD0),
            Charging = GetBoolean(BatterySystemPage, 0x44),
            Discharging = GetBoolean(BatterySystemPage, 0x45),
            BatteryPercent = batteryPercent,
            RuntimeRemaining = ToDuration(Get(BatterySystemPage, 0x68), defaultIsMinutes: true),
            LowBattery = GetBoolean(BatterySystemPage, 0x42),
            NeedReplacement = GetBoolean(BatterySystemPage, 0x4B),
            ShutdownImminent = GetBoolean(PowerDevicePage, 0x69),
            Overload = GetBoolean(PowerDevicePage, 0x65),
            Voltage = outputVoltage ?? inputVoltage ?? batteryVoltage ?? anyVoltage,
            Current = Get(PowerDevicePage, 0x31)?.ScaledValue,
            Frequency = Get(PowerDevicePage, 0x32)?.ScaledValue,
            Temperature = ToCelsius(Get(PowerDevicePage, 0x36)),
            RemainingTimeLimit = ToDuration(Get(BatterySystemPage, 0x2A), defaultIsMinutes: false),
            DesignCapacity = Get(BatterySystemPage, 0x83)?.ScaledValue,
            FullChargeCapacity = Get(BatterySystemPage, 0x67)?.ScaledValue,
            CycleCount = Get(BatterySystemPage, 0x6B)?.ScaledValue,
            BatteryVoltage = batteryVoltage,
            NominalBatteryVoltage = Get(PowerDevicePage, 0x40, "PowerSummary")?.ScaledValue,
            InputVoltage = inputVoltage,
            OutputVoltage = outputVoltage,
            PercentLoad = Get(PowerDevicePage, 0x35, "Output")?.ScaledValue
                ?? Get(PowerDevicePage, 0x35)?.ScaledValue,
            ActivePower = Get(PowerDevicePage, 0x34, "Output")?.ScaledValue
                ?? Get(PowerDevicePage, 0x34)?.ScaledValue,
            ApparentPower = Get(PowerDevicePage, 0x33, "Output")?.ScaledValue
                ?? Get(PowerDevicePage, 0x33)?.ScaledValue,
            ConfigVoltage = Get(PowerDevicePage, 0x40)?.ScaledValue,
            ConfigActivePower = Get(PowerDevicePage, 0x44)?.ScaledValue,
            ConfigApparentPower = Get(PowerDevicePage, 0x43)?.ScaledValue,
            LowVoltageTransfer = Get(PowerDevicePage, 0x53)?.ScaledValue,
            HighVoltageTransfer = Get(PowerDevicePage, 0x54)?.ScaledValue,
            RemainingCapacityLimit = Get(BatterySystemPage, 0x29)?.ScaledValue,
            WarningCapacityLimit = Get(BatterySystemPage, 0x8C)?.ScaledValue,
            CapacityGranularity1 = Get(BatterySystemPage, 0x8D)?.ScaledValue,
            CapacityGranularity2 = Get(BatterySystemPage, 0x8E)?.ScaledValue,
            FullyCharged = GetBoolean(BatterySystemPage, 0x46),
            RemainingTimeLimitExpired = GetBoolean(BatterySystemPage, 0x43),
            Rechargeable = GetBoolean(BatterySystemPage, 0x8B),
            Boost = GetBoolean(PowerDevicePage, 0x6E),
            DelayBeforeStartup = ToDuration(Get(PowerDevicePage, 0x56), defaultIsMinutes: false),
            DelayBeforeShutdown = ToDuration(Get(PowerDevicePage, 0x57), defaultIsMinutes: false),
            CapacityMode = Get(BatterySystemPage, 0x2C) is { RawValue: var capacityMode }
                ? FormatCapacityMode(capacityMode)
                : null,
            AudibleAlarmState = Get(PowerDevicePage, 0x5A) is { RawValue: var alarmState }
                ? FormatAudibleAlarm(alarmState)
                : null,
            SelfTestState = Get(PowerDevicePage, 0x58) is { RawValue: var testState }
                ? FormatSelfTest(testState)
                : null,
            Telemetry = telemetry,
        };

        HidDataValue? Get(ushort page, ushort usage, string? collectionContains = null)
        {
            if (!valuesByPageUsage.TryGetValue((page, usage), out var candidates) || candidates.Count == 0)
            {
                return null;
            }

            HidDataValue? best = null;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!string.IsNullOrWhiteSpace(collectionContains)
                    && !candidate.Capability.CollectionPath.Contains(collectionContains, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (best is null)
                {
                    best = candidate;
                    continue;
                }

                var bestIsInput = best.Capability.ReportKind == HidReportKind.Input;
                var candIsInput = candidate.Capability.ReportKind == HidReportKind.Input;
                if (candIsInput && !bestIsInput)
                {
                    best = candidate;
                }
                else if (candIsInput == bestIsInput && candidate.Capability.ReportId < best.Capability.ReportId)
                {
                    best = candidate;
                }
            }

            return best;
        }

        bool? GetBoolean(ushort page, ushort usage) =>
            Get(page, usage) is { RawValue: var raw } ? raw != 0 : null;
    }

    private static UpsTelemetryItem ToTelemetry(HidCapability capability, HidDataValue? value)
    {
        var unitSymbol = HidUsageCatalog.GetUnitSymbol(
            capability.UsagePage,
            capability.Usage,
            capability.Unit,
            capability.LogicalMaximum);
        var numericValue = GetDisplayNumericValue(capability, value);

        return new UpsTelemetryItem
        {
            Key = $"{capability.ReportKind}:{capability.ReportId}:{capability.LinkCollection}:{capability.UsagePage:X4}:{capability.Usage:X4}",
            ReportType = capability.ReportKind.ToString(),
            ReportId = capability.ReportId,
            UsagePage = capability.UsagePage,
            Usage = capability.Usage,
            UsagePageName = HidUsageCatalog.GetPageName(capability.UsagePage),
            UsageName = HidUsageCatalog.GetUsageName(capability.UsagePage, capability.Usage),
            LinkCollection = capability.LinkCollection,
            CollectionPath = capability.CollectionPath,
            IsReadable = capability.ReportKind is HidReportKind.Input or HidReportKind.Feature,
            HasValue = value is not null,
            RawValue = value?.RawValue,
            NumericValue = numericValue,
            TextValue = value?.TextValue,
            DisplayValue = FormatTelemetryValue(capability, value, numericValue, unitSymbol),
            UnitSymbol = unitSymbol,
            LogicalMinimum = capability.LogicalMinimum,
            LogicalMaximum = capability.LogicalMaximum,
            PhysicalMinimum = capability.PhysicalMinimum,
            PhysicalMaximum = capability.PhysicalMaximum,
            HidUnit = capability.Unit,
            UnitExponent = capability.UnitExponent,
            BitSize = capability.BitSize,
            ReportCount = capability.ReportCount,
            IsButton = capability.IsButton,
            IsVendorDefined = capability.UsagePage >= 0xFF00,
        };
    }

    private static double? GetDisplayNumericValue(HidCapability capability, HidDataValue? value)
    {
        if (value is null)
        {
            return null;
        }

        if (capability.UsagePage == PowerDevicePage
            && capability.Usage is 0x36 or 0x46
            && capability.Unit == KelvinUnit)
        {
            return value.ScaledValue - 273.15;
        }

        return value.ScaledValue;
    }

    private static string FormatTelemetryValue(
        HidCapability capability,
        HidDataValue? value,
        double? numericValue,
        string? unitSymbol)
    {
        if (value is null)
        {
            return capability.ReportKind == HidReportKind.Output ? "Write-only" : "N/A";
        }

        if (!string.IsNullOrWhiteSpace(value.TextValue))
        {
            return value.TextValue;
        }

        if (HidUsageCatalog.IsBoolean(capability.UsagePage, capability.Usage)
            || capability.IsButton)
        {
            return value.RawValue == 0 ? "False" : "True";
        }

        if (capability.UsagePage == PowerDevicePage && capability.Usage == 0x5A)
        {
            return FormatAudibleAlarm(value.RawValue);
        }

        if (capability.UsagePage == PowerDevicePage && capability.Usage == 0x58)
        {
            return FormatSelfTest(value.RawValue);
        }

        if (capability.UsagePage == BatterySystemPage && capability.Usage == 0x2C)
        {
            return FormatCapacityMode(value.RawValue);
        }

        var formatted = numericValue?.ToString("0.###", CultureInfo.InvariantCulture)
            ?? value.RawValue.ToString(CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(unitSymbol) ? formatted : $"{formatted} {unitSymbol}";
    }

    private static string FormatCapacityMode(long value) => value switch
    {
        0 => "Ampere-hour mode",
        1 => "Watt-hour mode",
        _ => $"Mode {value}",
    };

    private static string FormatAudibleAlarm(long value) => value switch
    {
        1 => "Disabled",
        2 => "Enabled",
        3 => "Muted",
        _ => $"Unknown ({value})",
    };

    private static string FormatSelfTest(long value) => value switch
    {
        1 => "Done - passed",
        2 => "Done - warning",
        3 => "Done - error",
        4 => "Aborted",
        5 => "In progress",
        6 => "No test initiated",
        _ => $"Unknown ({value})",
    };

    private static TimeSpan? ToDuration(HidDataValue? value, bool defaultIsMinutes)
    {
        if (value is null || value.ScaledValue < 0 || double.IsInfinity(value.ScaledValue) || double.IsNaN(value.ScaledValue))
        {
            return null;
        }

        try
        {
            return value.Capability.Unit == SecondsUnit || !defaultIsMinutes
                ? TimeSpan.FromSeconds(value.ScaledValue)
                : TimeSpan.FromMinutes(value.ScaledValue);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    private static double? ToCelsius(HidDataValue? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Capability.Unit == KelvinUnit
            ? value.ScaledValue - 273.15
            : value.ScaledValue;
    }
}
