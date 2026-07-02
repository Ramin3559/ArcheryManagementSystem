using EShooting.Web.Auth;
using EShooting.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCaching;

namespace EShooting.Web.Controllers;

/// <summary>
/// Nəzarətçi planşeti — zolaq monitoru + icarə avadanlığı vermə / təhvil alma.
/// Məs: /planset/zolaqlar
/// </summary>
[Authorize(Policy = PlansetAuthDefaults.Policy)]
public sealed class LaneMonitorController : Controller
{
    [HttpGet("/planset/zolaqlar")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index() => View();
}
