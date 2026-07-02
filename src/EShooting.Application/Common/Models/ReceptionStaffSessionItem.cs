namespace EShooting.Application.Common.Models;

/// <summary>Resepsiya PIN girişindən sonra sessiya məlumatı.</summary>
public sealed class ReceptionStaffSessionItem
{
    public Guid Id { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string PositionName { get; init; } = "—";
    public string AccessProfileName { get; init; } = "—";

    public bool CanRegisterCustomers { get; init; }
    public bool CanViewCustomerDetails { get; init; }
    public bool CanEditCustomerDetails { get; init; }
    public bool CanManageSubscriptions { get; init; }
    public bool CanRecordPayments { get; init; }
    public bool CanApplyDiscount { get; init; }
    public bool CanGrantComplimentarySession { get; init; }
    public bool CanManageSessions { get; init; }
    public bool CanManageEquipment { get; init; }
    public bool CanSellEquipment { get; init; }
    public bool CanReturnEquipment { get; init; }
    public bool CanAccessPlanset { get; init; }
    public bool CanIssueEquipmentRental { get; init; }
    public bool CanViewHistory { get; init; }
}
