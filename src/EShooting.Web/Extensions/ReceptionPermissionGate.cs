using EShooting.Web.Auth;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Extensions;

public static class ReceptionPermissionGate
{
    public static IActionResult? DenyUnless(ControllerBase controller, string permission)
    {
        if (!controller.User.IsReceptionStaff())
        {
            return null;
        }

        if (controller.User.HasReceptionPermission(permission))
        {
            return null;
        }

        return controller.StatusCode(StatusCodes.Status403Forbidden, new { error = "Bu əməliyyat üçün icazəniz yoxdur." });
    }
}
