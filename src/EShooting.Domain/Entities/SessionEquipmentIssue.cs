using EShooting.Domain.Enums;

namespace EShooting.Domain.Entities;

/// <summary>Avadanlıq verilməsi / satışı — hesabat üçün snapshot ilə.</summary>
public sealed class SessionEquipmentIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public Guid EquipmentItemId { get; set; }
    public EquipmentIssueType IssueType { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public Guid? IssuedByStaffId { get; set; }
    public Guid? ReturnedByStaffId { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public decimal LineTotal => UnitPrice * Quantity;
}
