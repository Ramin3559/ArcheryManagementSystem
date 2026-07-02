using System.Security.Claims;
using EShooting.Web.Auth;

namespace EShooting.Web.Extensions;

public static class PlansetStaffPrincipalExtensions
{
    public static bool IsPlansetSupervisor(this ClaimsPrincipal user) =>
        user.IsInRole(PlansetStaffClaims.Role);

    public static bool CanIssueEquipmentRental(this ClaimsPrincipal user) =>
        user.HasClaim(PlansetStaffClaims.CanIssueEquipmentRental, "1");

    public static Guid? GetPlansetStaffMemberId(this ClaimsPrincipal user)
    {
        if (!user.IsPlansetSupervisor())
        {
            return null;
        }

        var raw = user.FindFirstValue(PlansetStaffClaims.StaffId)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string GetPlansetStaffDisplayName(this ClaimsPrincipal user) =>
        user.IsPlansetSupervisor() ? user.Identity?.Name ?? "Nəzarətçi" : "—";
}
