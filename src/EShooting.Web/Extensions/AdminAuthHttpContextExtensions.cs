using System.Security.Claims;
using EShooting.Web.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace EShooting.Web.Extensions;

public static class AdminAuthHttpContextExtensions
{
    public static async Task<bool> IsAdminAuthenticatedAsync(this HttpContext httpContext)
    {
        foreach (var scheme in AdminAuthSchemes)
        {
            var result = await httpContext.AuthenticateAsync(scheme);
            if (result.Succeeded && result.Principal?.IsInRole("Admin") == true)
            {
                return true;
            }
        }

        return false;
    }

    public static async Task<ClaimsPrincipal?> GetAdminPrincipalAsync(this HttpContext httpContext)
    {
        foreach (var scheme in AdminAuthSchemes)
        {
            var result = await httpContext.AuthenticateAsync(scheme);
            if (result.Succeeded && result.Principal?.IsInRole("Admin") == true)
            {
                return result.Principal;
            }
        }

        return null;
    }

    public static Task SignInAdminAsync(
        this HttpContext httpContext,
        ClaimsPrincipal principal,
        bool clearLegacyAdminCookie = true) =>
        SignInAdminCoreAsync(httpContext, principal, clearLegacyAdminCookie);

    public static Task MigrateLegacyAdminAsync(
        this HttpContext httpContext,
        ClaimsPrincipal principal) =>
        SignInAdminCoreAsync(httpContext, principal, clearLegacyAdminCookie: false);

    private static async Task SignInAdminCoreAsync(
        HttpContext httpContext,
        ClaimsPrincipal principal,
        bool clearLegacyAdminCookie)
    {
        await httpContext.SignInAsync(
            AdminAuthDefaults.Scheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30),
                AllowRefresh = true
            });

        if (clearLegacyAdminCookie)
        {
            await httpContext.RemoveLegacyAdminCookieAsync();
        }
    }

    public static async Task RemoveLegacyAdminCookieAsync(this HttpContext httpContext)
    {
        var legacyAuth = await httpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (legacyAuth.Succeeded
            && legacyAuth.Principal?.IsInRole("Admin") == true
            && !legacyAuth.Principal.IsInRole(ReceptionStaffClaims.Role))
        {
            await httpContext.SignOutReceptionAsync();
        }
    }

    public static Task SignOutAdminAsync(this HttpContext httpContext) =>
        httpContext.SignOutAsync(AdminAuthDefaults.Scheme);

    public static Task SignOutReceptionAsync(this HttpContext httpContext) =>
        httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

    private static readonly string[] AdminAuthSchemes =
    [
        AdminAuthDefaults.Scheme,
        CookieAuthenticationDefaults.AuthenticationScheme
    ];
}
