using EShooting.Web.Auth;
using EShooting.Web.Extensions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace EShooting.Web.Auth;

/// <summary>
/// Köhnə (ümumi) cookie-də Admin rolü qalıbsa, admin panel sorğuları üçün ayrıca admin cookie-yə köçürür.
/// </summary>
public sealed class AdminLegacyAuthMigrationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (IsAdminPanelPath(context.Request.Path))
        {
            var adminAuth = await context.AuthenticateAsync(AdminAuthDefaults.Scheme);
            if (!HasAdminRole(adminAuth.Principal))
            {
                var legacyAuth = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                if (HasAdminRole(legacyAuth.Principal))
                {
                    await context.MigrateLegacyAdminAsync(legacyAuth.Principal!);
                }
            }
        }

        await next(context);
    }

    private static bool IsAdminPanelPath(PathString path) =>
        path.StartsWithSegments("/admin", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/admin/login", StringComparison.OrdinalIgnoreCase)
        && !path.StartsWithSegments("/admin/logout", StringComparison.OrdinalIgnoreCase);

    private static bool HasAdminRole(System.Security.Claims.ClaimsPrincipal? principal) =>
        principal?.Identity?.IsAuthenticated == true && principal.IsInRole("Admin");
}
