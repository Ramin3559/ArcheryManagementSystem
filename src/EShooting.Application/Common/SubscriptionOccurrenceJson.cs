using System.Globalization;
using System.Text.Json;
using EShooting.Domain.Entities;

namespace EShooting.Application.Common;

public static class SubscriptionOccurrenceJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public sealed class OverrideRow
    {
        public string? DateLocal { get; set; }
        public string? StartTimeLocal { get; set; }
        public int? LaneNumber { get; set; }
        public int? DurationMinutes { get; set; }
    }

    public static HashSet<string> DeserializeExcluded(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json, Options) ?? [];
            return list
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToHashSet(StringComparer.Ordinal);
        }
        catch
        {
            return [];
        }
    }

    public static List<OverrideRow> DeserializeOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<OverrideRow>>(json, Options) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public static string? SerializeOverrides(IEnumerable<OverrideRow> rows)
    {
        var list = rows?.ToList() ?? [];
        if (list.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(list, Options);
    }

    public static bool IsExcluded(SubscriptionSchedule schedule, DateTime dateLocal)
    {
        var key = dateLocal.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return DeserializeExcluded(schedule.ExcludedOccurrenceDatesJson).Contains(key);
    }

    public static bool TryResolveOccurrence(
        SubscriptionSchedule schedule,
        DateTime dateLocal,
        out TimeSpan startTimeLocal,
        out int durationMinutes,
        out int laneNumber)
    {
        startTimeLocal = default;
        durationMinutes = 0;
        laneNumber = 0;

        if (!schedule.IsEnabled || schedule.IsFullPackage || schedule.DurationMinutes <= 0)
        {
            return false;
        }

        var day = dateLocal.Date;
        if (day < schedule.ActiveFromDateLocal.Date || day > schedule.ActiveToDateLocal.Date)
        {
            return false;
        }

        if (IsExcluded(schedule, day))
        {
            return false;
        }

        var key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var overrides = DeserializeOverrides(schedule.OccurrenceOverridesJson);
        var ov = overrides.FirstOrDefault(o =>
            string.Equals(o.DateLocal?.Trim(), key, StringComparison.Ordinal));

        var isNaturalDay = (int)day.DayOfWeek == schedule.DayOfWeek;
        if (!isNaturalDay && ov is null)
        {
            return false;
        }

        startTimeLocal = schedule.StartTimeLocal;
        durationMinutes = schedule.DurationMinutes;
        laneNumber = schedule.LaneNumber > 0
            ? schedule.LaneNumber
            : schedule.LastAssignedLaneNumber ?? 0;

        if (ov is not null)
        {
            if (!string.IsNullOrWhiteSpace(ov.StartTimeLocal)
                && TimeSpan.TryParse(ov.StartTimeLocal, CultureInfo.InvariantCulture, out var st))
            {
                startTimeLocal = st;
            }

            if (ov.DurationMinutes is > 0)
            {
                durationMinutes = ov.DurationMinutes.Value;
            }

            if (ov.LaneNumber is > 0)
            {
                laneNumber = ov.LaneNumber.Value;
            }
        }

        // Zolaq sonra/Aktiv et ilə təyin oluna bilər — müddət varsa plan sayılır.
        return durationMinutes > 0;
    }
}
