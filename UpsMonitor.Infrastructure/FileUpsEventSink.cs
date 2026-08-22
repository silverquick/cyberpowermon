using System.Globalization;
using System.Text;
using UpsMonitor.Core;

namespace UpsMonitor.Infrastructure;

public sealed class FileUpsEventSink : IUpsEventSink, IDisposable
{
    private readonly AppPaths _paths;
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public FileUpsEventSink(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task WriteAsync(UpsEvent upsEvent, CancellationToken cancellationToken)
    {
        var line = string.Create(
            CultureInfo.InvariantCulture,
            $"{upsEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{upsEvent.Type}] {upsEvent.Message} State: {upsEvent.PreviousState} -> {upsEvent.CurrentState}{Environment.NewLine}");

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.LogsDirectory);
            var file = Path.Combine(_paths.LogsDirectory, $"ups-{upsEvent.Timestamp:yyyy-MM-dd}.log");
            await File.AppendAllTextAsync(file, line, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Dispose()
    {
        _writeLock.Dispose();
    }
}

public sealed class NullUpsEventSink : IUpsEventSink
{
    public Task WriteAsync(UpsEvent upsEvent, CancellationToken cancellationToken) => Task.CompletedTask;
}
