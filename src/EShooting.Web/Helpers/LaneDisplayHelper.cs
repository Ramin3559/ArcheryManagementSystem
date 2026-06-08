using EShooting.Domain.Enums;

namespace EShooting.Web.Helpers;

/// <summary>
/// Zolaq monitoru üçün mətn formatı və tərcümə (UI ilə uyğun).
/// </summary>
public static class LaneDisplayHelper
{
    public static string TranslateStatus(string? status)
    {
        return status switch
        {
            "Idle" => "Boş",
            "Active" => "Aktiv",
            "Scheduled" => "Planlaşdırılıb",
            "Completed" => "Bitib",
            _ => status ?? "Boş"
        };
    }

    public static string TranslateLaneType(LaneType laneType) =>
        laneType switch
        {
            LaneType.Amateur => "Həvəskar",
            LaneType.Professional => "Peşəkar",
            _ => laneType.ToString()
        };

    public static string TranslateWarning(string? warning)
    {
        if (string.IsNullOrWhiteSpace(warning))
        {
            return "";
        }

        var w = warning.Trim();
        return w switch
        {
            "Ready" => "Hazırdır",
            "Time is over" => "Vaxt bitib",
            "1 minute remaining" => "1 dəqiqə qalıb",
            "5 minutes remaining" => "5 dəqiqə qalıb",
            "In progress" => "Davam edir",
            _ => w.StartsWith("Starts in ", StringComparison.Ordinal)
                ? $"Başlayır: {w["Starts in ".Length..]}"
                : w
        };
    }

    public static string WarningCssClass(string? warning)
    {
        if (string.IsNullOrEmpty(warning))
        {
            return "warn ok";
        }

        var w = warning.ToLowerInvariant();
        if (w.Contains("over") || w.Contains("bitib"))
        {
            return "warn danger";
        }

        if (w.Contains("minute") || warning.Contains("dəqiqə", StringComparison.Ordinal))
        {
            return "warn alert";
        }

        return "warn ok";
    }

    public static string FormatRemaining(DateTime? startTimeUtc, DateTime? endTimeUtc, string status)
    {
        if (startTimeUtc is null || endTimeUtc is null)
        {
            return "—";
        }

        if (status == "Completed")
        {
            return "Bitib";
        }

        var now = DateTime.UtcNow;
        var start = DateTime.SpecifyKind(startTimeUtc.Value, DateTimeKind.Utc);
        var end = DateTime.SpecifyKind(endTimeUtc.Value, DateTimeKind.Utc);

        if (now < start)
        {
            return $"Başlayır: {FormatDuration(start - now)}";
        }

        var remaining = end - now;
        if (remaining <= TimeSpan.Zero)
        {
            return "00:00";
        }

        return FormatDuration(remaining);
    }

    public static string BuildRemainingLabel(DateTime? startTimeUtc, DateTime? endTimeUtc, string status) =>
        $"Qalan vaxt: {FormatRemaining(startTimeUtc, endTimeUtc, status)}";

    private static string FormatDuration(TimeSpan span)
    {
        if (span < TimeSpan.Zero)
        {
            span = TimeSpan.Zero;
        }

        var totalSeconds = (int)Math.Floor(span.TotalSeconds);
        var hours = totalSeconds / 3600;
        var minutes = totalSeconds % 3600 / 60;
        var seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        return $"{minutes:D2}:{seconds:D2}";
    }

    public static string FormatTimeLocal(DateTime? utc) => AppTimeZone.FormatTimeLocal(utc);

    public static string ToIsoOffset(DateTime? utc) => AppTimeZone.ToIsoOffset(utc);

    /// <summary>
    /// Yerli tarix qaytarır. Bugünkü gün üçün boş sətr (göstərmirik), sabahkı üçün "Sabah",
    /// dünənki üçün "Dünən", əks halda "9 May, Şənbə" formatında çıxır.
    /// </summary>
    public static string FormatDateLocal(DateTime? utc)
    {
        if (utc is null)
        {
            return string.Empty;
        }

        var local = AppTimeZone.ToLocal(utc.Value).Date;
        var today = AppTimeZone.LocalToday;
        var diffDays = (local - today).Days;

        // Bu gündürsə tarix göstərmirik — yer qənaət etmək üçün boş qaytarırıq.
        if (diffDays == 0) return string.Empty;
        if (diffDays == 1) return "Sabah";
        if (diffDays == -1) return "Dünən";

        var az = new System.Globalization.CultureInfo("az-AZ");
        return local.ToString("d MMMM, dddd", az);
    }
}
