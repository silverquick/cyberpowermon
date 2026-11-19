using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

namespace UpsMonitor.App;

// WPF-free query adapter: the VM owns request cancellation, the store owns SQLite access.
internal sealed record HistoryQueries
{
    internal required Func<string, DateTimeOffset, DateTimeOffset, IReadOnlyCollection<TelemetryMetric>, CancellationToken, Task<TelemetryHistoryResult>> History { get; init; }
    internal required Func<CancellationToken, Task<TelemetryDatabaseStatistics>> Statistics { get; init; }
    internal required Func<string, DateTimeOffset, DateTimeOffset, EnergyReportPeriod, double, CancellationToken, Task<IReadOnlyList<EnergyReportItem>>> Energy { get; init; }
    internal required Func<string, DateTimeOffset, DateTimeOffset, double, double, CancellationToken, Task<PowerTroubleSummary>> Trouble { get; init; }

    internal static HistoryQueries? FromStore(SqliteTelemetryStore? store) => store is null ? null : new()
    {
        History = (device, from, to, metrics, token) => store.QueryHistoryAsync(device, from, to, metrics, cancellationToken: token),
        Statistics = store.GetStatisticsAsync,
        Energy = store.QueryEnergyReportsAsync,
        Trouble = store.QueryPowerTroubleSummaryAsync,
    };
}
