using EShooting.Domain.Enums;

namespace EShooting.Web.Contracts.Subscriptions;

public sealed class CreateSubscriptionPackageRequest
{
    public string AthleteFullName { get; set; } = string.Empty;
    public string DayPattern { get; set; } = "1-3-5";
    public int VisitsCount { get; set; } = 12;
    public string StartTimeLocal { get; set; } = "19:00";
    public int DurationMinutes { get; set; } = 60;
    public DateTime StartDateLocal { get; set; }

    public Dictionary<int, PreferredLaneType> PreferredLaneTypesByDayOfWeek { get; set; } = new();
    public bool IsFullPackage { get; set; }
}
