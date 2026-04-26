using EShooting.Application.Common.Models;
using EShooting.Application.Sessions.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

/// <summary>
/// Zolaq monitoru üçün server tərəfli partial HTML (ayrıca view-lər).
/// </summary>
[Authorize]
[Route("dashboard")]
public sealed class DashboardPartialController(IMediator mediator) : Controller
{
    [HttpGet("lanes/partial/grid")]
    public async Task<IActionResult> LaneGrid(CancellationToken cancellationToken)
    {
        var lanes = await mediator.Send(new GetLaneDashboardQuery(), cancellationToken);
        return PartialView("Lanes/_LaneGrid", lanes);
    }

    [HttpGet("lanes/partial/table")]
    public async Task<IActionResult> LaneTable(CancellationToken cancellationToken)
    {
        var lanes = await mediator.Send(new GetLaneDashboardQuery(), cancellationToken);
        return PartialView("Lanes/_LaneTableBody", lanes);
    }

    /// <summary>
    /// Tək zolaq kartı (TV / tam ekran səhifələr üçün).
    /// </summary>
    [HttpGet("lanes/partial/card/{laneNumber:int}")]
    public async Task<IActionResult> LaneCard([FromRoute] int laneNumber, CancellationToken cancellationToken)
    {
        if (laneNumber is < 1 or > 11)
        {
            return NotFound();
        }

        var lanes = await mediator.Send(new GetLaneDashboardQuery(), cancellationToken);
        var lane = lanes.FirstOrDefault(l => l.LaneNumber == laneNumber);
        if (lane is null)
        {
            return NotFound();
        }

        return PartialView("Lanes/_LaneCard", lane);
    }
}
