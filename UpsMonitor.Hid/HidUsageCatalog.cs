namespace UpsMonitor.Hid;

internal static class HidUsageCatalog
{
    private static readonly IReadOnlyDictionary<ushort, string> PowerDeviceUsages = new Dictionary<ushort, string>
    {
        [0x01] = "iName",
        [0x02] = "PresentStatus",
        [0x03] = "ChangedStatus",
        [0x04] = "UPS",
        [0x05] = "PowerSupply",
        [0x10] = "BatterySystem",
        [0x11] = "BatterySystemID",
        [0x12] = "Battery",
        [0x13] = "BatteryID",
        [0x14] = "Charger",
        [0x15] = "ChargerID",
        [0x16] = "PowerConverter",
        [0x17] = "PowerConverterID",
        [0x18] = "OutletSystem",
        [0x19] = "OutletSystemID",
        [0x1A] = "Input",
        [0x1B] = "InputID",
        [0x1C] = "Output",
        [0x1D] = "OutputID",
        [0x1E] = "Flow",
        [0x1F] = "FlowID",
        [0x20] = "Outlet",
        [0x21] = "OutletID",
        [0x22] = "Gang",
        [0x23] = "GangID",
        [0x24] = "PowerSummary",
        [0x25] = "PowerSummaryID",
        [0x30] = "Voltage",
        [0x31] = "Current",
        [0x32] = "Frequency",
        [0x33] = "ApparentPower",
        [0x34] = "ActivePower",
        [0x35] = "PercentLoad",
        [0x36] = "Temperature",
        [0x37] = "Humidity",
        [0x38] = "BadCount",
        [0x40] = "ConfigVoltage",
        [0x41] = "ConfigCurrent",
        [0x42] = "ConfigFrequency",
        [0x43] = "ConfigApparentPower",
        [0x44] = "ConfigActivePower",
        [0x45] = "ConfigPercentLoad",
        [0x46] = "ConfigTemperature",
        [0x47] = "ConfigHumidity",
        [0x50] = "SwitchOnControl",
        [0x51] = "SwitchOffControl",
        [0x52] = "ToggleControl",
        [0x53] = "LowVoltageTransfer",
        [0x54] = "HighVoltageTransfer",
        [0x55] = "DelayBeforeReboot",
        [0x56] = "DelayBeforeStartup",
        [0x57] = "DelayBeforeShutdown",
        [0x58] = "Test",
        [0x59] = "ModuleReset",
        [0x5A] = "AudibleAlarmControl",
        [0x60] = "Present",
        [0x61] = "Good",
        [0x62] = "InternalFailure",
        [0x63] = "VoltageOutOfRange",
        [0x64] = "FrequencyOutOfRange",
        [0x65] = "Overload",
        [0x66] = "OverCharged",
        [0x67] = "OverTemperature",
        [0x68] = "ShutdownRequested",
        [0x69] = "ShutdownImminent",
        [0x6B] = "SwitchOnOff",
        [0x6C] = "Switchable",
        [0x6D] = "Used",
        [0x6E] = "Boost",
        [0x6F] = "Buck",
        [0x70] = "Initialized",
        [0x71] = "Tested",
        [0x72] = "AwaitingPower",
        [0x73] = "CommunicationLost",
        [0xFD] = "iManufacturer",
        [0xFE] = "iProduct",
        [0xFF] = "iSerialNumber",
    };

    private static readonly IReadOnlyDictionary<ushort, string> BatterySystemUsages = new Dictionary<ushort, string>
    {
        [0x01] = "SMBBatteryMode",
        [0x02] = "SMBBatteryStatus",
        [0x03] = "SMBAlarmWarning",
        [0x04] = "SMBChargerMode",
        [0x05] = "SMBChargerStatus",
        [0x06] = "SMBChargerSpecInfo",
        [0x07] = "SMBSelectorState",
        [0x08] = "SMBSelectorPresets",
        [0x09] = "SMBSelectorInfo",
        [0x10] = "OptionalMfgFunction1",
        [0x11] = "OptionalMfgFunction2",
        [0x12] = "OptionalMfgFunction3",
        [0x13] = "OptionalMfgFunction4",
        [0x14] = "OptionalMfgFunction5",
        [0x15] = "ConnectionToSMBus",
        [0x16] = "OutputConnection",
        [0x17] = "ChargerConnection",
        [0x18] = "BatteryInsertion",
        [0x19] = "UseNext",
        [0x1A] = "OKToUse",
        [0x1B] = "BatterySupported",
        [0x1C] = "SelectorRevision",
        [0x1D] = "ChargingIndicator",
        [0x28] = "ManufacturerAccess",
        [0x29] = "RemainingCapacityLimit",
        [0x2A] = "RemainingTimeLimit",
        [0x2B] = "AtRate",
        [0x2C] = "CapacityMode",
        [0x2D] = "BroadcastToCharger",
        [0x2E] = "PrimaryBattery",
        [0x2F] = "ChargeController",
        [0x40] = "TerminateCharge",
        [0x41] = "TerminateDischarge",
        [0x42] = "BelowRemainingCapacityLimit",
        [0x43] = "RemainingTimeLimitExpired",
        [0x44] = "Charging",
        [0x45] = "Discharging",
        [0x46] = "FullyCharged",
        [0x47] = "FullyDischarged",
        [0x48] = "ConditioningFlag",
        [0x49] = "AtRateOK",
        [0x4A] = "SMBErrorCode",
        [0x4B] = "NeedReplacement",
        [0x60] = "AtRateTimeToFull",
        [0x61] = "AtRateTimeToEmpty",
        [0x62] = "AverageCurrent",
        [0x63] = "MaxError",
        [0x64] = "RelativeStateOfCharge",
        [0x65] = "AbsoluteStateOfCharge",
        [0x66] = "RemainingCapacity",
        [0x67] = "FullChargeCapacity",
        [0x68] = "RunTimeToEmpty",
        [0x69] = "AverageTimeToEmpty",
        [0x6A] = "AverageTimeToFull",
        [0x6B] = "CycleCount",
        [0x80] = "BattPackModelLevel",
        [0x81] = "InternalChargeController",
        [0x82] = "PrimaryBatterySupport",
        [0x83] = "DesignCapacity",
        [0x84] = "SpecificationInfo",
        [0x85] = "ManufacturerDate",
        [0x86] = "SerialNumber",
        [0x87] = "iManufacturerName",
        [0x88] = "iDeviceName",
        [0x89] = "iDeviceChemistry",
        [0x8A] = "ManufacturerData",
        [0x8B] = "Rechargeable",
        [0x8C] = "WarningCapacityLimit",
        [0x8D] = "CapacityGranularity1",
        [0x8E] = "CapacityGranularity2",
        [0x8F] = "iOEMInformation",
        [0xC0] = "InhibitCharge",
        [0xC1] = "EnablePolling",
        [0xC2] = "ResetToZero",
        [0xD0] = "ACPresent",
        [0xD1] = "BatteryPresent",
        [0xD2] = "PowerFail",
        [0xD3] = "AlarmInhibited",
        [0xD4] = "ThermistorUnderRange",
        [0xD5] = "ThermistorHot",
        [0xD6] = "ThermistorCold",
        [0xD7] = "ThermistorOverRange",
        [0xD8] = "VoltageOutOfRange",
        [0xD9] = "CurrentOutOfRange",
        [0xDA] = "CurrentNotRegulated",
        [0xDB] = "VoltageNotRegulated",
        [0xDC] = "MasterMode",
        [0xF0] = "ChargerSelectorSupport",
        [0xF1] = "ChargerSpec",
        [0xF2] = "Level2",
        [0xF3] = "Level3",
    };

    internal static string GetPageName(ushort usagePage) => usagePage switch
    {
        0x84 => "Power Device",
        0x85 => "Battery System",
        >= 0xFF00 => "Vendor Defined",
        _ => $"Usage Page 0x{usagePage:X4}",
    };

    internal static string GetUsageName(ushort usagePage, ushort usage)
    {
        var found = usagePage switch
        {
            0x84 => PowerDeviceUsages.GetValueOrDefault(usage),
            0x85 => BatterySystemUsages.GetValueOrDefault(usage),
            _ => null,
        };

        return found ?? (usagePage >= 0xFF00 ? $"VendorUsage_0x{usage:X4}" : $"Usage_0x{usage:X4}");
    }

    internal static bool IsStringIndex(ushort usagePage, ushort usage) =>
        usagePage == 0x84 && usage is 0x01 or 0xFD or 0xFE or 0xFF
        || usagePage == 0x85 && usage is 0x87 or 0x88 or 0x89 or 0x8F;

    internal static bool IsBoolean(ushort usagePage, ushort usage)
    {
        if (usagePage == 0x84)
        {
            return usage is >= 0x60 and <= 0x73;
        }

        if (usagePage != 0x85)
        {
            return false;
        }

        return usage is >= 0x40 and <= 0x49
            or 0x4B
            or 0x81
            or 0x82
            or 0x8B
            or >= 0xC0 and <= 0xC2
            or >= 0xD0 and <= 0xDC
            or 0xF0
            or 0xF2
            or 0xF3;
    }

    internal static string? GetUnitSymbol(ushort usagePage, ushort usage, uint hidUnit, int logicalMaximum)
    {
        if (usagePage == 0x84)
        {
            return usage switch
            {
                0x30 or 0x40 or 0x53 or 0x54 => "V",
                0x31 or 0x41 => "A",
                0x32 or 0x42 => "Hz",
                0x33 or 0x43 => "VA",
                0x34 or 0x44 => "W",
                0x35 or 0x37 or 0x45 or 0x47 => "%",
                0x36 or 0x46 => "°C",
                0x55 or 0x56 or 0x57 => "s",
                _ => null,
            };
        }

        if (usagePage != 0x85)
        {
            return null;
        }

        if (usage is 0x60 or 0x61 or 0x68 or 0x69 or 0x6A or 0x2A)
        {
            return hidUnit == 0x00001001 ? "s" : "min";
        }

        if (usage is 0x63 or 0x64 or 0x65
            || usage is 0x29 or 0x66 or 0x67 or 0x83 or 0x8C or 0x8D or 0x8E && logicalMaximum <= 100)
        {
            return "%";
        }

        return null;
    }
}
