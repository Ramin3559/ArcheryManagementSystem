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

    public static IReadOnlyList<string> ParseMany(string? value, int maxCount = 200)
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
            var n = Normalize(part);
            if (string.IsNullOrWhiteSpace(n) || !seen.Add(n))
            {
                continue;
            }

            result.Add(n);
            if (result.Count >= maxCount)
            {
                break;
            }
        }

        return result;
    }
}
