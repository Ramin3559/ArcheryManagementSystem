using EShooting.Domain.Enums;

namespace EShooting.Application.Common.Models;

public sealed class LaneDashboardItem
{
    public Guid? SessionId { get; init; }
    public int ScoreCount { get; init; }
    public int LaneNumber { get; init; }
    public LaneType LaneType { get; init; }
    public string? AthleteName { get; init; }
    public string? AthleteFirstName { get; init; }
    public string? AthleteLastName { get; init; }
    public MembershipType? AthleteMembershipType { get; init; }
    public IReadOnlyCollection<string> QueueAthleteNames { get; init; } = [];
    public DateTime? StartTimeUtc { get; init; }
    public DateTime? EndTimeUtc { get; init; }
    public DateTime? CooldownUntilUtc { get; init; }
    public int TotalScore { get; init; }
    public string Status { get; init; } = "Idle";
    public string Warning { get; init; } = "Ready";

    public bool IsEquipmentIssued { get; init; }
    public bool IsEquipmentReturned { get; init; }
    public bool HasPendingRentalEquipment { get; init; }
    public bool IsSessionOpen { get; init; }
    /// <summary>VIP / müddətsiz sessiya — TV-də geri sayım yox, artan vaxt.</summary>
    public bool IsOpenEndedSession { get; init; }
    public bool IsAthleteVip { get; init; }
}
