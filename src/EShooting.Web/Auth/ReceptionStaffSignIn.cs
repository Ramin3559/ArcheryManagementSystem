using System.Security.Claims;
using EShooting.Application.Common.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace EShooting.Web.Auth;

public static class ReceptionStaffSignIn
{
    public static async Task SignInAsync(HttpContext httpContext, ReceptionStaffSessionItem staff)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, staff.Id.ToString()),
            new(ClaimTypes.Name, staff.FullName),
            new(ClaimTypes.Role, ReceptionStaffClaims.Role),
            new(ReceptionStaffClaims.StaffId, staff.Id.ToString()),
            new(ReceptionStaffClaims.PositionName, staff.PositionName),
            new(ReceptionStaffClaims.AccessProfileName, staff.AccessProfileName)
        };

        AddPermissionClaim(claims, ReceptionStaffClaims.CanRegisterCustomers, staff.CanRegisterCustomers);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanViewCustomerDetails, staff.CanViewCustomerDetails);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanEditCustomerDetails, staff.CanEditCustomerDetails);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanManageSubscriptions, staff.CanManageSubscriptions);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanRecordPayments, staff.CanRecordPayments);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanApplyDiscount, staff.CanApplyDiscount);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanGrantComplimentarySession, staff.CanGrantComplimentarySession);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanManageSessions, staff.CanManageSessions);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanManageEquipment, staff.CanManageEquipment);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanSellEquipment, staff.CanSellEquipment);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanReturnEquipment, staff.CanReturnEquipment);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanAccessPlanset, staff.CanAccessPlanset);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanIssueEquipmentRental, staff.CanIssueEquipmentRental);
        AddPermissionClaim(claims, ReceptionStaffClaims.CanViewHistory, staff.CanViewHistory);

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme,
            ClaimTypes.Name,
            ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);

        httpContext.User = principal;

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = false
            });
    }

    private static void AddPermissionClaim(List<Claim> claims, string type, bool enabled)
    {
        if (enabled)
        {
            claims.Add(new Claim(type, "1"));
        }
    }
}
