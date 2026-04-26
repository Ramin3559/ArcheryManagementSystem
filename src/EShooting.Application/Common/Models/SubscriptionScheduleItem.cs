namespace EShooting.Application.Common.Models;

public sealed class SubscriptionScheduleItem
{
    public Guid Id { get; init; }
    public Guid AthleteId { get; init; }
    public string AthleteName { get; init; } = string.Empty;
    public int DayOfWeek { get; init; }
    public TimeSpan StartTimeLocal { get; init; }
    public int DurationMinutes { get; init; }
    public DateTime ActiveFromDateLocal { get; init; }
    public DateTime ActiveToDateLocal { get; init; }
    public bool IsEnabled { get; init; }
    public int? LastAssignedLaneNumber { get; init; }
    public DateTime? LastAutoStartedAtUtc { get; init; }
}
