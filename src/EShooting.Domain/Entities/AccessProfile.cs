namespace EShooting.Domain.Entities;

/// <summary>Resepsiya işçisi üçün icazə profili (sistem rolu).</summary>
public sealed class AccessProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public bool CanRegisterCustomers { get; set; }
    public bool CanViewCustomerDetails { get; set; }
    public bool CanEditCustomerDetails { get; set; }
    public bool CanManageSubscriptions { get; set; }
    public bool CanRecordPayments { get; set; }
    public bool CanApplyDiscount { get; set; }
    public bool CanGrantComplimentarySession { get; set; }
    public bool CanManageSessions { get; set; }
    public bool CanManageEquipment { get; set; }
    public bool CanSellEquipment { get; set; }
    public bool CanReturnEquipment { get; set; }
    public bool CanAccessPlanset { get; set; }
    public bool CanIssueEquipmentRental { get; set; }
    public bool CanViewHistory { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
