using EShooting.Domain.Enums;

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

    /// <summary>
    /// İstifadəçinin abunəni yaradanda seçdiyi konkret zolaq nömrəsi (0 = Sistem seçəcək).
    /// </summary>
    public int LaneNumber { get; init; }

    public PreferredLaneType PreferredLaneType { get; init; } = PreferredLaneType.Any;
    public bool IsFullPackage { get; init; }

    public int? LastAssignedLaneNumber { get; init; }
    public DateTime? LastAutoStartedAtUtc { get; init; }
}
