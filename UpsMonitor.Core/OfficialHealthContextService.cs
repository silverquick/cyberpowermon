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
    /// 新規記録の状況を取得する。状況が空なら起動時補完の対象に残す。
    /// 正常にデータなしと判定された場合も、起動時に一度だけ再確認する。
    /// </summary>
    public static async Task CaptureForRecordAsync(
        OfficialHealthRecord record,
        IOfficialHealthContextSource? source,
        string deviceId,
        DateTimeOffset now,
        OfficialHealthContext? live,
        CancellationToken cancellationToken = default)
    {
        record.Context = await CaptureAsync(source, deviceId, record.RecordedAt, now, live, cancellationToken)
            .ConfigureAwait(false);
        record.ContextBackfillAttempted = record.Context is not null;
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
        OfficialHealthContext? context = null;
        if (source is not null)
        {
            try
            {
                context = await source.QueryOfficialHealthContextAsync(deviceId, recordedAt, cancellationToken)
                    .ConfigureAwait(false);
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

        return context is { HasAnyValue: true } ? context : null;
    }

    /// <summary>
    /// Context が null かつ未試行の記録に履歴から状況を補完する。冪等: 試行した記録は
    /// ContextBackfillAttempted を true にし、結果が null でも再試行しない。
    /// 戻り値は変更した(試行した)記録の数。
    /// </summary>
    public static async Task<int> BackfillAsync(
        IEnumerable<OfficialHealthRecord> records,
        string deviceId,
        IOfficialHealthContextSource source,
        CancellationToken cancellationToken = default)
    {
        var attempted = 0;
        foreach (var record in records.Where(item => item.Context is null && !item.ContextBackfillAttempted).ToArray())
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

            record.Context = context.HasAnyValue ? context : null;
            record.ContextBackfillAttempted = true;
            attempted++;
        }

        return attempted;
    }
}
