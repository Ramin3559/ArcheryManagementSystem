using System.Globalization;
using EShooting.Application.Common;

namespace EShooting.Web.Helpers;

/// <summary>
/// Bütün UI vaxt göstərişləri üçün sabit Azərbaycan (UTC+4) qurşağı.
/// Server Linux-da UTC olsa belə, resepsiya/TV ilə eyni saat görünsün.
/// </summary>
public static class AppTimeZone
{
    private static readonly TimeZoneInfo Azerbaijan = ResolveAzerbaijan();
    private static readonly TimeSpan AzerbaijanOffset = TimeSpan.FromHours(4);

    public static DateTime ToLocal(DateTime utc)
    {
        var u = DateTimeAssumedUtc.AsUtc(utc);
        return TimeZoneInfo.ConvertTimeFromUtc(u, Azerbaijan);
    }

    public static string FormatTimeLocal(DateTime? utc)
    {
        if (utc is null)
        {
            return "—";
        }

        return ToLocal(utc.Value).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>TV Başlama/Bitmə — saniyəsiz (HH:mm).</summary>
    public static string FormatScheduleTimeLocal(DateTime? utc)
    {
        if (utc is null)
        {
            return "—";
        }

        return ToLocal(utc.Value).ToString("HH:mm", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// JS <c>parseApiDate</c> üçün offset-li ISO (məs. +04:00).
    /// </summary>
    public static string ToIsoOffset(DateTime? utc)
    {
        if (utc is null)
        {
            return string.Empty;
        }

        var local = ToLocal(utc.Value);
        return new DateTimeOffset(local, AzerbaijanOffset).ToString("o", CultureInfo.InvariantCulture);
    }

    public static DateTime LocalToday => ToLocal(DateTime.UtcNow).Date;

    private static TimeZoneInfo ResolveAzerbaijan()
    {
        foreach (var id in new[] { "Asia/Baku", "Azerbaijan Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
                // try next
            }
            catch (InvalidTimeZoneException)
            {
                // try next
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Asia/Baku",
            AzerbaijanOffset,
            "Azerbaijan",
            "Azerbaijan");
    }
}
