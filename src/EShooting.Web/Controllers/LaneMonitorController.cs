using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCaching;

namespace EShooting.Web.Controllers;

/// <summary>
/// Planşet üçün yalnız baxış rejimində zolaq monitoru (heç bir əməliyyat yoxdur).
/// Məs: /planset/zolaqlar
/// </summary>
[AllowAnonymous]
public sealed class LaneMonitorController : Controller
{
    [HttpGet("/planset/zolaqlar")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index() => View();
}
