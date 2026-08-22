namespace UpsMonitor.Core;

public sealed record UpsSnapshot
{
    public required DateTimeOffset Timestamp { get; init; }
    public bool IsConnected { get; init; }
    public UpsDeviceInfo? Device { get; init; }

    public bool? AcPresent { get; init; }
    public bool? Charging { get; init; }
    public bool? Discharging { get; init; }
    public double? BatteryPercent { get; init; }
    public TimeSpan? RuntimeRemaining { get; init; }
    public bool? LowBattery { get; init; }
    public bool? NeedReplacement { get; init; }
    public bool? ShutdownImminent { get; init; }
    public bool? Overload { get; init; }

    public double? Voltage { get; init; }
    public double? Current { get; init; }
    public double? Frequency { get; init; }
    public double? Temperature { get; init; }
    public TimeSpan? RemainingTimeLimit { get; init; }
    public double? DesignCapacity { get; init; }
    public double? FullChargeCapacity { get; init; }
    public double? CycleCount { get; init; }

    public double? BatteryVoltage { get; init; }
    public double? NominalBatteryVoltage { get; init; }
    public double? InputVoltage { get; init; }
    public double? OutputVoltage { get; init; }
    public double? PercentLoad { get; init; }
    public double? ActivePower { get; init; }
    public double? ApparentPower { get; init; }
    public double? ConfigVoltage { get; init; }
    public double? ConfigActivePower { get; init; }
    public double? ConfigApparentPower { get; init; }
    public double? LowVoltageTransfer { get; init; }
    public double? HighVoltageTransfer { get; init; }
    public double? RemainingCapacityLimit { get; init; }
    public double? WarningCapacityLimit { get; init; }
    public double? CapacityGranularity1 { get; init; }
    public double? CapacityGranularity2 { get; init; }
    public bool? FullyCharged { get; init; }
    public bool? RemainingTimeLimitExpired { get; init; }
    public bool? Rechargeable { get; init; }
    public bool? Boost { get; init; }
    public TimeSpan? DelayBeforeStartup { get; init; }
    public TimeSpan? DelayBeforeShutdown { get; init; }
    public string? CapacityMode { get; init; }
    public string? AudibleAlarmState { get; init; }
    public string? SelfTestState { get; init; }
    public IReadOnlyList<UpsTelemetryItem> Telemetry { get; init; } = [];

    public static UpsSnapshot Disconnected(DateTimeOffset timestamp) => new()
    {
        Timestamp = timestamp,
        IsConnected = false,
    };
}
