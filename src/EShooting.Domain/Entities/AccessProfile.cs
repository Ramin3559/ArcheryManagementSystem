namespace EShooting.Domain.Entities;

/// <summary>Resepsiya işçisi üçün icazə profili (sistem rolu).</summary>
public sealed class AccessProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public bool CanRegisterCustomers { get; set; }
    public bool CanManageSubscriptions { get; set; }
    public bool CanManageSessions { get; set; }
    public bool CanManageEquipment { get; set; }
    public bool CanViewHistory { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
