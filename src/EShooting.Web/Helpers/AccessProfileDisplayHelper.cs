using EShooting.Application.Common.Models;

namespace EShooting.Web.Helpers;

public static class AccessProfileDisplayHelper
{
    public static string PermissionsShort(AccessProfileItem profile)
    {
        var parts = new List<string>();
        if (profile.CanRegisterCustomers) parts.Add("Müştəri");
        if (profile.CanManageSubscriptions) parts.Add("Paket");
        if (profile.CanManageSessions) parts.Add("Zolaq");
        if (profile.CanManageEquipment) parts.Add("Avadanlıq");
        if (profile.CanViewHistory) parts.Add("Tarixçə");
        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }
}
