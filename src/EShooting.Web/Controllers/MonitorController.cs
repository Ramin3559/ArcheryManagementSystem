using EShooting.Application.Sessions.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

public sealed class MonitorController(IMediator mediator) : Controller
{
    [HttpGet("/monitor/scoreboard/{laneNumber:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Scoreboard([FromRoute] int laneNumber, CancellationToken cancellationToken)
    {
        if (laneNumber is < 1 or > 11)
        {
            return NotFound();
        }

        var lanes = await mediator.Send(new GetLaneDashboardQuery(), cancellationToken);
        var lane = lanes.FirstOrDefault(x => x.LaneNumber == laneNumber);
        // Monitor scoreboard should show logo unless a lane is actively running right now.
        var hasSession = lane?.SessionId is not null && lane.Status == "Active";

        return View(new ScoreboardViewModel
        {
            LaneNumber = laneNumber,
            HasSession = hasSession,
            TotalScore = lane?.TotalScore ?? 0
        });
    }

    public sealed class ScoreboardViewModel
    {
        public required int LaneNumber { get; init; }
        public required bool HasSession { get; init; }
        public required int TotalScore { get; init; }
    }
}

