using UpsMonitor.Core;

namespace UpsMonitor.Hid;

internal enum HidReportKind
{
    Input = 0,
    Output = 1,
    Feature = 2,
}

internal sealed record HidCapability(
    HidReportKind ReportKind,
    ushort UsagePage,
    ushort Usage,
    byte ReportId,
    ushort LinkCollection,
    ushort LinkUsagePage,
    ushort LinkUsage,
    string CollectionPath,
    int LogicalMinimum,
    int LogicalMaximum,
    int PhysicalMinimum,
    int PhysicalMaximum,
    ushort BitSize,
    ushort ReportCount,
    uint Unit,
    int UnitExponent,
    bool IsButton);

internal sealed record HidDescriptor(
    ushort UsagePage,
    ushort Usage,
    ushort InputReportByteLength,
    ushort OutputReportByteLength,
    ushort FeatureReportByteLength,
    IReadOnlyList<HidCapability> Capabilities,
    IReadOnlyList<HidLinkCollection> LinkCollections);

internal sealed record HidLinkCollection(
    ushort Index,
    ushort UsagePage,
    ushort Usage,
    ushort Parent,
    string Path);

internal sealed record HidDeviceCandidate(UpsDeviceInfo Device, HidDescriptor Descriptor);

internal sealed record HidDataValue(
    HidCapability Capability,
    long RawValue,
    double ScaledValue,
    string? TextValue = null);

public sealed record HidDescriptorItem(
    string ReportType,
    ushort UsagePage,
    ushort Usage,
    byte ReportId,
    ushort LinkCollection,
    ushort LinkUsagePage,
    ushort LinkUsage,
    string CollectionPath,
    int LogicalMinimum,
    int LogicalMaximum,
    int PhysicalMinimum,
    int PhysicalMaximum,
    uint Unit,
    int UnitExponent,
    ushort BitSize,
    ushort ReportCount,
    bool IsButton);

public sealed record HidUpsDeviceDescription(
    UpsDeviceInfo Device,
    IReadOnlyList<HidDescriptorItem> DescriptorItems);
