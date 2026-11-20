using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using UpsMonitor.Core;
using UpsMonitor.Infrastructure;

namespace UpsMonitor.App;

/// <summary>Shared History/Logs export worker, cancellation and status. Uses the UI dispatcher.</summary>
public sealed class ExportViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly SqliteTelemetryStore? _store;
    private readonly Func<bool, bool, string?> _pickFile;
    private readonly Action<Exception> _onError;
    private CancellationTokenSource? _cancellation;
    private Task? _worker;
    private string _status = string.Empty;
    private bool _disposed;

    public ExportViewModel(SqliteTelemetryStore? store, Func<bool, bool, string?> pickFile,
        Func<EventQuery> eventQuery, Action<Exception> onError, Action<Exception> onCommandError)
    {
        _store = store;
        _pickFile = pickFile;
        _onError = onError;
        ExportEventsCsvCommand = new AsyncRelayCommand(() => ExportAsync(true, false, null, eventQuery), onCommandError);
        CancelExportCommand = new AsyncRelayCommand(() => { _cancellation?.Cancel(); return Task.CompletedTask; }, onCommandError);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand ExportEventsCsvCommand { get; }
    public ICommand CancelExportCommand { get; }
    public string ExportStatus { get => _status; private set { _status = value; Changed(); } }
    public bool IsExporting => _cancellation is not null;
    public bool CanStartExport => _store is not null && !IsExporting && !_disposed;

    public Task ExportTelemetryAsync(bool json, Func<(DateTimeOffset From, DateTimeOffset To)> range) =>
        ExportAsync(false, json, range, null);

    private async Task ExportAsync(bool events, bool json,
        Func<(DateTimeOffset From, DateTimeOffset To)>? range, Func<EventQuery>? eventQuery)
    {
        if (!CanStartExport) return;
        var path = _pickFile(events, json);
        if (path is null || !CanStartExport) return;
        // Snapshot both tabs' filters after the dialog closes, just as before extraction.
        var bounds = range?.Invoke() ?? default;
        var query = eventQuery?.Invoke();
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        Changed(nameof(IsExporting)); Changed(nameof(CanStartExport));
        ExportStatus = LocalizationManager.Get("ExportRunning");
        try
        {
            _worker = Task.Run(async () =>
            {
                if (events)
                    await ExportOperation.EventsAsync(_store!, path, query!, cancellation.Token);
                else
                    await ExportOperation.TelemetryAsync(_store!, path,
                        json ? TelemetryExportFormat.Json : TelemetryExportFormat.Csv, bounds.From, bounds.To, cancellation.Token);
            }, cancellation.Token);
            await _worker;
            if (!_disposed) ExportStatus = LocalizationManager.Format("ExportSuccess", System.IO.Path.GetFileName(path));
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            if (!_disposed) ExportStatus = LocalizationManager.Get("ExportCancelled");
        }
        catch (Exception exception)
        {
            if (!_disposed)
            {
                ExportStatus = LocalizationManager.Format("ExportFailed", exception.Message);
                _onError(exception);
            }
        }
        finally
        {
            _cancellation = null;
            if (!_disposed) { Changed(nameof(IsExporting)); Changed(nameof(CanStartExport)); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _cancellation?.Cancel();
        await ExportShutdown.WaitForWorkerAsync(_worker, ExportShutdown.WaitTimeout).ConfigureAwait(false);
    }

    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
