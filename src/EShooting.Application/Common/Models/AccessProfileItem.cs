namespace EShooting.Application.Common.Models;

public sealed class AccessProfileItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool CanRegisterCustomers { get; init; }
    public bool CanManageSubscriptions { get; init; }
    public bool CanManageSessions { get; init; }
    public bool CanManageEquipment { get; init; }
    public bool CanViewHistory { get; init; }
    public bool IsActive { get; init; }
}
