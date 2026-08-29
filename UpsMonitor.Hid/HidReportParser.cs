using System.Buffers;

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
        var capabilities = descriptor.Capabilities;

        // Process Value capabilities
        for (var i = 0; i < capabilities.Count; i++)
        {
            var capability = capabilities[i];
            if (capability.ReportKind != reportKind || capability.ReportId != reportId || capability.IsButton)
            {
                continue;
            }

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

        // Process Button capabilities
        List<HidCapability>? buttonCaps = null;
        for (var i = 0; i < capabilities.Count; i++)
        {
            var cap = capabilities[i];
            if (cap.ReportKind == reportKind && cap.ReportId == reportId && cap.IsButton)
            {
                buttonCaps ??= [];
                buttonCaps.Add(cap);
            }
        }

        if (buttonCaps is not null)
        {
            var groups = buttonCaps.GroupBy(item => (item.UsagePage, item.LinkCollection));
            foreach (var group in groups)
            {
                var usagePage = group.Key.UsagePage;
                var linkCollection = group.Key.LinkCollection;
                var maximumLength = HidNative.HidP_MaxUsageListLength(reportKind, usagePage, preparsedData);
                if (maximumLength == 0)
                {
                    maximumLength = (uint)group.Count();
                }

                maximumLength = Math.Min(maximumLength, 4096);
                var rented = ArrayPool<ushort>.Shared.Rent((int)maximumLength);
                try
                {
                    var activeCount = maximumLength;
                    var status = HidNative.HidP_GetUsages(
                        reportKind,
                        usagePage,
                        linkCollection,
                        rented,
                        ref activeCount,
                        preparsedData,
                        report,
                        (uint)report.Length);

                    if (status != HidNative.HidpStatusSuccess)
                    {
                        continue;
                    }

                    var activeSpan = rented.AsSpan(0, (int)activeCount);
                    foreach (var capability in group)
                    {
                        var isActive = false;
                        for (var a = 0; a < activeSpan.Length; a++)
                        {
                            if (activeSpan[a] == capability.Usage)
                            {
                                isActive = true;
                                break;
                            }
                        }

                        var raw = isActive ? 1 : 0;
                        result.Add(new HidDataValue(capability, raw, raw));
                    }
                }
                finally
                {
                    ArrayPool<ushort>.Shared.Return(rented);
                }
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
