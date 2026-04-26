using EShooting.Domain.Enums;

namespace EShooting.Domain.Entities;

public sealed class TrainingSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AthleteId { get; set; }
    public Guid LaneId { get; set; }
    public Guid? SubscriptionScheduleId { get; set; }
    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;
    public List<ScoreEntry> Scores { get; set; } = [];

    public bool IsEquipmentIssued { get; set; }
    public DateTime? EquipmentReturnedAtUtc { get; set; }

    public int TotalScore => Scores.Sum(x => x.Value);
}
