namespace EShooting.Web.Auth;

public static class ReceptionStaffClaims
{
    public const string Role = "ReceptionStaff";

    public const string StaffId = "staff_id";
    public const string PositionName = "staff_position";
    public const string AccessProfileName = "staff_access_profile";

    public const string CanRegisterCustomers = "perm_register_customers";
    public const string CanViewCustomerDetails = "perm_view_customers";
    public const string CanEditCustomerDetails = "perm_edit_customers";
    public const string CanManageSubscriptions = "perm_manage_subscriptions";
    public const string CanRecordPayments = "perm_record_payments";
    public const string CanApplyDiscount = "perm_apply_discount";
    public const string CanGrantComplimentarySession = "perm_complimentary_session";
    public const string CanManageSessions = "perm_manage_sessions";
    public const string CanManageEquipment = "perm_manage_equipment";
    public const string CanSellEquipment = "perm_sell_equipment";
    public const string CanReturnEquipment = "perm_return_equipment";
    public const string CanAccessPlanset = "perm_access_planset";
    public const string CanIssueEquipmentRental = "perm_issue_equipment_rental";
    public const string CanViewHistory = "perm_view_history";

    public static readonly string[] AllPermissionClaims =
    [
        CanRegisterCustomers,
        CanViewCustomerDetails,
        CanEditCustomerDetails,
        CanManageSubscriptions,
        CanRecordPayments,
        CanApplyDiscount,
        CanGrantComplimentarySession,
        CanManageSessions,
        CanManageEquipment,
        CanSellEquipment,
        CanReturnEquipment,
        CanAccessPlanset,
        CanIssueEquipmentRental,
        CanViewHistory
    ];
}
