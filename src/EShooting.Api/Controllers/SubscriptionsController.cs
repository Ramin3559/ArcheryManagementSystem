using EShooting.Application.Subscriptions.Commands;
using EShooting.Application.Subscriptions.Queries;
using EShooting.Web.Contracts.Subscriptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("subscriptions")]
public sealed class SubscriptionsController(IMediator mediator) : ControllerBase
{
    [HttpPost("schedules")]
    public async Task<IActionResult> CreateSchedule(
        [FromBody] CreateSubscriptionScheduleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TimeSpan.TryParse(request.StartTimeLocal, out var startTimeLocal))
        {
            return BadRequest(new { error = "StartTimeLocal must be a valid time value (HH:mm)." });
        }

        try
        {
            var id = await mediator.Send(
                new CreateSubscriptionScheduleCommand(
                    request.AthleteId,
                    request.AthleteFullName,
                    request.DayOfWeek,
                    startTimeLocal,
                    request.DurationMinutes,
                    request.ActiveFromDateLocal,
                    request.ActiveToDateLocal,
                    request.PreferredLaneType,
                    request.LaneNumber,
                    request.IsFullPackage),
                cancellationToken);

            return Ok(new { id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("schedules")]
    public async Task<IActionResult> GetSchedules([FromQuery] Guid? athleteId, CancellationToken cancellationToken)
    {
        var schedules = await mediator.Send(new GetSubscriptionSchedulesQuery(athleteId), cancellationToken);
        return Ok(schedules);
    }

    [HttpPost("packages")]
    public async Task<IActionResult> CreatePackage(
        [FromBody] CreateSubscriptionPackageRequest request,
        CancellationToken cancellationToken)
    {
        if (!TimeSpan.TryParse(request.StartTimeLocal, out var startTimeLocal))
        {
            return BadRequest(new { error = "StartTimeLocal must be a valid time value (HH:mm)." });
        }

        try
        {
            var preferredLaneTypesByDay = request.PreferredLaneTypesByDayOfWeek
                ?? new Dictionary<int, EShooting.Domain.Enums.PreferredLaneType>();
            var result = await mediator.Send(
                new CreateSubscriptionPackageCommand(
                    request.AthleteFullName,
                    request.DayPattern,
                    request.VisitsCount,
                    startTimeLocal,
                    request.DurationMinutes,
                    request.StartDateLocal,
                    request.EndDateLocal,
                    preferredLaneTypesByDay,
                    request.IsFullPackage),
                cancellationToken);

            return Ok(new
            {
                createdCount = result.CreatedCount,
                firstSessionDateLocal = result.FirstSessionDateLocal,
                lastSessionDateLocal = result.LastSessionDateLocal
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
