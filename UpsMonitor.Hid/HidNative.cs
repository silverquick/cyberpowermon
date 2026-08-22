using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace UpsMonitor.Hid;

internal static class HidNative
{
    internal const uint DigcfPresent = 0x00000002;
    internal const uint DigcfDeviceInterface = 0x00000010;
    internal const uint GenericRead = 0x80000000;
    internal const uint GenericWrite = 0x40000000;
    internal const uint FileShareRead = 0x00000001;
    internal const uint FileShareWrite = 0x00000002;
    internal const uint OpenExisting = 3;
    internal const uint FileAttributeNormal = 0x00000080;
    internal const uint FileFlagOverlapped = 0x40000000;
    internal const int HidpStatusSuccess = 0x00110000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct SpDeviceInterfaceData
    {
        internal int Size;
        internal Guid InterfaceClassGuid;
        internal int Flags;
        internal IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HiddAttributes
    {
        internal int Size;
        internal ushort VendorId;
        internal ushort ProductId;
        internal ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct HidpCaps
    {
        internal ushort Usage;
        internal ushort UsagePage;
        internal ushort InputReportByteLength;
        internal ushort OutputReportByteLength;
        internal ushort FeatureReportByteLength;
        internal fixed ushort Reserved[17];
        internal ushort NumberLinkCollectionNodes;
        internal ushort NumberInputButtonCaps;
        internal ushort NumberInputValueCaps;
        internal ushort NumberInputDataIndices;
        internal ushort NumberOutputButtonCaps;
        internal ushort NumberOutputValueCaps;
        internal ushort NumberOutputDataIndices;
        internal ushort NumberFeatureButtonCaps;
        internal ushort NumberFeatureValueCaps;
        internal ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct HidpValueCaps
    {
        internal ushort UsagePage;
        internal byte ReportId;
        internal byte IsAlias;
        internal ushort BitField;
        internal ushort LinkCollection;
        internal ushort LinkUsage;
        internal ushort LinkUsagePage;
        internal byte IsRange;
        internal byte IsStringRange;
        internal byte IsDesignatorRange;
        internal byte IsAbsolute;
        internal byte HasNull;
        internal byte Reserved;
        internal ushort BitSize;
        internal ushort ReportCount;
        internal fixed ushort Reserved2[5];
        internal uint UnitsExp;
        internal uint Units;
        internal int LogicalMin;
        internal int LogicalMax;
        internal int PhysicalMin;
        internal int PhysicalMax;
        internal ushort UsageOrUsageMin;
        internal ushort ReservedOrUsageMax;
        internal ushort StringIndexOrStringMin;
        internal ushort ReservedOrStringMax;
        internal ushort DesignatorIndexOrDesignatorMin;
        internal ushort ReservedOrDesignatorMax;
        internal ushort DataIndexOrDataIndexMin;
        internal ushort ReservedOrDataIndexMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct HidpButtonCaps
    {
        internal ushort UsagePage;
        internal byte ReportId;
        internal byte IsAlias;
        internal ushort BitField;
        internal ushort LinkCollection;
        internal ushort LinkUsage;
        internal ushort LinkUsagePage;
        internal byte IsRange;
        internal byte IsStringRange;
        internal byte IsDesignatorRange;
        internal byte IsAbsolute;
        internal fixed uint Reserved[10];
        internal ushort UsageOrUsageMin;
        internal ushort ReservedOrUsageMax;
        internal ushort StringIndexOrStringMin;
        internal ushort ReservedOrStringMax;
        internal ushort DesignatorIndexOrDesignatorMin;
        internal ushort ReservedOrDesignatorMax;
        internal ushort DataIndexOrDataIndexMin;
        internal ushort ReservedOrDataIndexMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct HidpLinkCollectionNode
    {
        internal ushort LinkUsage;
        internal ushort LinkUsagePage;
        internal ushort Parent;
        internal ushort NumberOfChildren;
        internal ushort NextSibling;
        internal ushort FirstChild;
        internal uint CollectionFlags;
        internal IntPtr UserContext;
    }

    [DllImport("hid.dll")]
    internal static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        IntPtr enumerator,
        IntPtr parentWindow,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        uint memberIndex,
        ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SpDeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize,
        out uint requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_GetAttributes(SafeFileHandle device, ref HiddAttributes attributes);

    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_GetManufacturerString(SafeFileHandle device, StringBuilder buffer, int bufferLength);

    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_GetProductString(SafeFileHandle device, StringBuilder buffer, int bufferLength);

    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_GetSerialNumberString(SafeFileHandle device, StringBuilder buffer, int bufferLength);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_GetPreparsedData(SafeFileHandle device, out IntPtr preparsedData);

    [DllImport("hid.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetCaps(IntPtr preparsedData, out HidpCaps capabilities);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetValueCaps(
        HidReportKind reportType,
        [Out] HidpValueCaps[] valueCaps,
        ref ushort valueCapsLength,
        IntPtr preparsedData);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetButtonCaps(
        HidReportKind reportType,
        [Out] HidpButtonCaps[] buttonCaps,
        ref ushort buttonCapsLength,
        IntPtr preparsedData);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetLinkCollectionNodes(
        [Out] HidpLinkCollectionNode[] linkCollectionNodes,
        ref uint linkCollectionNodesLength,
        IntPtr preparsedData);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetUsageValue(
        HidReportKind reportType,
        ushort usagePage,
        ushort linkCollection,
        ushort usage,
        out uint usageValue,
        IntPtr preparsedData,
        byte[] report,
        uint reportLength);

    [DllImport("hid.dll")]
    internal static extern int HidP_GetUsages(
        HidReportKind reportType,
        ushort usagePage,
        ushort linkCollection,
        [Out] ushort[] usageList,
        ref uint usageLength,
        IntPtr preparsedData,
        byte[] report,
        uint reportLength);

    [DllImport("hid.dll")]
    internal static extern uint HidP_MaxUsageListLength(
        HidReportKind reportType,
        ushort usagePage,
        IntPtr preparsedData);

    [DllImport("hid.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_GetFeature(SafeFileHandle device, [In, Out] byte[] reportBuffer, int reportBufferLength);

    [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool HidD_GetIndexedString(
        SafeFileHandle device,
        uint stringIndex,
        StringBuilder buffer,
        int bufferLength);
}
