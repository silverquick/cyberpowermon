namespace UpsMonitor.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string? commonApplicationData = null, string? localApplicationData = null)
    {
        var commonRoot = commonApplicationData
            ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        var localRoot = localApplicationData
            ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        SharedDirectory = Path.Combine(commonRoot, "UpsMonitor");
        ConfigurationFile = Path.Combine(SharedDirectory, "config.json");
        DevicesFile = Path.Combine(SharedDirectory, "devices.json");
        LogsDirectory = Path.Combine(SharedDirectory, "logs");
        UserDirectory = Path.Combine(localRoot, "UpsMonitor");
    }

    public string SharedDirectory { get; }

    public string ConfigurationFile { get; }

    public string DevicesFile { get; }

    public string LogsDirectory { get; }

    public string UserDirectory { get; }
}
