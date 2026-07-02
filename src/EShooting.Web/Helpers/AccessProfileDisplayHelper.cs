using EShooting.Application.Common.Models;

namespace EShooting.Web.Helpers;

public static class AccessProfileDisplayHelper
{
    public static string PermissionsShort(AccessProfileItem profile)
    {
        var parts = new List<string>();
        if (profile.CanRegisterCustomers) parts.Add("Qeydiyyat");
        if (profile.CanViewCustomerDetails) parts.Add("Müştəri baxış");
        if (profile.CanEditCustomerDetails) parts.Add("Müştəri dəyişiklik");
        if (profile.CanManageSubscriptions) parts.Add("Paket");
        if (profile.CanApplyDiscount) parts.Add("Endirim");
        if (profile.CanGrantComplimentarySession) parts.Add("Ödənişsiz");
        if (profile.CanManageSessions) parts.Add("Zolaq");
        if (profile.CanManageEquipment) parts.Add("Avadanlıq vermə");
        if (profile.CanSellEquipment) parts.Add("Satış");
        if (profile.CanReturnEquipment) parts.Add("Geri qaytarma");
        if (profile.CanAccessPlanset) parts.Add("Planşet");
        if (profile.CanIssueEquipmentRental) parts.Add("İcarə");
        if (profile.CanViewHistory) parts.Add("Tarixçə");
        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }
}
