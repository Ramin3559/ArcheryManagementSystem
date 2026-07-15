using System.Globalization;

namespace EShooting.Web.Helpers;

/// <summary>
/// HTML number input və InvariantCulture ilə yazılmış dəyərləri AZ mədəniyyətinə bağlayan
/// model binder-dən doğru oxumaq üçün (AZ-də '.' minlik ayırıcısıdır, '10.5' → 105 olur).
/// </summary>
public static class InvariantDecimalParser
{
    private static readonly CultureInfo Az = CultureInfo.GetCultureInfo("az-Latn-AZ");

    public static decimal? ParseOptional(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var text = raw.Trim().Replace('\u00A0', ' ').Replace(" ", "");
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var invariant))
        {
            return invariant;
        }

        if (decimal.TryParse(text, NumberStyles.Number, Az, out var az))
        {
            return az;
        }

        return null;
    }

    public static decimal? ParseOptionalPositiveOrNull(string? raw)
    {
        var value = ParseOptional(raw);
        if (value is null || value <= 0m)
        {
            return null;
        }

        return value;
    }
}
