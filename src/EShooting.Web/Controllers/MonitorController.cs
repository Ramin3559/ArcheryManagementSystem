using EShooting.Application.Sessions.Queries;
using EShooting.Web.Hubs;
using EShooting.Web.Services;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace EShooting.Web.Controllers;

public sealed class MonitorController(
    IMediator mediator,
    ScoreDisplayState scoreDisplayState,
    IHubContext<LaneHub> hubContext) : Controller
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
        var hasSession = scoreDisplayState.IsEnabled
            && lane?.SessionId is not null
            && lane.Status == "Active";

        return View(new ScoreboardViewModel
        {
            LaneNumber = laneNumber,
            HasSession = hasSession,
            TotalScore = lane?.TotalScore ?? 0,
            ScoreDisplayEnabled = scoreDisplayState.IsEnabled
        });
    }

    [HttpGet("/monitor/score-display")]
    [AllowAnonymous]
    public IActionResult GetScoreDisplay() =>
        Ok(new { enabled = scoreDisplayState.IsEnabled });

    [HttpPost("/monitor/score-display")]
    [AllowAnonymous]
    public async Task<IActionResult> SetScoreDisplay([FromBody] ScoreDisplayRequest request, CancellationToken cancellationToken)
    {
        scoreDisplayState.IsEnabled = request.Enabled;
        await hubContext.Clients.All.SendAsync("score-display-changed", request.Enabled, cancellationToken);
        return Ok(new { enabled = scoreDisplayState.IsEnabled });
    }

    public sealed record ScoreDisplayRequest(bool Enabled);

    public sealed class ScoreboardViewModel
    {
        public required int LaneNumber { get; init; }
        public required bool HasSession { get; init; }
        public required int TotalScore { get; init; }
        public required bool ScoreDisplayEnabled { get; init; }
    }
}
