using EShooting.Domain.Enums;

namespace EShooting.Web.Contracts.Subscriptions;

public sealed class CreateSubscriptionScheduleWithExceptionsRequest
{
    public Guid? AthleteId { get; set; }
    public string AthleteFullName { get; set; } = string.Empty;
    public int DayOfWeek { get; set; }
    public string StartTimeLocal { get; set; } = "19:00";
    public int DurationMinutes { get; set; } = 90;
    public DateTime ActiveFromDateLocal { get; set; }
    public DateTime ActiveToDateLocal { get; set; }
    public PreferredLaneType PreferredLaneType { get; set; } = PreferredLaneType.Any;
    public int LaneNumber { get; set; } = 0;
    public bool IsFullPackage { get; set; }

    public List<ScheduleExceptionOverride> Overrides { get; set; } = new();
}

public sealed class ScheduleExceptionOverride
{
    public DateTime DateLocal { get; set; }
    public int LaneNumber { get; set; }
    public string StartTimeLocal { get; set; } = "19:00";
}

