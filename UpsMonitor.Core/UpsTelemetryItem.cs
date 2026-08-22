namespace UpsMonitor.Core;

public sealed record UpsTelemetryItem
{
    public required string Key { get; init; }
    public required string ReportType { get; init; }
    public required byte ReportId { get; init; }
    public required ushort UsagePage { get; init; }
    public required ushort Usage { get; init; }
    public required string UsagePageName { get; init; }
    public required string UsageName { get; init; }
    public required ushort LinkCollection { get; init; }
    public required string CollectionPath { get; init; }
    public required bool IsReadable { get; init; }
    public required bool HasValue { get; init; }
    public long? RawValue { get; init; }
    public double? NumericValue { get; init; }
    public string? TextValue { get; init; }
    public required string DisplayValue { get; init; }
    public string? UnitSymbol { get; init; }
    public required int LogicalMinimum { get; init; }
    public required int LogicalMaximum { get; init; }
    public required int PhysicalMinimum { get; init; }
    public required int PhysicalMaximum { get; init; }
    public required uint HidUnit { get; init; }
    public required int UnitExponent { get; init; }
    public required ushort BitSize { get; init; }
    public required ushort ReportCount { get; init; }
    public required bool IsButton { get; init; }
    public required bool IsVendorDefined { get; init; }
}
