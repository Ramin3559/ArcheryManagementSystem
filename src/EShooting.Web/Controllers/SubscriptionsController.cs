using EShooting.Application.Subscriptions.Commands;
using EShooting.Application.Subscriptions.Queries;
using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Web.Contracts.Subscriptions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("subscriptions")]
public sealed class SubscriptionsController(IMediator mediator, ITrainingCenterRepository repository) : ControllerBase
{
    [HttpPost("schedules/analyze")]
    public async Task<IActionResult> AnalyzeSchedule(
        [FromBody] AnalyzeSubscriptionScheduleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TimeSpan.TryParse(request.StartTimeLocal, out var startTimeLocal))
        {
            return BadRequest(new { error = "Saat formatı yanlışdır (HH:mm)." });
        }

        if (request.DayOfWeek is < 0 or > 6)
        {
            return BadRequest(new { error = "Həftə günü yanlışdır." });
        }

        if (request.IsFullPackage)
        {
            return Ok(new { occurrences = Array.Empty<object>(), conflictCount = 0 });
        }

        if (request.DurationMinutes <= 0)
        {
            return BadRequest(new { error = "Müddət müsbət olmalıdır." });
        }

        if (request.ActiveToDateLocal.Date < request.ActiveFromDateLocal.Date)
        {
            return BadRequest(new { error = "Bitmə tarixi başlanğıcdan əvvəl ola bilməz." });
        }

        if (request.LaneNumber <= 0)
        {
            return BadRequest(new { error = "Konflikt həlledicisi üçün konkret zolaq seçin." });
        }

        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var athlete = request.AthleteId is not null
            ? athletes.FirstOrDefault(x => x.Id == request.AthleteId.Value)
            : athletes.FirstOrDefault(x => string.Equals(x.FullName, request.AthleteFullName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (athlete is null)
        {
            return BadRequest(new { error = "İdmançı tapılmadı." });
        }

        var laneAllowed = athlete.Category == Domain.Enums.CustomerCategory.Amateur
            ? Enumerable.Range(1, 8).ToArray()
            : Enumerable.Range(1, 11).ToArray();

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;

        var from = request.ActiveFromDateLocal.Date;
        var to = request.ActiveToDateLocal.Date;
        var occurrences = new List<object>();
        var conflictCount = 0;

        for (var day = from; day <= to; day = day.AddDays(1))
        {
            if ((int)day.DayOfWeek != request.DayOfWeek) continue;

            var slotLocal = day.Add(startTimeLocal);
            var reqStartUtc = DateTime.SpecifyKind(slotLocal, DateTimeKind.Local).ToUniversalTime();
            var reqEndUtc = reqStartUtc.AddMinutes(request.DurationMinutes);

            bool IsLaneBusy(int laneNumber)
            {
                var lane = lanes.FirstOrDefault(l => l.Number == laneNumber);
                if (lane is null) return true;

                var busyBySession = sessions
                    .Where(s => s.LaneId == lane.Id)
                    .Any(s => LaneReservationRules.OverlapsSession(s, reqStartUtc, reqEndUtc, nowUtc));
                if (busyBySession) return true;

                var reqStartLocal = slotLocal;
                var reqEndLocal = slotLocal.AddMinutes(request.DurationMinutes);
                return schedules.Any(s =>
                {
                    if (!s.IsEnabled) return false;
                    if (day < s.ActiveFromDateLocal.Date || day > s.ActiveToDateLocal.Date) return false;
                    if (s.DayOfWeek != (int)day.DayOfWeek) return false;
                    var reservedLane = s.LastAssignedLaneNumber ?? (s.LaneNumber > 0 ? s.LaneNumber : (int?)null);
                    if (reservedLane != laneNumber) return false;
                    var subStart = day.Add(s.StartTimeLocal);
                    var subEnd = subStart.AddMinutes(s.DurationMinutes);
                    return reqStartLocal < subEnd && reqEndLocal > subStart;
                });
            }

            var baseBusy = IsLaneBusy(request.LaneNumber);

            var busyLanesSameTime = laneAllowed
                .Where(IsLaneBusy)
                .ToArray();
            var freeLanesSameTime = laneAllowed
                .Where(n => !busyLanesSameTime.Contains(n))
                .ToArray();

            // Find a few alternative times on the same lane for this date.
            var altTimes = new List<string>();
            var lane = lanes.FirstOrDefault(l => l.Number == request.LaneNumber);
            if (lane is not null)
            {
                for (var cursor = day; cursor < day.AddDays(1); cursor = cursor.AddMinutes(30))
                {
                    var altStartUtc = DateTime.SpecifyKind(cursor, DateTimeKind.Local).ToUniversalTime();
                    var altEndUtc = altStartUtc.AddMinutes(request.DurationMinutes);

                    var busy = sessions
                        .Where(s => s.LaneId == lane.Id)
                        .Any(s => LaneReservationRules.OverlapsSession(s, altStartUtc, altEndUtc, nowUtc));
                    if (!busy)
                    {
                        var reqStartLocal2 = cursor;
                        var reqEndLocal2 = cursor.AddMinutes(request.DurationMinutes);
                        busy = schedules.Any(s =>
                        {
                            if (!s.IsEnabled) return false;
                            if (day < s.ActiveFromDateLocal.Date || day > s.ActiveToDateLocal.Date) return false;
                            if (s.DayOfWeek != (int)day.DayOfWeek) return false;
                            var reservedLane = s.LastAssignedLaneNumber ?? (s.LaneNumber > 0 ? s.LaneNumber : (int?)null);
                            if (reservedLane != request.LaneNumber) return false;
                            var subStart = day.Add(s.StartTimeLocal);
                            var subEnd = subStart.AddMinutes(s.DurationMinutes);
                            return reqStartLocal2 < subEnd && reqEndLocal2 > subStart;
                        });
                    }

                    if (!busy)
                    {
                        altTimes.Add(cursor.ToString("HH:mm"));
                    }
                }
            }

            if (baseBusy) conflictCount++;
            occurrences.Add(new
            {
                dateLocal = day.ToString("yyyy-MM-dd"),
                isBusy = baseBusy,
                allowedLaneNumbers = laneAllowed,
                busyLaneNumbersSameTime = busyLanesSameTime,
                freeLaneNumbersSameTime = freeLanesSameTime,
                freeStartTimesSameLane = altTimes
            });
        }

        return Ok(new { occurrences, conflictCount });
    }

    [HttpPost("schedules/with-exceptions")]
    public async Task<IActionResult> CreateScheduleWithExceptions(
        [FromBody] CreateSubscriptionScheduleWithExceptionsRequest request,
        CancellationToken cancellationToken)
    {
        if (!TimeSpan.TryParse(request.StartTimeLocal, out var startTimeLocal))
        {
            return BadRequest(new { error = "Saat formatı yanlışdır (HH:mm)." });
        }

        var overrides = new List<EShooting.Application.Subscriptions.Commands.ScheduleExceptionOverride>();
        foreach (var ov in request.Overrides ?? new List<EShooting.Web.Contracts.Subscriptions.ScheduleExceptionOverride>())
        {
            if (!TimeSpan.TryParse(ov.StartTimeLocal, out var ovTime))
            {
                return BadRequest(new { error = "İstisna saat formatı yanlışdır (HH:mm)." });
            }
            overrides.Add(new EShooting.Application.Subscriptions.Commands.ScheduleExceptionOverride(ov.DateLocal.Date, ov.LaneNumber, ovTime));
        }

        try
        {
            var id = await mediator.Send(
                new CreateSubscriptionScheduleWithExceptionsCommand(
                    request.AthleteId,
                    request.AthleteFullName,
                    request.DayOfWeek,
                    startTimeLocal,
                    request.DurationMinutes,
                    request.ActiveFromDateLocal,
                    request.ActiveToDateLocal,
                    request.PreferredLaneType,
                    request.LaneNumber,
                    request.IsFullPackage,
                    overrides),
                cancellationToken);

            return Ok(new { id });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.StartsWith("DUPLICATE_SUBSCRIPTION_SCHEDULE:", StringComparison.OrdinalIgnoreCase))
            {
                var existingId = ex.Message.Split(':').LastOrDefault();
                return Conflict(new
                {
                    error = "Bu şəxs üçün bu gün/saat/zolaq üzrə abunə artıq mövcuddur. Yeniləmək istəyirsinizmi?",
                    existingScheduleId = existingId,
                    kind = "duplicate"
                });
            }

            if (ex.Message.StartsWith("KONFLIKT_HƏLL_OLUNMAYIB:", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = "Bəzi həftələr doludur. Zəhmət olmasa konfliktli həftələr üçün alternativ seçin.", kind = "busy" });
            }

            if (ex.Message.Contains("doludur", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("bütün", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = ex.Message, kind = "busy" });
            }

            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "Bu şəxs üçün bu gün/saat/zolaq üzrə abunə artıq mövcuddur. Yeniləmək istəyirsinizmi?", kind = "duplicate" });
        }
    }

    [HttpPost("schedules")]
    public async Task<IActionResult> CreateSchedule(
        [FromBody] CreateSubscriptionScheduleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TimeSpan.TryParse(request.StartTimeLocal, out var startTimeLocal))
        {
            return BadRequest(new { error = "Saat formatı yanlışdır (HH:mm)." });
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
            if (ex.Message.StartsWith("DUPLICATE_SUBSCRIPTION_SCHEDULE:", StringComparison.OrdinalIgnoreCase))
            {
                var existingId = ex.Message.Split(':').LastOrDefault();
                return Conflict(new
                {
                    error = "Bu şəxs üçün bu gün/saat/zolaq üzrə abunə artıq mövcuddur. Yeniləmək istəyirsinizmi?",
                    existingScheduleId = existingId,
                    kind = "duplicate"
                });
            }

            // For subscription schedules, never silently reassign lane/time.
            // If the chosen lane/time is busy, return 409 so the operator can pick a new lane/time.
            if (ex.Message.Contains("doludur", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("bütün", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = ex.Message, kind = "busy" });
            }

            return BadRequest(new { error = ex.Message });
        }
        catch (DbUpdateException)
        {
            return Conflict(new { error = "Bu şəxs üçün bu gün/saat/zolaq üzrə abunə artıq mövcuddur. Yeniləmək istəyirsinizmi?" });
        }
    }

    [HttpPut("schedules/{id:guid}")]
    public async Task<IActionResult> UpdateSchedule(
        [FromRoute] Guid id,
        [FromBody] CreateSubscriptionScheduleRequest request,
        CancellationToken cancellationToken)
    {
        if (!TimeSpan.TryParse(request.StartTimeLocal, out var startTimeLocal))
        {
            return BadRequest(new { error = "Saat formatı yanlışdır (HH:mm)." });
        }

        try
        {
            var updatedId = await mediator.Send(
                new UpdateSubscriptionScheduleCommand(
                    id,
                    request.DayOfWeek,
                    startTimeLocal,
                    request.DurationMinutes,
                    request.ActiveFromDateLocal,
                    request.ActiveToDateLocal,
                    request.PreferredLaneType,
                    request.LaneNumber,
                    request.IsFullPackage),
                cancellationToken);

            return Ok(new { id = updatedId, message = "Abunə məlumatları yeniləndi." });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.StartsWith("DUPLICATE_SUBSCRIPTION_SCHEDULE:", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = "Bu şəxs üçün bu gün/saat/zolaq üzrə abunə artıq mövcuddur." });
            }
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
            var result = await mediator.Send(
                new CreateSubscriptionPackageCommand(
                    request.AthleteFullName,
                    request.DayPattern,
                    request.VisitsCount,
                    startTimeLocal,
                    request.DurationMinutes,
                    request.StartDateLocal,
                    request.PreferredLaneTypesByDayOfWeek,
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

    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelSubscriptionRequest request, CancellationToken cancellationToken)
    {
        if (request.AthleteId == Guid.Empty)
        {
            return BadRequest(new { error = "AthleteId is required." });
        }

        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var athlete = athletes.FirstOrDefault(x => x.Id == request.AthleteId);
        if (athlete is null)
        {
            return NotFound(new { error = "Athlete not found." });
        }

        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var updated = 0;
        foreach (var s in schedules.Where(x => x.AthleteId == request.AthleteId && x.IsEnabled))
        {
            s.IsEnabled = false;
            await repository.UpdateSubscriptionScheduleAsync(s, cancellationToken);
            updated++;
        }

        athlete.IsSubscriber = false;
        athlete.IsFullPackage = false;
        await repository.UpdateAthleteAsync(athlete, cancellationToken);

        return Ok(new { cancelledSchedules = updated });
    }
}
