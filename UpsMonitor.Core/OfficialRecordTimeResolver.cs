using System.Globalization;

namespace UpsMonitor.Core;

public enum OfficialRecordTimeError
{
    None,
    InvalidTimeFormat,
    Future,
}

public readonly record struct OfficialRecordTimeResult(
    DateTimeOffset? RecordedAt,
    OfficialRecordTimeError Error);

public static class OfficialRecordTimeResolver
{
    /// <summary>
    /// Resolves wall-clock input using the date and UTC offset supplied by now.
    /// No system clock or machine time zone is consulted.
    /// </summary>
    public static OfficialRecordTimeResult Resolve(DateTime? date, string? timeText, DateTimeOffset now)
    {
        var day = DateTime.SpecifyKind((date ?? now.Date).Date, DateTimeKind.Unspecified);
        DateTimeOffset recordedAt;
        if (string.IsNullOrWhiteSpace(timeText))
        {
            recordedAt = day == now.Date ? now : new DateTimeOffset(day, now.Offset);
        }
        else
        {
            if (!TimeOnly.TryParseExact(timeText.Trim(), ["H:mm", "HH:mm"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                return new(null, OfficialRecordTimeError.InvalidTimeFormat);
            }

            recordedAt = new DateTimeOffset(day + time.ToTimeSpan(), now.Offset);
        }

        return recordedAt - now > TimeSpan.FromMinutes(5)
            ? new(null, OfficialRecordTimeError.Future)
            : new(recordedAt, OfficialRecordTimeError.None);
    }
}
