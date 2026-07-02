namespace EShooting.Application.Athletes;

public static class AthleteRegistrationRules
{
    public const string RequiredFieldsMessage =
        "Ad, Soyad, Telefon, Email, Ş/V nömrəsi və Kart nömrəsi mütləqdir.";

    public static bool HasRequiredContactFields(
        string? firstName,
        string? lastName,
        string? phoneNumber,
        string? email,
        string? idCardNumber,
        string? clubCardNumber)
    {
        return !string.IsNullOrWhiteSpace(firstName)
            && !string.IsNullOrWhiteSpace(lastName)
            && !string.IsNullOrWhiteSpace(NormalizeDigits(phoneNumber))
            && !string.IsNullOrWhiteSpace(NormalizeEmail(email))
            && !string.IsNullOrWhiteSpace(NormalizeText(idCardNumber))
            && !string.IsNullOrWhiteSpace(NormalizeText(clubCardNumber));
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
