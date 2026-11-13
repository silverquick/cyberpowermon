using System.ComponentModel;
using System.Globalization;
using UpsMonitor.Core;

namespace UpsMonitor.App;

public sealed class UpsEventViewModel : INotifyPropertyChanged
{
    private readonly UpsEvent _source;

    public UpsEventViewModel(UpsEvent upsEvent)
    {
        _source = upsEvent;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public UpsEventSeverity Severity => _source.Severity;

    public string Timestamp => _source.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.CurrentCulture);

    public string Type => _source.Type switch
    {
        UpsEventType.PowerLost => LocalizationManager.Get("EventPowerLost"),
        UpsEventType.PowerRestored => LocalizationManager.Get("EventPowerRestored"),
        UpsEventType.BatteryLow => LocalizationManager.Get("EventBatteryLow"),
        UpsEventType.BatteryCritical => LocalizationManager.Get("EventBatteryCritical"),
        UpsEventType.RuntimeLow => LocalizationManager.Get("EventRuntimeLow"),
        UpsEventType.OverloadDetected => LocalizationManager.Get("EventOverload"),
        UpsEventType.UpsDisconnected => LocalizationManager.Get("EventUpsDisconnected"),
        UpsEventType.UpsReconnected => LocalizationManager.Get("EventUpsReconnected"),
        UpsEventType.VoltageAbnormal => LocalizationManager.Get("EventVoltageAbnormal"),
        UpsEventType.HighLoadWarning => LocalizationManager.Get("EventHighLoadWarning"),
        _ => _source.Type.ToString(),
    };

    public string Message => LocalizationManager.Get(_source.Type switch
    {
        UpsEventType.PowerLost => "EventMessagePowerLost",
        UpsEventType.PowerRestored => "EventMessagePowerRestored",
        UpsEventType.BatteryLow => "EventMessageBatteryLow",
        UpsEventType.BatteryCritical => "EventMessageBatteryCritical",
        UpsEventType.RuntimeLow => "EventMessageRuntimeLow",
        UpsEventType.OverloadDetected => "EventMessageOverload",
        UpsEventType.UpsDisconnected => "EventMessageUpsDisconnected",
        UpsEventType.UpsReconnected => "EventMessageUpsReconnected",
        UpsEventType.VoltageAbnormal => "EventMessageVoltageAbnormal",
        UpsEventType.HighLoadWarning => "EventMessageHighLoadWarning",
        _ => _source.Message,
    });

    public string StateTransition => $"{LocalizeState(_source.PreviousState)} → {LocalizeState(_source.CurrentState)}";

    public void RefreshLanguage()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Timestamp)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Type)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Message)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateTransition)));
    }

    private static string LocalizeState(UpsPowerState state) => state switch
    {
        UpsPowerState.Online => LocalizationManager.Get("StateOnline"),
        UpsPowerState.OnBattery => LocalizationManager.Get("StateOnBattery"),
        UpsPowerState.LowBattery => LocalizationManager.Get("StateLowBattery"),
        UpsPowerState.Critical => LocalizationManager.Get("StateCritical"),
        _ => LocalizationManager.Get("StateUnknown"),
    };
}
