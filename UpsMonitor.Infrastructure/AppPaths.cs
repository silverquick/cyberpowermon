namespace UpsMonitor.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(string? commonApplicationData = null)
    {
        var commonRoot = commonApplicationData
            ?? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);

        SharedDirectory = Path.Combine(commonRoot, "UpsMonitor");
        ConfigurationFile = Path.Combine(SharedDirectory, "config.json");
        TelemetryDatabaseFile = Path.Combine(SharedDirectory, "telemetry.db");
        LogsDirectory = Path.Combine(SharedDirectory, "logs");
    }

    public string SharedDirectory { get; }

    public string ConfigurationFile { get; }

    public string TelemetryDatabaseFile { get; }

    public string LogsDirectory { get; }
}
