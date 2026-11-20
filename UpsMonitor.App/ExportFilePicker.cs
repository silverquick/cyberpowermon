namespace UpsMonitor.App;

internal static class ExportFilePicker
{
    internal static string? Pick(bool events, bool json)
    {
        var extension = json ? "json" : "csv";
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = json ? "JSON files (*.json)|*.json|All files (*.*)|*.*" : "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"ups-{(events ? "events" : "telemetry")}-{DateTime.Now:yyyyMMdd-HHmmss}.{extension}",
            DefaultExt = "." + extension,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
