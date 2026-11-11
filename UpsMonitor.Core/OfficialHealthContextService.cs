namespace UpsMonitor.Core;

/// <summary>公式値記録時の状況を履歴から問い合わせる元(履歴DB)。</summary>
public interface IOfficialHealthContextSource
{
    Task<OfficialHealthContext> QueryOfficialHealthContextAsync(
        string deviceId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}

public static class OfficialHealthContextService
{
    /// <summary>記録時刻が現在とこの範囲内なら、瞬間値は履歴ではなく現在値を使う。</summary>
    public static readonly TimeSpan LiveWindow = TimeSpan.FromMinutes(5);

    /// <summary>
    /// 新規記録の状況を取得する。履歴取得が未完了なら、live 値があっても起動時補完の対象に残す。
    /// </summary>
    public static async Task CaptureForRecordAsync(
        OfficialHealthRecord record,
        IOfficialHealthContextSource? source,
        string deviceId,
        DateTimeOffset now,
        OfficialHealthContext? live,
        CancellationToken cancellationToken = default)
    {
        var (context, historyCompleted) = await CaptureWithStatusAsync(source, deviceId, record.RecordedAt, now, live, cancellationToken)
            .ConfigureAwait(false);
        record.Context = context;
        record.ContextBackfillAttempted = historyCompleted;
    }

    /// <summary>
    /// 記録時の状況を作る。履歴から取得し、記録時刻が現在に近く live があれば
    /// 瞬間値(live の非 null 項目)で上書きする。履歴が使えない場合も live のみで返す。
    /// 何も得られなければ null。
    /// </summary>
    public static async Task<OfficialHealthContext?> CaptureAsync(
        IOfficialHealthContextSource? source,
        string deviceId,
        DateTimeOffset recordedAt,
        DateTimeOffset now,
        OfficialHealthContext? live,
        CancellationToken cancellationToken = default)
    {
        var (context, _) = await CaptureWithStatusAsync(source, deviceId, recordedAt, now, live, cancellationToken)
            .ConfigureAwait(false);
        return context;
    }

    private static async Task<(OfficialHealthContext? Context, bool HistoryCompleted)> CaptureWithStatusAsync(
        IOfficialHealthContextSource? source,
        string deviceId,
        DateTimeOffset recordedAt,
        DateTimeOffset now,
        OfficialHealthContext? live,
        CancellationToken cancellationToken)
    {
        OfficialHealthContext? context = null;
        var historyCompleted = false;
        if (source is not null)
        {
            try
            {
                context = await source.QueryOfficialHealthContextAsync(deviceId, recordedAt, cancellationToken)
                    .ConfigureAwait(false);
                historyCompleted = true;
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // 履歴の取得失敗は記録自体を妨げない。
            }
        }

        if (live is not null && (recordedAt - now).Duration() <= LiveWindow)
        {
            context ??= new OfficialHealthContext();
            context.LoadPercent = live.LoadPercent ?? context.LoadPercent;
            context.ActivePowerWatts = live.ActivePowerWatts ?? context.ActivePowerWatts;
            context.RuntimeSeconds = live.RuntimeSeconds ?? context.RuntimeSeconds;
            context.BatteryVoltage = live.BatteryVoltage ?? context.BatteryVoltage;
            context.InputVoltage = live.InputVoltage ?? context.InputVoltage;
            context.TemperatureCelsius = live.TemperatureCelsius ?? context.TemperatureCelsius;
            context.Charging = live.Charging ?? context.Charging;
        }

        return (context is { HasAnyValue: true } ? context : null, historyCompleted);
    }

    /// <summary>
    /// 履歴取得が未完了の記録に状況を補完し、既存の非 null 値は維持する。
    /// 正常完了なら ContextBackfillAttempted を true にし、結果が空でも再試行しない。
    /// 戻り値は変更した(履歴取得を完了した)記録の数。
    /// </summary>
    public static async Task<int> BackfillAsync(
        IEnumerable<OfficialHealthRecord> records,
        string deviceId,
        IOfficialHealthContextSource source,
        CancellationToken cancellationToken = default)
    {
        var attempted = 0;
        foreach (var record in records.Where(item => !item.ContextBackfillAttempted).ToArray())
        {
            OfficialHealthContext context;
            try
            {
                context = await source.QueryOfficialHealthContextAsync(deviceId, record.RecordedAt, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                // 一時的な失敗は未試行のまま残し、次回起動で再試行する。
                continue;
            }

            if (record.Context is { } existing)
            {
                context = existing with
                {
                    LoadPercent = existing.LoadPercent ?? context.LoadPercent,
                    ActivePowerWatts = existing.ActivePowerWatts ?? context.ActivePowerWatts,
                    RuntimeSeconds = existing.RuntimeSeconds ?? context.RuntimeSeconds,
                    BatteryVoltage = existing.BatteryVoltage ?? context.BatteryVoltage,
                    InputVoltage = existing.InputVoltage ?? context.InputVoltage,
                    TemperatureCelsius = existing.TemperatureCelsius ?? context.TemperatureCelsius,
                    Charging = existing.Charging ?? context.Charging,
                    Load10MinuteMean = existing.Load10MinuteMean ?? context.Load10MinuteMean,
                    Load10MinuteMax = existing.Load10MinuteMax ?? context.Load10MinuteMax,
                    Load24HourMean = existing.Load24HourMean ?? context.Load24HourMean,
                    Load24HourMax = existing.Load24HourMax ?? context.Load24HourMax,
                    BatteryVoltage24HourMean = existing.BatteryVoltage24HourMean ?? context.BatteryVoltage24HourMean,
                    DaysSinceLastDischarge = existing.DaysSinceLastDischarge ?? context.DaysSinceLastDischarge,
                    HistoryStartedAt = existing.HistoryStartedAt ?? context.HistoryStartedAt,
                };
            }

            record.Context = context.HasAnyValue ? context : null;
            record.ContextBackfillAttempted = true;
            attempted++;
        }

        return attempted;
    }
}
