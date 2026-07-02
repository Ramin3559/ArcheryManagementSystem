namespace EShooting.Application.Common;

/// <summary>
/// Resepsiya/TV ilə uyğun Bakı (UTC+4) vaxt çevrilməsi.
/// </summary>
public static class AzerbaijanTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(4);

    public static DateTime NormalizeScheduleInputToUtc(DateTime value)
    {
        return value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => new DateTimeOffset(value, Offset).UtcDateTime
        };
    }

    public static DateTime UtcToLocalDateTime(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Offset);

    public static DateTime UtcToLocalDate(DateTime utc) =>
        UtcToLocalDateTime(utc).Date;

    public static DateTime TodayLocal => UtcToLocalDate(DateTime.UtcNow);
}
