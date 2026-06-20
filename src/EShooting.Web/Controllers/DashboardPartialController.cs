using EShooting.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.ResponseCaching;

namespace EShooting.Web.Controllers;

/// <summary>
/// Zolaq monitoru üçün server tərəfli partial HTML (ayrıca view-lər).
/// </summary>
[AllowAnonymous]
[Route("dashboard")]
public sealed class DashboardPartialController(CachedLaneDashboardService laneDashboard) : Controller
{
    [HttpGet("lanes/partial/grid")]
    public async Task<IActionResult> LaneGrid(CancellationToken cancellationToken)
    {
        var lanes = await laneDashboard.GetLanesAsync(cancellationToken);
        return PartialView("Lanes/_LaneGrid", lanes);
    }

    /// <summary>Planşet monitoru — Stop düyməsi və digər əməliyyatlar olmadan.</summary>
    [HttpGet("lanes/partial/grid-readonly")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> LaneGridReadOnly(CancellationToken cancellationToken)
    {
        var lanes = await laneDashboard.GetLanesAsync(cancellationToken);
        return PartialView("Lanes/_LaneGridReadOnly", lanes);
    }

    [HttpGet("lanes/partial/table")]
    public async Task<IActionResult> LaneTable(CancellationToken cancellationToken)
    {
        var lanes = await laneDashboard.GetLanesAsync(cancellationToken);
        return PartialView("Lanes/_LaneTableBody", lanes);
    }

    /// <summary>
    /// Tək zolaq kartı (TV / tam ekran səhifələr üçün).
    /// </summary>
    [HttpGet("lanes/partial/card/{laneNumber:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> LaneCard([FromRoute] int laneNumber, CancellationToken cancellationToken)
    {
        if (laneNumber is < 1 or > 11)
        {
            return NotFound();
        }

        var lanes = await laneDashboard.GetLanesAsync(cancellationToken);
        var lane = lanes.FirstOrDefault(l => l.LaneNumber == laneNumber);
        if (lane is null)
        {
            return NotFound();
        }

        return PartialView("Lanes/_LaneCardTv", lane);
    }
}
