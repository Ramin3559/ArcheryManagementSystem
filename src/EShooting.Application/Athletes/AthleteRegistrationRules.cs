namespace EShooting.Application.Athletes;

public static class AthleteRegistrationRules
{
    public const string RequiredFieldsMessage =
        "Ad, Soyad, Telefon və Ş/V nömrəsi mütləqdir.";

    public static bool HasRequiredContactFields(
        string? firstName,
        string? lastName,
        string? phoneNumber,
        string? idCardNumber)
    {
        return !string.IsNullOrWhiteSpace(firstName)
            && !string.IsNullOrWhiteSpace(lastName)
            && !string.IsNullOrWhiteSpace(NormalizeDigits(phoneNumber))
            && !string.IsNullOrWhiteSpace(NormalizeText(idCardNumber));
    }

    public static string? NormalizeOptionalEmail(string? value)
    {
        var email = NormalizeEmail(value);
        return string.IsNullOrWhiteSpace(email) ? null : email;
    }

    public static string? NormalizeOptionalText(string? value)
    {
        var text = NormalizeText(value);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public static string NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsDigit).ToArray());
    }

    public static string NormalizeEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim().ToLowerInvariant();
    }

    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.Trim();
    }
}
