using System.Globalization;

namespace EShooting.Application.Athletes;

public static class ClubCardNumberRules
{
    public const int MaxRangeCount = 2000;

    public static string? Normalize(string? value)
    {
        var digits = AthleteRegistrationRules.NormalizeDigits(value);
        if (string.IsNullOrWhiteSpace(digits))
        {
            return null;
        }

        if (!int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1)
        {
            return null;
        }

        return Format(n);
    }

    public static string Format(int number)
        => number < 1000
            ? number.ToString("000", CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture);

    public static bool Same(string? a, string? b)
    {
        var na = Normalize(a) ?? AthleteRegistrationRules.NormalizeText(a) ?? "";
        var nb = Normalize(b) ?? AthleteRegistrationRules.NormalizeText(b) ?? "";
        return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryParseRange(int from, int to, out string? error)
    {
        error = null;
        if (from < 1 || to < 1)
        {
            error = "Başlanğıc və bitmə 1-dən kiçik ola bilməz.";
            return false;
        }

        if (to < from)
        {
            error = "Bitmə nömrəsi başlanğıcdan kiçik ola bilməz.";
            return false;
        }

        if (to - from + 1 > MaxRangeCount)
        {
            error = $"Bir dəfəyə ən çox {MaxRangeCount} kart əlavə edilə bilər.";
            return false;
        }

        return true;
    }

    public static IReadOnlyList<string> ParseMany(string? value, int maxCount = MaxRangeCount)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var parts = value.Split([',', ';', '\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in parts)
        {
            if (part.Contains('-', StringComparison.Ordinal))
            {
                if (!TryParseRun(part, out var from, out var to))
                {
                    continue;
                }

                for (var n = from; n <= to; n++)
                {
                    var formatted = Format(n);
                    if (!seen.Add(formatted))
                    {
                        continue;
                    }

                    result.Add(formatted);
                    if (result.Count >= maxCount)
                    {
                        return result;
                    }
                }

                continue;
            }

            var single = Normalize(part);
            if (string.IsNullOrWhiteSpace(single) || !seen.Add(single))
            {
                continue;
            }

            result.Add(single);
            if (result.Count >= maxCount)
            {
                break;
            }
        }

        return result;
    }

    public static string Compact(IReadOnlyList<string> numbers)
    {
        var ints = new List<int>();
        var seen = new HashSet<int>();
        foreach (var number in numbers)
        {
            var normalized = Normalize(number);
            if (normalized is null
                || !int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var n)
                || !seen.Add(n))
            {
                continue;
            }

            ints.Add(n);
        }

        ints.Sort();
        if (ints.Count == 0)
        {
            return "";
        }

        var parts = new List<string>();
        var runStart = ints[0];
        var runEnd = ints[0];
        for (var i = 1; i < ints.Count; i++)
        {
            if (ints[i] == runEnd + 1)
            {
                runEnd = ints[i];
                continue;
            }

            parts.Add(FormatRun(runStart, runEnd));
            runStart = runEnd = ints[i];
        }

        parts.Add(FormatRun(runStart, runEnd));
        return string.Join(", ", parts);
    }

    public static string CompactForDisplay(IReadOnlyList<string> numbers)
        => Compact(numbers).Replace("-", "–", StringComparison.Ordinal);

    private static string FormatRun(int from, int to)
        => from == to ? Format(from) : $"{Format(from)}-{Format(to)}";

    private static bool TryParseRun(string part, out int from, out int to)
    {
        from = 0;
        to = 0;
        var dash = part.IndexOf('-');
        if (dash <= 0 || dash >= part.Length - 1 || part.IndexOf('-', dash + 1) >= 0)
        {
            return false;
        }

        var left = Normalize(part[..dash]);
        var right = Normalize(part[(dash + 1)..]);
        if (left is null
            || right is null
            || !int.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out from)
            || !int.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out to)
            || !TryParseRange(from, to, out _))
        {
            from = 0;
            to = 0;
            return false;
        }

        return true;
    }
}
