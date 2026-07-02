using System.Security.Claims;
using EShooting.Web.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Extensions;

public static class ReceptionStaffPrincipalExtensions
{
    public static bool IsReceptionStaff(this ClaimsPrincipal user) =>
        user.IsInRole(ReceptionStaffClaims.Role);

    public static bool HasReceptionPermission(this ClaimsPrincipal user, string claimType) =>
        user.HasClaim(claimType, "1");

    public static bool HasAnyReceptionPermission(this ClaimsPrincipal user, params string[] claimTypes) =>
        claimTypes.Any(user.HasReceptionPermission);

    public static Guid? GetStaffMemberId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(ReceptionStaffClaims.StaffId)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string GetStaffDisplayName(this ClaimsPrincipal user) =>
        user.Identity?.Name ?? "İşçi";

    public static string GetStaffPositionName(this ClaimsPrincipal user) =>
        user.FindFirstValue(ReceptionStaffClaims.PositionName) ?? "—";
}
