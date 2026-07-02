using EShooting.Application.Common.Models;
using EShooting.Domain.Entities;

namespace EShooting.Application.StaffMembers;

public static class ReceptionStaffSessionMapper
{
    public static ReceptionStaffSessionItem Map(StaffMember member, AccessProfile profile) => new()
    {
        Id = member.Id,
        FirstName = member.FirstName,
        LastName = member.LastName,
        FullName = $"{member.FirstName} {member.LastName}".Trim(),
        PositionName = member.StaffPosition?.Name ?? "—",
        AccessProfileName = profile.Name,
        CanRegisterCustomers = profile.CanRegisterCustomers,
        CanViewCustomerDetails = profile.CanViewCustomerDetails,
        CanEditCustomerDetails = profile.CanEditCustomerDetails,
        CanManageSubscriptions = profile.CanManageSubscriptions,
        CanRecordPayments = profile.CanRecordPayments,
        CanApplyDiscount = profile.CanApplyDiscount,
        CanGrantComplimentarySession = profile.CanGrantComplimentarySession,
        CanManageSessions = profile.CanManageSessions,
        CanManageEquipment = profile.CanManageEquipment,
        CanSellEquipment = profile.CanSellEquipment,
        CanReturnEquipment = profile.CanReturnEquipment,
        CanAccessPlanset = profile.CanAccessPlanset,
        CanIssueEquipmentRental = profile.CanIssueEquipmentRental,
        CanViewHistory = profile.CanViewHistory
    };
}
