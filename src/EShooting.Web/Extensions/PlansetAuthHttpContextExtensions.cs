using EShooting.Web.Auth;
using Microsoft.AspNetCore.Authentication;

namespace EShooting.Web.Extensions;

public static class PlansetAuthHttpContextExtensions
{
    public static async Task<bool> IsPlansetAuthenticatedAsync(this HttpContext httpContext)
    {
        var result = await httpContext.AuthenticateAsync(PlansetAuthDefaults.Scheme);
        return result.Succeeded && result.Principal?.IsInRole(PlansetStaffClaims.Role) == true;
    }

    public static Task SignOutPlansetAsync(this HttpContext httpContext) =>
        httpContext.SignOutAsync(PlansetAuthDefaults.Scheme);
}
