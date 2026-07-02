using System.Security.Claims;
using EShooting.Application.Common.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace EShooting.Web.Auth;

public static class PlansetStaffSignIn
{
    public static async Task SignInAsync(HttpContext httpContext, ReceptionStaffSessionItem staff)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new(ClaimTypes.Name, staff.FullName),
            new(ClaimTypes.Role, PlansetStaffClaims.Role),
            new(PlansetStaffClaims.StaffId, staff.Id.ToString()),
            new(PlansetStaffClaims.PositionName, staff.PositionName)
        };

        if (staff.CanIssueEquipmentRental)
        {
            claims.Add(new Claim(PlansetStaffClaims.CanIssueEquipmentRental, "1"));
        }

        var identity = new ClaimsIdentity(
            claims,
            PlansetAuthDefaults.Scheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(
            PlansetAuthDefaults.Scheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(12),
                AllowRefresh = true
            });
    }
}
