namespace EShooting.Web.Contracts.AccessProfiles;

public sealed class AccessProfileFormModel
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool CanRegisterCustomers { get; set; }
    public bool CanManageSubscriptions { get; set; }
    public bool CanManageSessions { get; set; }
    public bool CanManageEquipment { get; set; }
    public bool CanViewHistory { get; set; }
    public bool IsActive { get; set; } = true;
}
