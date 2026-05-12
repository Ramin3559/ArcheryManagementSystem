using System.Text.Json;

namespace EShooting.Web;

internal static class OccurrenceJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal sealed class OverrideRow
    {
        public string? DateLocal { get; set; }
        public string? StartTimeLocal { get; set; }
        public int? LaneNumber { get; set; }
        public int? DurationMinutes { get; set; }
    }

    internal static HashSet<string> DeserializeExcluded(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var list = JsonSerializer.Deserialize<List<string>>(json, Options) ?? [];
            return list.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).ToHashSet(StringComparer.Ordinal);
        }
        catch
        {
            return [];
        }
    }

    internal static string SerializeExcluded(IEnumerable<string> dates)
    {
        var ordered = dates
            .Distinct(StringComparer.Ordinal)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        return JsonSerializer.Serialize(ordered, Options);
    }

    internal static List<OverrideRow> DeserializeOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<OverrideRow>>(json, Options) ?? [];
        }
        catch
        {
            return [];
        }
    }

    internal static string SerializeOverrides(List<OverrideRow> rows)
    {
        return JsonSerializer.Serialize(rows, Options);
    }

    internal static Dictionary<string, OverrideRow> OverridesToMap(IEnumerable<OverrideRow> rows)
    {
        var map = new Dictionary<string, OverrideRow>(StringComparer.Ordinal);
        foreach (var o in rows)
        {
            if (string.IsNullOrWhiteSpace(o.DateLocal)) continue;
            map[o.DateLocal.Trim()] = o;
        }

        return map;
    }
}
