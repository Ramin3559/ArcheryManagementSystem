using EShooting.Domain.Enums;

namespace EShooting.Domain.Entities;

public sealed class SubscriptionSchedule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AthleteId { get; set; }
    public int LaneNumber { get; set; }
    public int DayOfWeek { get; set; }
    public TimeSpan StartTimeLocal { get; set; }
    public int DurationMinutes { get; set; }
    public DateTime ActiveFromDateLocal { get; set; }
    public DateTime ActiveToDateLocal { get; set; }
    public bool IsEnabled { get; set; } = true;
    public PreferredLaneType PreferredLaneType { get; set; } = PreferredLaneType.Any;
    public bool IsFullPackage { get; set; }
    public int? LastAssignedLaneNumber { get; set; }
    public DateTime? LastAutoStartedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// JSON massivi: ["yyyy-MM-dd", ...] — bu tarixlərdə təkrarlanan seans göstərilmir və avtomatik başlamır.
    /// </summary>
    public string? ExcludedOccurrenceDatesJson { get; set; }

    /// <summary>
    /// JSON massivi: [{ "dateLocal":"yyyy-MM-dd", "startTimeLocal":"HH:mm", "laneNumber":n, "durationMinutes":m }, ...]
    /// </summary>
    public string? OccurrenceOverridesJson { get; set; }
}
