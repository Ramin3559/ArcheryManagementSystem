namespace EShooting.Web.Contracts.Subscriptions;

public sealed class CreateSubscriptionScheduleRequest
{
    public string AthleteFullName { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }
    public string StartTimeLocal { get; set; } = "19:00";
    public int DurationMinutes { get; set; } = 60;
    public DateTime ActiveFromDateLocal { get; set; }
    public DateTime ActiveToDateLocal { get; set; }
}
