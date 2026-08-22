namespace UpsMonitor.Hid;

internal static class HidDescriptorReader
{
    internal static HidDescriptor Read(IntPtr preparsedData)
    {
        EnsureSuccess(HidNative.HidP_GetCaps(preparsedData, out var caps), "HidP_GetCaps");
        var items = new List<HidCapability>();
        var linkCollections = ReadLinkCollections(preparsedData, caps);

        AddValueCaps(HidReportKind.Input, caps.NumberInputValueCaps);
        AddButtonCaps(HidReportKind.Input, caps.NumberInputButtonCaps);
        AddValueCaps(HidReportKind.Output, caps.NumberOutputValueCaps);
        AddButtonCaps(HidReportKind.Output, caps.NumberOutputButtonCaps);
        AddValueCaps(HidReportKind.Feature, caps.NumberFeatureValueCaps);
        AddButtonCaps(HidReportKind.Feature, caps.NumberFeatureButtonCaps);

        return new HidDescriptor(
            caps.UsagePage,
            caps.Usage,
            caps.InputReportByteLength,
            caps.OutputReportByteLength,
            caps.FeatureReportByteLength,
            items,
            linkCollections);

        void AddValueCaps(HidReportKind kind, ushort count)
        {
            if (count == 0)
            {
                return;
            }

            var nativeCaps = new HidNative.HidpValueCaps[count];
            var actual = count;
            EnsureSuccess(HidNative.HidP_GetValueCaps(kind, nativeCaps, ref actual, preparsedData), "HidP_GetValueCaps");

            foreach (var native in nativeCaps.Take(actual))
            {
                foreach (var usage in ExpandUsages(native.IsRange != 0, native.UsageOrUsageMin, native.ReservedOrUsageMax))
                {
                    items.Add(new HidCapability(
                        kind,
                        native.UsagePage,
                        usage,
                        native.ReportId,
                        native.LinkCollection,
                        native.LinkUsagePage,
                        native.LinkUsage,
                        GetCollectionPath(native.LinkCollection),
                        native.LogicalMin,
                        native.LogicalMax,
                        native.PhysicalMin,
                        native.PhysicalMax,
                        native.BitSize,
                        native.ReportCount,
                        native.Units,
                        DecodeUnitExponent(native.UnitsExp),
                        false));
                }
            }
        }

        void AddButtonCaps(HidReportKind kind, ushort count)
        {
            if (count == 0)
            {
                return;
            }

            var nativeCaps = new HidNative.HidpButtonCaps[count];
            var actual = count;
            EnsureSuccess(HidNative.HidP_GetButtonCaps(kind, nativeCaps, ref actual, preparsedData), "HidP_GetButtonCaps");

            foreach (var native in nativeCaps.Take(actual))
            {
                foreach (var usage in ExpandUsages(native.IsRange != 0, native.UsageOrUsageMin, native.ReservedOrUsageMax))
                {
                    items.Add(new HidCapability(
                        kind,
                        native.UsagePage,
                        usage,
                        native.ReportId,
                        native.LinkCollection,
                        native.LinkUsagePage,
                        native.LinkUsage,
                        GetCollectionPath(native.LinkCollection),
                        0,
                        1,
                        0,
                        1,
                        1,
                        1,
                        0,
                        0,
                        true));
                }
            }
        }

        string GetCollectionPath(ushort linkCollection) =>
            linkCollections.FirstOrDefault(item => item.Index == linkCollection)?.Path
            ?? $"LinkCollection[{linkCollection}]";
    }

    private static IReadOnlyList<HidLinkCollection> ReadLinkCollections(
        IntPtr preparsedData,
        HidNative.HidpCaps caps)
    {
        if (caps.NumberLinkCollectionNodes == 0)
        {
            return
            [
                new HidLinkCollection(
                    0,
                    caps.UsagePage,
                    caps.Usage,
                    0,
                    HidUsageCatalog.GetUsageName(caps.UsagePage, caps.Usage)),
            ];
        }

        var nativeNodes = new HidNative.HidpLinkCollectionNode[caps.NumberLinkCollectionNodes];
        var actual = (uint)nativeNodes.Length;
        EnsureSuccess(
            HidNative.HidP_GetLinkCollectionNodes(nativeNodes, ref actual, preparsedData),
            "HidP_GetLinkCollectionNodes");

        var result = new List<HidLinkCollection>(checked((int)actual));
        for (ushort index = 0; index < actual; index++)
        {
            var node = nativeNodes[index];
            result.Add(new HidLinkCollection(
                index,
                node.LinkUsagePage,
                node.LinkUsage,
                node.Parent,
                BuildPath(index, nativeNodes, actual)));
        }

        return result;
    }

    private static string BuildPath(
        ushort index,
        IReadOnlyList<HidNative.HidpLinkCollectionNode> nodes,
        uint count)
    {
        var segments = new List<string>();
        var visited = new HashSet<ushort>();
        var current = index;

        while (current < count && visited.Add(current))
        {
            var node = nodes[current];
            segments.Add(HidUsageCatalog.GetUsageName(node.LinkUsagePage, node.LinkUsage));
            if (current == 0 || node.Parent == current)
            {
                break;
            }

            current = node.Parent;
        }

        segments.Reverse();
        return string.Join(" / ", segments.Where(segment => !string.IsNullOrWhiteSpace(segment)));
    }

    private static IEnumerable<ushort> ExpandUsages(bool isRange, ushort minimum, ushort maximum)
    {
        if (!isRange || maximum < minimum)
        {
            yield return minimum;
            yield break;
        }

        var count = Math.Min(maximum - minimum + 1, 1024);
        for (var offset = 0; offset < count; offset++)
        {
            yield return (ushort)(minimum + offset);
        }
    }

    private static int DecodeUnitExponent(uint exponent)
    {
        var nibble = (int)(exponent & 0x0F);
        return nibble > 7 ? nibble - 16 : nibble;
    }

    private static void EnsureSuccess(int status, string operation)
    {
        if (status != HidNative.HidpStatusSuccess)
        {
            throw new IOException($"{operation} failed with HID parser status 0x{status:X8}.");
        }
    }
}
