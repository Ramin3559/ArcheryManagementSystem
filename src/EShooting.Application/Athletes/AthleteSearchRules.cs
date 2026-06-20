using EShooting.Domain.Entities;

namespace EShooting.Application.Athletes;

public static class AthleteSearchRules
{
    public static bool IsGroupSessionPlaceholder(Athlete athlete)
    {
        if (athlete.IsGroupPlaceholder)
        {
            return true;
        }

        return athlete.FullName.Contains(", ", StringComparison.Ordinal)
            && string.IsNullOrWhiteSpace(athlete.PhoneNumber)
            && string.IsNullOrWhiteSpace(athlete.FirstName)
            && string.IsNullOrWhiteSpace(athlete.LastName);
    }

    public static bool IsSearchable(Athlete athlete) => !IsGroupSessionPlaceholder(athlete);
}
