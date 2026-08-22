namespace UpsMonitor.Hid;

internal static class HidReportParser
{
    internal static IReadOnlyList<HidDataValue> Parse(
        IntPtr preparsedData,
        HidDescriptor descriptor,
        HidReportKind reportKind,
        byte[] report)
    {
        var result = new List<HidDataValue>();
        var reportId = report.Length > 0 ? report[0] : (byte)0;
        var capabilities = descriptor.Capabilities
            .Where(item => item.ReportKind == reportKind && item.ReportId == reportId)
            .ToArray();

        foreach (var capability in capabilities.Where(item => !item.IsButton))
        {
            var status = HidNative.HidP_GetUsageValue(
                reportKind,
                capability.UsagePage,
                capability.LinkCollection,
                capability.Usage,
                out var raw,
                preparsedData,
                report,
                (uint)report.Length);

            if (status != HidNative.HidpStatusSuccess)
            {
                continue;
            }

            var signedValue = DecodeSignedValue(raw, capability.LogicalMinimum, capability.BitSize);
            result.Add(new HidDataValue(
                capability,
                signedValue,
                ApplyDisplayScale(signedValue, capability.Unit, capability.UnitExponent)));
        }

        foreach (var group in capabilities
            .Where(item => item.IsButton)
            .GroupBy(item => (item.UsagePage, item.LinkCollection)))
        {
            var maximumLength = HidNative.HidP_MaxUsageListLength(reportKind, group.Key.UsagePage, preparsedData);
            if (maximumLength == 0)
            {
                maximumLength = (uint)group.Count();
            }

            maximumLength = Math.Min(maximumLength, 4096);
            var activeUsages = new ushort[maximumLength];
            var activeCount = maximumLength;
            var status = HidNative.HidP_GetUsages(
                reportKind,
                group.Key.UsagePage,
                group.Key.LinkCollection,
                activeUsages,
                ref activeCount,
                preparsedData,
                report,
                (uint)report.Length);

            if (status != HidNative.HidpStatusSuccess)
            {
                continue;
            }

            var activeSet = activeUsages.Take(checked((int)activeCount)).ToHashSet();
            foreach (var capability in group)
            {
                var raw = activeSet.Contains(capability.Usage) ? 1 : 0;
                result.Add(new HidDataValue(capability, raw, raw));
            }
        }

        return result;
    }

    private static double ApplyDisplayScale(long value, uint hidUnit, int unitExponent)
    {
        // The Power Device specification encodes volt and watt as derived HID SI units.
        // With those encodings, exponent 7 is the user-facing base unit and exponent 6
        // represents tenths. Other common units use the exponent directly.
        var displayExponent = hidUnit switch
        {
            0x00F0D121 => unitExponent - 7, // Volt
            0x0000D121 => unitExponent - 7, // Watt / volt-ampere
            _ => unitExponent is < 0 and >= -8 ? unitExponent : 0,
        };

        return value * Math.Pow(10, displayExponent);
    }

    private static long DecodeSignedValue(uint value, int logicalMinimum, ushort bitSize)
    {
        if (logicalMinimum >= 0 || bitSize == 0)
        {
            return value;
        }

        if (bitSize >= 32)
        {
            return unchecked((int)value);
        }

        var signBit = 1u << (bitSize - 1);
        if ((value & signBit) == 0)
        {
            return value;
        }

        var mask = (1u << bitSize) - 1;
        return unchecked((int)(value | ~mask));
    }
}
