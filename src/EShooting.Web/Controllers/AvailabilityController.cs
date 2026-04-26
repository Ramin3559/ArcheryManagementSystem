using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("availability")]
public sealed class AvailabilityController(ITrainingCenterRepository repository) : ControllerBase
{
    [HttpGet("lane")]
    public async Task<IActionResult> GetLaneAvailability(
        [FromQuery] int laneNumber,
        [FromQuery] DateTime dateLocal,
        [FromQuery] int durationMinutes,
        CancellationToken cancellationToken)
    {
        if (laneNumber is < 1 or > 11)
        {
            return BadRequest(new { error = "Zolaq nömrəsi yanlışdır." });
        }

        if (durationMinutes is <= 0 or > 240)
        {
            return BadRequest(new { error = "Müddət 1–240 dəqiqə aralığında olmalıdır." });
        }

        var lane = await repository.GetLaneByNumberAsync(laneNumber, cancellationToken);
        if (lane is null)
        {
            return NotFound(new { error = "Zolaq tapılmadı." });
        }

        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;

        // 00:00 - 24:00 local, 30-min steps
        var day = dateLocal.Date;
        var startLocal = day;
        var endLocal = day.AddDays(1);

        var slots = new List<object>();
        for (var cursor = startLocal; cursor < endLocal; cursor = cursor.AddMinutes(30))
        {
            var slotStartUtc = DateTime.SpecifyKind(cursor, DateTimeKind.Local).ToUniversalTime();
            var slotEndUtc = slotStartUtc
                .AddMinutes(durationMinutes);

            var busy = sessions
                .Where(s => s.LaneId == lane.Id)
                .Any(s => LaneReservationRules.OverlapsSession(s, slotStartUtc, slotEndUtc, nowUtc));

            if (!busy)
            {
                // Also consider enabled subscription schedules as "reserved" (even if sessions are not materialized).
                var reqStartLocal = cursor;
                var reqEndLocal = cursor.AddMinutes(durationMinutes);
                var requestedDateLocal = reqStartLocal.Date;
                busy = schedules.Any(s =>
                {
                    if (!s.IsEnabled) return false;
                    if (requestedDateLocal < s.ActiveFromDateLocal.Date || requestedDateLocal > s.ActiveToDateLocal.Date) return false;
                    if (s.DayOfWeek != (int)requestedDateLocal.DayOfWeek) return false;

                    var reservedLane = s.LastAssignedLaneNumber ?? (s.LaneNumber > 0 ? s.LaneNumber : (int?)null);
                    if (reservedLane != laneNumber) return false;

                    var subStart = requestedDateLocal.Add(s.StartTimeLocal);
                    var subEnd = subStart.AddMinutes(s.DurationMinutes);
                    return reqStartLocal < subEnd && reqEndLocal > subStart;
                });
            }

            slots.Add(new
            {
                startTimeLocal = cursor.ToString("HH:mm"),
                isBusy = busy
            });
        }

        return Ok(new { laneNumber, dateLocal = day.ToString("yyyy-MM-dd"), slots });
    }
}

