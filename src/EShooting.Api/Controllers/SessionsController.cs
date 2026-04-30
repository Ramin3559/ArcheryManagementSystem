using EShooting.Web.Contracts.Sessions;
using EShooting.Application.Sessions.Commands;
using EShooting.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("sessions")]
public sealed class SessionsController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Secilmis lane uzre idmanci ucun meshq sessiyasi yaradir.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Schedule([FromBody] ScheduleSessionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var sessionId = await mediator.Send(new ScheduleSessionCommand(
                request.AthleteId,
                request.LaneNumber,
                request.StartTimeUtc,
                request.DurationMinutes,
                request.IsEquipmentIssued,
                request.PreferredLaneType), cancellationToken);

            return Ok(new { sessionId });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("batch-lane")]
    public async Task<IActionResult> RegisterGroupOnLane(
        [FromBody] RegisterGroupOnLaneRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await mediator.Send(
                new RegisterGroupOnLaneCommand(
                    request.AthleteNames,
                    request.LaneNumber,
                    request.StartTimeUtc,
                    request.DurationMinutes,
                    request.IsEquipmentIssued),
                cancellationToken);

            return Ok(new
            {
                createdCount = result.Sessions.Count,
                sessions = result.Sessions
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Secilmis sessiyaya xal deyerini gonderir.
    /// </summary>
    [HttpPost("{sessionId:guid}/scores")]
    public async Task<IActionResult> SubmitScore(
        Guid sessionId,
        [FromBody] SubmitScoreRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var totalScore = await mediator.Send(
                new SubmitScoreCommand(sessionId, request.RoundNumber, request.Value),
                cancellationToken);

            return Ok(new { totalScore });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Sessiyani tamamlanmis kimi qeyd edir.
    /// </summary>
    [HttpPost("{sessionId:guid}/complete")]
    public async Task<IActionResult> Complete(Guid sessionId, CancellationToken cancellationToken)
    {
        await mediator.Send(new CompleteSessionCommand(sessionId), cancellationToken);
        return NoContent();
    }
}
