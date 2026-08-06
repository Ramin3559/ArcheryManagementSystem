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

        bool canChangeLane,

        bool canViewHistory,

        bool canDeleteRestoreCustomers,

        bool canChangeCustomerPackage) =>

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

        || canChangeLane

        || canViewHistory

        || canDeleteRestoreCustomers

        || canChangeCustomerPackage;



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

            profile.CanChangeLane,

            profile.CanViewHistory,

            profile.CanDeleteRestoreCustomers,

            profile.CanChangeCustomerPackage);



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

        bool canChangeLane,

        bool canViewHistory,

        bool canDeleteRestoreCustomers,

        bool canChangeCustomerPackage)

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

        target.CanChangeLane = canChangeLane;

        target.CanViewHistory = canViewHistory;

        target.CanDeleteRestoreCustomers = canDeleteRestoreCustomers;

        target.CanChangeCustomerPackage = canChangeCustomerPackage;

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

            source.CanChangeLane,

            source.CanViewHistory,

            source.CanDeleteRestoreCustomers,

            source.CanChangeCustomerPackage);

}


