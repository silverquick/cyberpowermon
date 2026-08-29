namespace UpsMonitor.Core;

public enum UpsEventType
{
    PowerLost,
    PowerRestored,
    BatteryLow,
    BatteryCritical,
    RuntimeLow,
    OverloadDetected,
    UpsDisconnected,
    UpsReconnected,
    VoltageAbnormal,
    HighLoadWarning,
}

public enum UpsEventSeverity
{
    Information,
    Warning,
    Critical,
}

public sealed record UpsEvent(
    DateTimeOffset Timestamp,
    UpsEventType Type,
    string Message,
    UpsPowerState PreviousState,
    UpsPowerState CurrentState)
{
    public UpsEventSeverity Severity => Type switch
    {
        UpsEventType.BatteryCritical or UpsEventType.OverloadDetected => UpsEventSeverity.Critical,
        UpsEventType.PowerLost or UpsEventType.BatteryLow or UpsEventType.RuntimeLow
            or UpsEventType.UpsDisconnected or UpsEventType.VoltageAbnormal
            or UpsEventType.HighLoadWarning => UpsEventSeverity.Warning,
        _ => UpsEventSeverity.Information,
    };
}

public sealed class UpsEventDetector
{
    private TimeSpan _runtimeLowThreshold;
    private UpsSnapshot? _previous;
    private bool _runtimeWasLow;

    public UpsEventDetector(TimeSpan runtimeLowThreshold)
    {
        SetRuntimeLowThreshold(runtimeLowThreshold);
    }

    public void SetRuntimeLowThreshold(TimeSpan threshold)
    {
        if (threshold < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(threshold), "Runtime low threshold cannot be negative.");
        }

        _runtimeLowThreshold = threshold;
    }

    public IReadOnlyList<UpsEvent> Observe(UpsSnapshot current)
    {
        List<UpsEvent>? events = null;
        var previous = _previous;
        var previousState = previous is null
            ? UpsPowerState.Unknown
            : UpsPowerStateEvaluator.Evaluate(previous);
        var currentState = UpsPowerStateEvaluator.Evaluate(current);

        if (previous is null)
        {
            if (current.IsConnected)
            {
                Add(UpsEventType.UpsReconnected, $"UPS connected: {current.Device?.DisplayName ?? "Unknown device"}");
            }
        }
        else
        {
            if (previous.IsConnected && !current.IsConnected)
            {
                Add(UpsEventType.UpsDisconnected, "UPS disconnected");
            }
            else if (!previous.IsConnected && current.IsConnected)
            {
                Add(UpsEventType.UpsReconnected, $"UPS reconnected: {current.Device?.DisplayName ?? "Unknown device"}");
            }

            if (previous.AcPresent is true && current.AcPresent is false)
            {
                Add(UpsEventType.PowerLost, "AC power lost");
            }
            else if (previous.AcPresent is false && current.AcPresent is true)
            {
                Add(UpsEventType.PowerRestored, "AC power restored");
            }

            if (previous.LowBattery is not true && current.LowBattery is true)
            {
                Add(UpsEventType.BatteryLow, "Battery is below the remaining capacity limit");
            }

            if (previous.ShutdownImminent is not true && current.ShutdownImminent is true)
            {
                Add(UpsEventType.BatteryCritical, "UPS reports shutdown imminent");
            }

            if (previous.Overload is not true && current.Overload is true)
            {
                Add(UpsEventType.OverloadDetected, "UPS overload detected");
            }
        }

        var runtimeIsLow = current.IsConnected
            && current.RuntimeRemaining is { } runtime
            && runtime <= _runtimeLowThreshold;

        if (runtimeIsLow && !_runtimeWasLow)
        {
            Add(UpsEventType.RuntimeLow, $"UPS runtime is at or below {_runtimeLowThreshold.TotalMinutes:0.#} minutes");
        }

        _runtimeWasLow = runtimeIsLow;
        _previous = current;
        return (IReadOnlyList<UpsEvent>?)events ?? Array.Empty<UpsEvent>();

        void Add(UpsEventType type, string message)
        {
            events ??= new List<UpsEvent>();
            events.Add(new UpsEvent(current.Timestamp, type, message, previousState, currentState));
        }
    }
}
