using EShooting.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("dashboard")]
public sealed class DashboardController(CachedLaneDashboardService laneDashboard) : ControllerBase
{
    /// <summary>
    /// Monitor ve admin paneli ucun lane veziyyetlerini cemlenmis sekilde qaytarir.
    /// </summary>
    [HttpGet("lanes")]
    public async Task<IActionResult> GetLanes(CancellationToken cancellationToken)
    {
        var lanes = await laneDashboard.GetLanesAsync(cancellationToken);
        return Ok(lanes);
    }
}
