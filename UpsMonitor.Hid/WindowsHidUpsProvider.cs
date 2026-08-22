using UpsMonitor.Core;

namespace UpsMonitor.Hid;

public sealed class WindowsHidUpsProvider : IUpsProvider
{
    private HidDeviceSession? _session;

    public UpsDeviceInfo? Device { get; private set; }

    public async Task<bool> ConnectAsync(CancellationToken cancellationToken)
    {
        if (_session is not null)
        {
            return true;
        }

        var candidates = await Task.Run(HidDeviceEnumerator.EnumerateCandidates, cancellationToken).ConfigureAwait(false);
        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                _session = HidDeviceSession.Open(candidate);
                Device = candidate.Device;
                return true;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _session?.Dispose();
                _session = null;
            }
        }

        Device = null;
        return false;
    }

    public async Task<UpsSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        var session = _session ?? throw new InvalidOperationException("No UPS HID device is connected.");
        var device = Device ?? throw new InvalidOperationException("UPS device metadata is unavailable.");
        var values = await Task.Run(session.ReadValues, cancellationToken).ConfigureAwait(false);
        return UpsHidMapper.Map(device, session.Descriptor, values);
    }

    public void Disconnect()
    {
        var session = Interlocked.Exchange(ref _session, null);
        Device = null;
        session?.Dispose();
    }

    public ValueTask DisposeAsync()
    {
        Disconnect();
        return ValueTask.CompletedTask;
    }
}
