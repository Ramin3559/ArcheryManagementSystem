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

    public static string FormatScheduleTimeLocal(DateTime? utc) => AppTimeZone.FormatScheduleTimeLocal(utc);

    public static string ToIsoOffset(DateTime? utc) => AppTimeZone.ToIsoOffset(utc);

    /// <summary>
    /// Köhnə "Qrup:" / "Atıcı:" prefikslərini silir (DB-də qalmış qeydlər üçün).
    /// </summary>
    public static string StripAthleteLabelPrefix(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "";
        }

        var s = name.Trim();
        if (s.StartsWith("Qrup:", StringComparison.OrdinalIgnoreCase))
        {
            return s[5..].TrimStart();
        }

        if (s.StartsWith("Atıcı:", StringComparison.OrdinalIgnoreCase))
        {
            return s[6..].TrimStart();
        }

        return s;
    }

    /// <summary>TV və zolaq kartlarında: "Atıcı: Ad Soyad" (tək və ya qrup).</summary>
    public static string FormatAthleteLabel(string? name)
    {
        var core = StripAthleteLabelPrefix(name);
        return string.IsNullOrEmpty(core) ? "Atıcı: —" : $"Atıcı: {core}";
    }

    /// <summary>TV ekranı üçün Ad və Soyadı ayırır (DB sahələri və ya FullName-dən).</summary>
    public static (string First, string Last) SplitAthleteDisplayName(
        string? fullName,
        string? firstName = null,
        string? lastName = null)
    {
        if (!string.IsNullOrWhiteSpace(firstName) || !string.IsNullOrWhiteSpace(lastName))
        {
            return (firstName?.Trim() ?? "", lastName?.Trim() ?? "");
        }

        var core = StripAthleteLabelPrefix(fullName);
        if (string.IsNullOrEmpty(core))
        {
            return ("", "");
        }

        if (IsGroupAthleteName(fullName))
        {
            return (core, "");
        }

        var spaceIdx = core.IndexOf(' ');
        if (spaceIdx <= 0)
        {
            return (core, "");
        }

        return (core[..spaceIdx], core[(spaceIdx + 1)..].Trim());
    }

    public sealed record GroupAthleteDisplayLine(string First, string Last, bool TrailingComma);

    /// <summary>Qrup sessiyası üçün TV ekranı: hər adam Ad / Soyad, vergül alt-alta.</summary>
    public static IReadOnlyList<GroupAthleteDisplayLine> ParseGroupAthleteDisplayLines(
        string? fullName,
        string? firstName = null,
        string? lastName = null)
    {
        var core = StripAthleteLabelPrefix(fullName);
        if (string.IsNullOrWhiteSpace(core) && string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName))
        {
            return Array.Empty<GroupAthleteDisplayLine>();
        }

        if (!IsGroupAthleteName(fullName))
        {
            var (first, last) = SplitAthleteDisplayName(fullName, firstName, lastName);
            return [new GroupAthleteDisplayLine(first, last, false)];
        }

        var parts = core.Split(", ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var lines = new List<GroupAthleteDisplayLine>(parts.Length);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i].Trim();
            var spaceIdx = part.IndexOf(' ');
            string first;
            string last;
            if (spaceIdx <= 0)
            {
                first = part;
                last = "";
            }
            else
            {
                first = part[..spaceIdx];
                last = part[(spaceIdx + 1)..].Trim();
            }

            lines.Add(new GroupAthleteDisplayLine(first, last, i < parts.Length - 1));
        }

        return lines;
    }

    public static bool IsGroupAthleteName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.TrimStart().StartsWith("Qrup:", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var core = StripAthleteLabelPrefix(name);
        return core.Contains(", ", StringComparison.Ordinal);
    }

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
