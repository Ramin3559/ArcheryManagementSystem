namespace EShooting.Domain.Entities;

/// <summary>Klub kartının vermə/qaytarma tarixçəsi.</summary>
public sealed class ClubCardAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string CardNumber { get; set; } = string.Empty;
    public Guid AthleteId { get; set; }
    public DateTime IssuedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReturnedAtUtc { get; set; }
    public Guid? IssuedByStaffId { get; set; }
    public Guid? ReturnedByStaffId { get; set; }
}
