using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using UpsMonitor.Core;

namespace UpsMonitor.Hid;

public static class HidDeviceEnumerator
{
    private static readonly IntPtr InvalidHandleValue = new(-1);

    public static IReadOnlyList<HidUpsDeviceDescription> EnumerateUpsDevices()
    {
        return EnumerateCandidates()
            .Select(candidate => new HidUpsDeviceDescription(
                candidate.Device,
                candidate.Descriptor.Capabilities.Select(ToPublicItem).ToArray()))
            .ToArray();
    }

    internal static IReadOnlyList<HidDeviceCandidate> EnumerateCandidates()
    {
        var candidates = new List<HidDeviceCandidate>();
        HidNative.HidD_GetHidGuid(out var hidGuid);
        var deviceInfoSet = HidNative.SetupDiGetClassDevs(
            ref hidGuid,
            IntPtr.Zero,
            IntPtr.Zero,
            HidNative.DigcfPresent | HidNative.DigcfDeviceInterface);

        if (deviceInfoSet == InvalidHandleValue)
        {
            throw new IOException("SetupDiGetClassDevs failed.", new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()));
        }

        try
        {
            for (uint index = 0; ; index++)
            {
                var interfaceData = new HidNative.SpDeviceInterfaceData
                {
                    Size = Marshal.SizeOf<HidNative.SpDeviceInterfaceData>(),
                };

                if (!HidNative.SetupDiEnumDeviceInterfaces(
                    deviceInfoSet,
                    IntPtr.Zero,
                    ref hidGuid,
                    index,
                    ref interfaceData))
                {
                    const int noMoreItems = 259;
                    if (Marshal.GetLastWin32Error() == noMoreItems)
                    {
                        break;
                    }

                    continue;
                }

                var path = GetDevicePath(deviceInfoSet, ref interfaceData);
                if (path is null)
                {
                    continue;
                }

                try
                {
                    var candidate = Inspect(path);
                    if (candidate is { Descriptor.UsagePage: 0x84, Descriptor.Usage: 0x04 })
                    {
                        candidates.Add(candidate);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // One inaccessible HID interface must not prevent discovery of other devices.
                }
            }
        }
        finally
        {
            HidNative.SetupDiDestroyDeviceInfoList(deviceInfoSet);
        }

        return candidates;
    }

    private static HidDeviceCandidate? Inspect(string path)
    {
        using var handle = Open(path, 0, HidNative.FileAttributeNormal);
        if (handle.IsInvalid)
        {
            return null;
        }

        var attributes = new HidNative.HiddAttributes { Size = Marshal.SizeOf<HidNative.HiddAttributes>() };
        if (!HidNative.HidD_GetAttributes(handle, ref attributes)
            || !HidNative.HidD_GetPreparsedData(handle, out var preparsedData))
        {
            return null;
        }

        try
        {
            var descriptor = HidDescriptorReader.Read(preparsedData);
            var info = new UpsDeviceInfo(
                path,
                attributes.VendorId,
                attributes.ProductId,
                ReadString(handle, HidNative.HidD_GetManufacturerString),
                ReadString(handle, HidNative.HidD_GetProductString),
                ReadString(handle, HidNative.HidD_GetSerialNumberString),
                descriptor.UsagePage,
                descriptor.Usage,
                descriptor.InputReportByteLength,
                descriptor.FeatureReportByteLength);
            return new HidDeviceCandidate(info, descriptor);
        }
        finally
        {
            HidNative.HidD_FreePreparsedData(preparsedData);
        }
    }

    internal static SafeFileHandle Open(string path, uint access, uint flags) =>
        HidNative.CreateFile(
            path,
            access,
            HidNative.FileShareRead | HidNative.FileShareWrite,
            IntPtr.Zero,
            HidNative.OpenExisting,
            flags,
            IntPtr.Zero);

    private static string? GetDevicePath(IntPtr deviceInfoSet, ref HidNative.SpDeviceInterfaceData interfaceData)
    {
        _ = HidNative.SetupDiGetDeviceInterfaceDetail(
            deviceInfoSet,
            ref interfaceData,
            IntPtr.Zero,
            0,
            out var requiredSize,
            IntPtr.Zero);

        if (requiredSize == 0)
        {
            return null;
        }

        var detailBuffer = Marshal.AllocHGlobal(checked((int)requiredSize));
        try
        {
            Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 6);
            if (!HidNative.SetupDiGetDeviceInterfaceDetail(
                deviceInfoSet,
                ref interfaceData,
                detailBuffer,
                requiredSize,
                out _,
                IntPtr.Zero))
            {
                return null;
            }

            // DevicePath follows the 32-bit cbSize field; cbSize includes native tail padding on x64.
            return Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, sizeof(int)));
        }
        finally
        {
            Marshal.FreeHGlobal(detailBuffer);
        }
    }

    private static string? ReadString(SafeFileHandle handle, HidStringReader reader)
    {
        var buffer = new StringBuilder(256);
        return reader(handle, buffer, buffer.Capacity * sizeof(char))
            ? buffer.ToString().TrimEnd('\0')
            : null;
    }

    private static HidDescriptorItem ToPublicItem(HidCapability item) => new(
        item.ReportKind.ToString(),
        item.UsagePage,
        item.Usage,
        item.ReportId,
        item.LinkCollection,
        item.LinkUsagePage,
        item.LinkUsage,
        item.CollectionPath,
        item.LogicalMinimum,
        item.LogicalMaximum,
        item.PhysicalMinimum,
        item.PhysicalMaximum,
        item.Unit,
        item.UnitExponent,
        item.BitSize,
        item.ReportCount,
        item.IsButton);

    private delegate bool HidStringReader(SafeFileHandle handle, StringBuilder buffer, int bufferLength);
}
