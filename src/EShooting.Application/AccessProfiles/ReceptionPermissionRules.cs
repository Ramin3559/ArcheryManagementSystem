using EShooting.Domain.Entities;

namespace EShooting.Application.AccessProfiles;

/// <summary>Resepsiya və planşet üçün sistem icazələri.</summary>
public static class ReceptionPermissionRules
{
    public static bool HasAny(
        bool canRegisterCustomers,
        bool canViewCustomerDetails,
        bool canEditCustomerDetails,
        bool canManageSubscriptions,
        bool canRecordPayments,
        bool canApplyDiscount,
        bool canGrantComplimentarySession,
        bool canManageSessions,
        bool canManageEquipment,
        bool canSellEquipment,
        bool canReturnEquipment,
        bool canAccessPlanset,
        bool canIssueEquipmentRental,
        bool canViewHistory) =>
        canRegisterCustomers
        || canViewCustomerDetails
        || canEditCustomerDetails
        || canManageSubscriptions
        || canRecordPayments
        || canApplyDiscount
        || canGrantComplimentarySession
        || canManageSessions
        || canManageEquipment
        || canSellEquipment
        || canReturnEquipment
        || canAccessPlanset
        || canIssueEquipmentRental
        || canViewHistory;

    public static bool HasAny(AccessProfile profile) =>
        HasAny(
            profile.CanRegisterCustomers,
            profile.CanViewCustomerDetails,
            profile.CanEditCustomerDetails,
            profile.CanManageSubscriptions,
            profile.CanRecordPayments,
            profile.CanApplyDiscount,
            profile.CanGrantComplimentarySession,
            profile.CanManageSessions,
            profile.CanManageEquipment,
            profile.CanSellEquipment,
            profile.CanReturnEquipment,
            profile.CanAccessPlanset,
            profile.CanIssueEquipmentRental,
            profile.CanViewHistory);

    public static void ApplyPermissions(
        AccessProfile target,
        bool canRegisterCustomers,
        bool canViewCustomerDetails,
        bool canEditCustomerDetails,
        bool canManageSubscriptions,
        bool canRecordPayments,
        bool canApplyDiscount,
        bool canGrantComplimentarySession,
        bool canManageSessions,
        bool canManageEquipment,
        bool canSellEquipment,
        bool canReturnEquipment,
        bool canAccessPlanset,
        bool canIssueEquipmentRental,
        bool canViewHistory)
    {
        target.CanRegisterCustomers = canRegisterCustomers;
        target.CanViewCustomerDetails = canViewCustomerDetails;
        target.CanEditCustomerDetails = canEditCustomerDetails;
        target.CanManageSubscriptions = canManageSubscriptions;
        target.CanRecordPayments = canRecordPayments;
        target.CanApplyDiscount = canApplyDiscount;
        target.CanGrantComplimentarySession = canGrantComplimentarySession;
        target.CanManageSessions = canManageSessions;
        target.CanManageEquipment = canManageEquipment;
        target.CanSellEquipment = canSellEquipment;
        target.CanReturnEquipment = canReturnEquipment;
        target.CanAccessPlanset = canAccessPlanset;
        target.CanIssueEquipmentRental = canIssueEquipmentRental;
        target.CanViewHistory = canViewHistory;
    }

    public static void CopyPermissions(AccessProfile source, AccessProfile target) =>
        ApplyPermissions(
            target,
            source.CanRegisterCustomers,
            source.CanViewCustomerDetails,
            source.CanEditCustomerDetails,
            source.CanManageSubscriptions,
            source.CanRecordPayments,
            source.CanApplyDiscount,
            source.CanGrantComplimentarySession,
            source.CanManageSessions,
            source.CanManageEquipment,
            source.CanSellEquipment,
            source.CanReturnEquipment,
            source.CanAccessPlanset,
            source.CanIssueEquipmentRental,
            source.CanViewHistory);
}
