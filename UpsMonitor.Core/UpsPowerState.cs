namespace UpsMonitor.Core;

public enum UpsPowerState
{
    Unknown,
    Online,
    OnBattery,
    LowBattery,
    Critical,
}

public static class UpsPowerStateEvaluator
{
    public static UpsPowerState Evaluate(UpsSnapshot snapshot)
    {
        if (!snapshot.IsConnected)
        {
            return UpsPowerState.Unknown;
        }

        if (snapshot.ShutdownImminent is true)
        {
            return UpsPowerState.Critical;
        }

        if (snapshot.LowBattery is true)
        {
            return UpsPowerState.LowBattery;
        }

        if (snapshot.AcPresent is false || snapshot.Discharging is true)
        {
            return UpsPowerState.OnBattery;
        }

        return snapshot.AcPresent is true ? UpsPowerState.Online : UpsPowerState.Unknown;
    }
}
