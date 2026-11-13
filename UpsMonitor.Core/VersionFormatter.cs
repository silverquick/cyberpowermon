namespace UpsMonitor.Core;

public static class VersionFormatter
{
    public static string Format(Version version) => $"{version.Major}.{version.Minor}.{version.Build:D3}";
}
