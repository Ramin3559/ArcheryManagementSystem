namespace EShooting.Domain.Entities;

public sealed class ScoreEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public int RoundNumber { get; set; }
    public int Value { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
