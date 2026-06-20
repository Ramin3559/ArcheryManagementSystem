using EShooting.Domain.Enums;

namespace EShooting.Domain.Entities;

public sealed class SessionEquipmentIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid EquipmentItemId { get; set; }
    public EquipmentIssueType IssueType { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
