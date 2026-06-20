namespace EShooting.Web.Auth;

public static class ReceptionStaffClaims
{
    public const string Role = "ReceptionStaff";

    public const string StaffId = "staff_id";
    public const string PositionName = "staff_position";
    public const string AccessProfileName = "staff_access_profile";

    public const string CanRegisterCustomers = "perm_register_customers";
    public const string CanManageSubscriptions = "perm_manage_subscriptions";
    public const string CanManageSessions = "perm_manage_sessions";
    public const string CanManageEquipment = "perm_manage_equipment";
    public const string CanViewHistory = "perm_view_history";
}
