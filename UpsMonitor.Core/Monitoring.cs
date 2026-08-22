namespace UpsMonitor.Core;

public interface IUpsProvider : IAsyncDisposable
{
    UpsDeviceInfo? Device { get; }

    Task<bool> ConnectAsync(CancellationToken cancellationToken);

    Task<UpsSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken);

    void Disconnect();
}

public interface IUpsEventSink
{
    Task WriteAsync(UpsEvent upsEvent, CancellationToken cancellationToken);
}

public sealed class UpsMonitorEngine : IAsyncDisposable
{
    private readonly IUpsProvider _provider;
    private readonly IUpsEventSink _eventSink;
    private readonly UpsEventDetector _eventDetector;
    private readonly SemaphoreSlim _wakeSignal = new(0, 1);
    private readonly object _lifecycleLock = new();
    private CancellationTokenSource? _runCancellation;
    private Task? _runTask;
    private volatile bool _rescanRequested;
    private int _pollIntervalMs;

    public UpsMonitorEngine(
        IUpsProvider provider,
        IUpsEventSink eventSink,
        int pollIntervalMs,
        TimeSpan runtimeLowThreshold)
    {
        _provider = provider;
        _eventSink = eventSink;
        _pollIntervalMs = ValidatePollInterval(pollIntervalMs);
        _eventDetector = new UpsEventDetector(runtimeLowThreshold);
    }

    public event Action<UpsSnapshot>? SnapshotUpdated;

    public event Action<UpsEvent>? EventDetected;

    public event Action<Exception>? MonitorError;

    public void Start()
    {
        lock (_lifecycleLock)
        {
            if (_runTask is not null)
            {
                return;
            }

            _runCancellation = new CancellationTokenSource();
            _runTask = Task.Run(() => RunAsync(_runCancellation.Token));
        }
    }

    public void SetPollInterval(int milliseconds)
    {
        _pollIntervalMs = ValidatePollInterval(milliseconds);
        Wake();
    }

    public void NotifyDeviceChange()
    {
        _rescanRequested = true;
        Wake();
    }

    public async Task StopAsync()
    {
        Task? runTask;
        lock (_lifecycleLock)
        {
            runTask = _runTask;
            _runCancellation?.Cancel();
        }

        Wake();
        if (runTask is not null)
        {
            try
            {
                await runTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        lock (_lifecycleLock)
        {
            _runTask = null;
            _runCancellation?.Dispose();
            _runCancellation = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _provider.Disconnect();
        await _provider.DisposeAsync().ConfigureAwait(false);
        _wakeSignal.Dispose();
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_rescanRequested)
                {
                    _rescanRequested = false;
                    _provider.Disconnect();
                }

                if (_provider.Device is null && !await _provider.ConnectAsync(cancellationToken).ConfigureAwait(false))
                {
                    await PublishAsync(UpsSnapshot.Disconnected(DateTimeOffset.Now), cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var snapshot = await _provider.ReadSnapshotAsync(cancellationToken).ConfigureAwait(false);
                    await PublishAsync(snapshot, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _provider.Disconnect();
                MonitorError?.Invoke(exception);
                await PublishAsync(UpsSnapshot.Disconnected(DateTimeOffset.Now), cancellationToken).ConfigureAwait(false);
            }

            await WaitForNextPollAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task PublishAsync(UpsSnapshot snapshot, CancellationToken cancellationToken)
    {
        SnapshotUpdated?.Invoke(snapshot);
        foreach (var upsEvent in _eventDetector.Observe(snapshot))
        {
            EventDetected?.Invoke(upsEvent);
            try
            {
                await _eventSink.WriteAsync(upsEvent, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                MonitorError?.Invoke(exception);
            }
        }
    }

    private async Task WaitForNextPollAsync(CancellationToken cancellationToken)
    {
        using var waitCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delay = Task.Delay(_pollIntervalMs, waitCancellation.Token);
        var wake = _wakeSignal.WaitAsync(waitCancellation.Token);
        var completed = await Task.WhenAny(delay, wake).ConfigureAwait(false);
        await waitCancellation.CancelAsync().ConfigureAwait(false);

        try
        {
            await completed.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void Wake()
    {
        if (_wakeSignal.CurrentCount == 0)
        {
            _wakeSignal.Release();
        }
    }

    private static int ValidatePollInterval(int milliseconds) =>
        milliseconds is >= 250 and <= 60_000
            ? milliseconds
            : throw new ArgumentOutOfRangeException(nameof(milliseconds), "Poll interval must be between 250 and 60000 ms.");
}
