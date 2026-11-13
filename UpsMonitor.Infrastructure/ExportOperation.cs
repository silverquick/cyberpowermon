using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public enum TelemetryExportFormat { Csv, Json }

/// <summary>Flush the write queue before opening a streaming read connection.</summary>
public static class ExportOperation
{
    public static async Task TelemetryAsync(SqliteTelemetryStore store, string destination,
        TelemetryExportFormat format, DateTimeOffset from, DateTimeOffset to, CancellationToken token = default)
    {
        await store.FlushAsync(token).ConfigureAwait(false);
        if (format == TelemetryExportFormat.Json)
            await TelemetryExporter.ExportTelemetryJsonAsync(store.DatabasePath, destination, from, to, token).ConfigureAwait(false);
        else
            await TelemetryExporter.ExportTelemetryCsvAsync(store.DatabasePath, destination, from, to, token).ConfigureAwait(false);
    }

    public static async Task EventsAsync(SqliteTelemetryStore store, string destination, EventQuery query,
        CancellationToken token = default)
    {
        query = query.Snapshot();
        await store.FlushAsync(token).ConfigureAwait(false);
        await TelemetryExporter.ExportEventsCsvAsync(store.DatabasePath, destination, query, token).ConfigureAwait(false);
    }
}
