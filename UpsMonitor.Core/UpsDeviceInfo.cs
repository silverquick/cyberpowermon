namespace UpsMonitor.Core;

public sealed record UpsDeviceInfo(
    string DevicePath,
    ushort VendorId,
    ushort ProductId,
    string? Manufacturer,
    string? Product,
    string? SerialNumber,
    ushort UsagePage,
    ushort Usage,
    ushort InputReportByteLength,
    ushort FeatureReportByteLength)
{
    public string DisplayName =>
        !string.IsNullOrWhiteSpace(Product) ? Product : $"VID_{VendorId:X4}&PID_{ProductId:X4}";
}
