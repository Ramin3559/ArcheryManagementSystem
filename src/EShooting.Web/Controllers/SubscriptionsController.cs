using System.Globalization;
using EShooting.Application.Subscriptions.Commands;
using EShooting.Application.Subscriptions.Queries;
using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using EShooting.Web.Contracts.Subscriptions;
using EShooting.Application.Customers;
using EShooting.Web.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static EShooting.Application.Common.DateTimeAssumedUtc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("subscriptions")]
public sealed class SubscriptionsController(
    IMediator mediator,
    ITrainingCenterRepository repository,
    IRealtimeNotifier notifier) : ControllerBase
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
            return BadRequest(new { error = "Müştəri tapılmadı." });
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

    [HttpPost("schedules/{id:guid}/disable")]
    public async Task<IActionResult> DisableSchedule([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var existing = schedules.FirstOrDefault(x => x.Id == id);
        if (existing is null)
        {
            return NotFound(new { error = "Abunə qrafiki tapılmadı." });
        }

        if (!existing.IsEnabled)
        {
            return Ok(new { message = "Bu abunə qrafiki artıq dayandırılıb." });
        }

        existing.IsEnabled = false;
        await repository.UpdateSubscriptionScheduleAsync(existing, cancellationToken);

        // Bu şəxsin başqa aktiv abunə qrafiki yoxdursa, müştəri statusunu yenilə.
        var stillSubscribed = schedules.Any(s => s.AthleteId == existing.AthleteId && s.IsEnabled && s.Id != existing.Id);
        if (!stillSubscribed)
        {
            var athletes = await repository.GetAthletesAsync(cancellationToken);
            var athlete = athletes.FirstOrDefault(a => a.Id == existing.AthleteId);
            if (athlete is not null)
            {
                athlete.IsSubscriber = false;
                athlete.IsFullPackage = false;
                await repository.UpdateAthleteAsync(athlete, cancellationToken);
            }
        }

        return Ok(new { message = "Abunə qrafiki dayandırıldı." });
    }

    [HttpPost("schedules/{id:guid}/exclude-occurrence")]
    public async Task<IActionResult> ExcludeOccurrence(
        [FromRoute] Guid id,
        [FromBody] ExcludeSubscriptionOccurrenceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DateLocal))
        {
            return BadRequest(new { error = "Tarix daxil edin (yyyy-MM-dd)." });
        }

        if (!DateTime.TryParseExact(request.DateLocal.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return BadRequest(new { error = "Tarix formatı yanlışdır (yyyy-MM-dd)." });
        }

        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var schedule = schedules.FirstOrDefault(x => x.Id == id);
        if (schedule is null)
        {
            return NotFound(new { error = "Abunə qrafiki tapılmadı." });
        }

        if (!schedule.IsEnabled)
        {
            return BadRequest(new { error = "Bu abunə qrafiki aktiv deyil." });
        }

        if (schedule.IsFullPackage)
        {
            return BadRequest(new { error = "Tam paket üçün tək seans ləğvi tətbiq olunmur." });
        }

        var err = ValidateOccurrenceDate(schedule, date);
        if (err is not null)
        {
            return BadRequest(new { error = err });
        }

        var excluded = OccurrenceJson.DeserializeExcluded(schedule.ExcludedOccurrenceDatesJson);
        var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        excluded.Add(key);
        schedule.ExcludedOccurrenceDatesJson = OccurrenceJson.SerializeExcluded(excluded);

        var overrides = OccurrenceJson.DeserializeOverrides(schedule.OccurrenceOverridesJson);
        overrides.RemoveAll(o => string.Equals(o.DateLocal?.Trim(), key, StringComparison.Ordinal));
        schedule.OccurrenceOverridesJson = overrides.Count > 0 ? OccurrenceJson.SerializeOverrides(overrides) : null;

        await repository.UpdateSubscriptionScheduleAsync(schedule, cancellationToken);
        return Ok(new { message = "Bu tarix üçün seans ləğv edildi." });
    }

    [HttpPut("schedules/{id:guid}/occurrence-override")]
    public async Task<IActionResult> UpsertOccurrenceOverride(
        [FromRoute] Guid id,
        [FromBody] UpsertSubscriptionOccurrenceOverrideRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DateLocal))
        {
            return BadRequest(new { error = "Tarix daxil edin (yyyy-MM-dd)." });
        }

        if (!DateTime.TryParseExact(request.DateLocal.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return BadRequest(new { error = "Tarix formatı yanlışdır (yyyy-MM-dd)." });
        }

        if (!TimeSpan.TryParse(request.StartTimeLocal?.Trim(), out var startTimeLocal))
        {
            return BadRequest(new { error = "Saat formatı yanlışdır (HH:mm)." });
        }

        if (request.LaneNumber is < 0 or > 11)
        {
            return BadRequest(new { error = "Zolaq nömrəsi 0–11 aralığında olmalıdır." });
        }

        var schedules = (await repository.GetSubscriptionSchedulesAsync(cancellationToken)).ToList();
        var schedule = schedules.FirstOrDefault(x => x.Id == id);
        if (schedule is null)
        {
            return NotFound(new { error = "Abunə qrafiki tapılmadı." });
        }

        if (!schedule.IsEnabled)
        {
            return BadRequest(new { error = "Bu abunə qrafiki aktiv deyil." });
        }

        if (schedule.IsFullPackage)
        {
            return BadRequest(new { error = "Tam paket üçün tək seans dəyişikliyi tətbiq olunmur." });
        }

        var err = ValidateOccurrenceDate(schedule, date);
        if (err is not null)
        {
            return BadRequest(new { error = err });
        }

        var durationMinutes = request.DurationMinutes is > 0 ? request.DurationMinutes.Value : schedule.DurationMinutes;
        if (durationMinutes <= 0)
        {
            return BadRequest(new { error = "Müddət müsbət olmalıdır." });
        }

        var athletes = (await repository.GetAthletesAsync(cancellationToken)).ToList();
        var athlete = athletes.FirstOrDefault(a => a.Id == schedule.AthleteId);
        if (athlete is null)
        {
            return BadRequest(new { error = "Müştəri tapılmadı." });
        }

        if (athlete.Category == CustomerCategory.Amateur)
        {
            if (request.LaneNumber >= 9)
            {
                return BadRequest(new { error = "Həvəskar yalnız 1-8 zolaqlarda ola bilər." });
            }
        }

        try
        {
            if (request.LaneNumber > 0)
            {
                await ValidateSessionOverlapForOccurrenceAsync(
                    repository,
                    request.LaneNumber,
                    date,
                    startTimeLocal,
                    durationMinutes,
                    athletes,
                    schedule.AthleteId,
                    cancellationToken);

                AssertNoSubscriptionLaneConflictOnDate(
                    schedules,
                    schedule,
                    athletes,
                    date,
                    startTimeLocal,
                    durationMinutes,
                    request.LaneNumber);
            }
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var excluded = OccurrenceJson.DeserializeExcluded(schedule.ExcludedOccurrenceDatesJson);
        excluded.Remove(key);
        schedule.ExcludedOccurrenceDatesJson = excluded.Count > 0 ? OccurrenceJson.SerializeExcluded(excluded) : null;

        var list = OccurrenceJson.DeserializeOverrides(schedule.OccurrenceOverridesJson);
        var existing = list.FirstOrDefault(o => string.Equals(o.DateLocal?.Trim(), key, StringComparison.Ordinal));
        if (existing is null)
        {
            list.Add(new OccurrenceJson.OverrideRow
            {
                DateLocal = key,
                StartTimeLocal = startTimeLocal.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                LaneNumber = request.LaneNumber,
                DurationMinutes = durationMinutes
            });
        }
        else
        {
            existing.StartTimeLocal = startTimeLocal.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            existing.LaneNumber = request.LaneNumber;
            existing.DurationMinutes = durationMinutes;
        }

        schedule.OccurrenceOverridesJson = OccurrenceJson.SerializeOverrides(list);
        await repository.UpdateSubscriptionScheduleAsync(schedule, cancellationToken);
        return Ok(new { message = "Bu tarix üçün seans yeniləndi." });
    }

    [HttpPut("schedules/{id:guid}/reschedule-occurrence")]
    public async Task<IActionResult> RescheduleOccurrence(
        [FromRoute] Guid id,
        [FromBody] RescheduleSubscriptionOccurrenceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SourceDateLocal) || string.IsNullOrWhiteSpace(request.TargetDateLocal))
        {
            return BadRequest(new { error = "Köhnə və yeni tarix daxil edin (yyyy-MM-dd)." });
        }

        if (!DateTime.TryParseExact(request.SourceDateLocal.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var sourceDate))
        {
            return BadRequest(new { error = "Köhnə tarix formatı yanlışdır (yyyy-MM-dd)." });
        }

        if (!DateTime.TryParseExact(request.TargetDateLocal.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var targetDate))
        {
            return BadRequest(new { error = "Yeni tarix formatı yanlışdır (yyyy-MM-dd)." });
        }

        if (!TimeSpan.TryParse(request.StartTimeLocal?.Trim(), out var startTimeLocal))
        {
            return BadRequest(new { error = "Saat formatı yanlışdır (HH:mm)." });
        }

        if (request.LaneNumber is < 0 or > 11)
        {
            return BadRequest(new { error = "Zolaq nömrəsi 0–11 aralığında olmalıdır." });
        }

        var sourceKey = sourceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var targetKey = targetDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (string.Equals(sourceKey, targetKey, StringComparison.Ordinal))
        {
            return BadRequest(new { error = "Köhnə və yeni tarix eyni ola bilməz." });
        }

        var schedules = (await repository.GetSubscriptionSchedulesAsync(cancellationToken)).ToList();
        var schedule = schedules.FirstOrDefault(x => x.Id == id);
        if (schedule is null)
        {
            return NotFound(new { error = "Abunə qrafiki tapılmadı." });
        }

        if (!schedule.IsEnabled)
        {
            return BadRequest(new { error = "Bu abunə qrafiki aktiv deyil." });
        }

        if (schedule.IsFullPackage)
        {
            return BadRequest(new { error = "Tam paket üçün seans köçürməsi tətbiq olunmur." });
        }

        var sourceErr = ValidateOccurrenceSourceDate(schedule, sourceDate);
        if (sourceErr is not null)
        {
            return BadRequest(new { error = "Köhnə tarix: " + sourceErr });
        }

        var targetErr = ValidateOccurrenceDateInPeriod(schedule, targetDate);
        if (targetErr is not null)
        {
            return BadRequest(new { error = "Yeni tarix: " + targetErr });
        }

        var durationMinutes = request.DurationMinutes is > 0 ? request.DurationMinutes.Value : schedule.DurationMinutes;
        if (durationMinutes <= 0)
        {
            return BadRequest(new { error = "Müddət müsbət olmalıdır." });
        }

        var athletes = (await repository.GetAthletesAsync(cancellationToken)).ToList();
        var athlete = athletes.FirstOrDefault(a => a.Id == schedule.AthleteId);
        if (athlete is null)
        {
            return BadRequest(new { error = "Müştəri tapılmadı." });
        }

        if (athlete.Category == CustomerCategory.Amateur && request.LaneNumber >= 9)
        {
            return BadRequest(new { error = "Həvəskar yalnız 1-8 zolaqlarda ola bilər." });
        }

        try
        {
            if (request.LaneNumber > 0)
            {
                await ValidateSessionOverlapForOccurrenceAsync(
                    repository,
                    request.LaneNumber,
                    targetDate,
                    startTimeLocal,
                    durationMinutes,
                    athletes,
                    schedule.AthleteId,
                    cancellationToken);

                AssertNoSubscriptionLaneConflictOnDate(
                    schedules,
                    schedule,
                    athletes,
                    targetDate,
                    startTimeLocal,
                    durationMinutes,
                    request.LaneNumber);
            }
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var excluded = OccurrenceJson.DeserializeExcluded(schedule.ExcludedOccurrenceDatesJson);
        var list = OccurrenceJson.DeserializeOverrides(schedule.OccurrenceOverridesJson);
        var isNaturalSourceDay = (int)sourceDate.DayOfWeek == schedule.DayOfWeek;
        if (isNaturalSourceDay)
        {
            excluded.Add(sourceKey);
            schedule.ExcludedOccurrenceDatesJson = OccurrenceJson.SerializeExcluded(excluded);
        }

        list.RemoveAll(o => string.Equals(o.DateLocal?.Trim(), sourceKey, StringComparison.Ordinal));

        var existingTarget = list.FirstOrDefault(o => string.Equals(o.DateLocal?.Trim(), targetKey, StringComparison.Ordinal));
        if (existingTarget is null)
        {
            list.Add(new OccurrenceJson.OverrideRow
            {
                DateLocal = targetKey,
                StartTimeLocal = startTimeLocal.ToString(@"hh\:mm", CultureInfo.InvariantCulture),
                LaneNumber = request.LaneNumber,
                DurationMinutes = durationMinutes
            });
        }
        else
        {
            existingTarget.StartTimeLocal = startTimeLocal.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
            existingTarget.LaneNumber = request.LaneNumber;
            existingTarget.DurationMinutes = durationMinutes;
        }

        excluded.Remove(targetKey);
        schedule.ExcludedOccurrenceDatesJson = excluded.Count > 0 ? OccurrenceJson.SerializeExcluded(excluded) : null;
        schedule.OccurrenceOverridesJson = OccurrenceJson.SerializeOverrides(list);
        await repository.UpdateSubscriptionScheduleAsync(schedule, cancellationToken);
        await CancelSessionsOnLocalDateAsync(
            schedule.AthleteId,
            schedule.Id,
            sourceDate,
            cancellationToken);
        return Ok(new { message = "Seans yeni tarixə köçürüldü, köhnə abunə günü ləğv edildi." });
    }

    private async Task CancelSessionsOnLocalDateAsync(
        Guid athleteId,
        Guid subscriptionScheduleId,
        DateTime dateLocal,
        CancellationToken cancellationToken)
    {
        var dayStartLocal = dateLocal.Date;
        var dayEndLocal = dayStartLocal.AddDays(1);
        var startUtc = DateTime.SpecifyKind(dayStartLocal, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(dayEndLocal, DateTimeKind.Local).ToUniversalTime();

        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var lanes = await repository.GetLanesAsync(cancellationToken);
        var laneById = lanes.ToDictionary(x => x.Id, x => x.Number);

        foreach (var session in sessions)
        {
            if (session.AthleteId != athleteId || session.Status == SessionStatus.Completed)
            {
                continue;
            }

            if (session.SubscriptionScheduleId is Guid linked && linked != subscriptionScheduleId)
            {
                continue;
            }

            var sessionStart = AsUtc(session.StartTimeUtc);
            if (sessionStart < startUtc || sessionStart >= endUtc)
            {
                continue;
            }

            SessionHousekeeping.MarkCompleted(session, DateTime.UtcNow);
            await repository.UpdateSessionAsync(session, cancellationToken);

            if (laneById.TryGetValue(session.LaneId, out var laneNumber))
            {
                await notifier.PublishLaneUpdateAsync(laneNumber, cancellationToken);
            }
        }
    }

    private static string? ValidateOccurrenceDateInPeriod(SubscriptionSchedule schedule, DateTime date)
    {
        var d = date.Date;
        if (d < schedule.ActiveFromDateLocal.Date || d > schedule.ActiveToDateLocal.Date)
        {
            return "Tarix abunə aralığının xaricindədir.";
        }

        return null;
    }

    private static string? ValidateOccurrenceSourceDate(SubscriptionSchedule schedule, DateTime date)
    {
        var periodErr = ValidateOccurrenceDateInPeriod(schedule, date);
        if (periodErr is not null)
        {
            return periodErr;
        }

        var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (OccurrenceJson.DeserializeExcluded(schedule.ExcludedOccurrenceDatesJson).Contains(key))
        {
            return "Bu tarix artıq ləğv edilib.";
        }

        if ((int)date.DayOfWeek == schedule.DayOfWeek)
        {
            return null;
        }

        if (OccurrenceJson.DeserializeOverrides(schedule.OccurrenceOverridesJson)
            .Any(o => string.Equals(o.DateLocal?.Trim(), key, StringComparison.Ordinal)))
        {
            return null;
        }

        return "Bu tarix abunə planına uyğun deyil.";
    }

    private static string? ValidateOccurrenceDate(SubscriptionSchedule schedule, DateTime date)
    {
        var d = date.Date;
        if (d < schedule.ActiveFromDateLocal.Date || d > schedule.ActiveToDateLocal.Date)
        {
            return "Tarix abunə aralığının xaricindədir.";
        }

        if ((int)d.DayOfWeek != schedule.DayOfWeek)
        {
            return "Bu tarix seçilmiş abunə həftə gününə uyğun gəlmir.";
        }

        return null;
    }

    private static void GetEffectiveOccurrence(
        SubscriptionSchedule schedule,
        DateTime date,
        out TimeSpan start,
        out int duration,
        out int lane)
    {
        start = schedule.StartTimeLocal;
        duration = schedule.DurationMinutes;
        lane = schedule.LaneNumber;
        var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        foreach (var ov in OccurrenceJson.DeserializeOverrides(schedule.OccurrenceOverridesJson))
        {
            if (!string.Equals(ov.DateLocal?.Trim(), key, StringComparison.Ordinal)) continue;
            if (!string.IsNullOrWhiteSpace(ov.StartTimeLocal) && TimeSpan.TryParse(ov.StartTimeLocal, out var st))
            {
                start = st;
            }

            if (ov.DurationMinutes is > 0)
            {
                duration = ov.DurationMinutes.Value;
            }

            if (ov.LaneNumber is > 0)
            {
                lane = ov.LaneNumber.Value;
            }

            break;
        }
    }

    private static bool IsScheduleOccurringOnDate(SubscriptionSchedule s, DateTime date)
    {
        if (!s.IsEnabled || s.IsFullPackage)
        {
            return false;
        }

        var d = date.Date;
        if (d < s.ActiveFromDateLocal.Date || d > s.ActiveToDateLocal.Date)
        {
            return false;
        }

        if ((int)d.DayOfWeek != s.DayOfWeek)
        {
            return false;
        }

        var key = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return !OccurrenceJson.DeserializeExcluded(s.ExcludedOccurrenceDatesJson).Contains(key);
    }

    private static bool HasEffectiveOccurrenceOnDate(SubscriptionSchedule s, DateTime date)
    {
        if (!s.IsEnabled || s.IsFullPackage)
        {
            return false;
        }

        var d = date.Date;
        if (d < s.ActiveFromDateLocal.Date || d > s.ActiveToDateLocal.Date)
        {
            return false;
        }

        var key = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (OccurrenceJson.DeserializeExcluded(s.ExcludedOccurrenceDatesJson).Contains(key))
        {
            return false;
        }

        if (OccurrenceJson.DeserializeOverrides(s.OccurrenceOverridesJson)
            .Any(o => string.Equals(o.DateLocal?.Trim(), key, StringComparison.Ordinal)))
        {
            return true;
        }

        return IsScheduleOccurringOnDate(s, date);
    }

    private static bool TimeRangesOverlap(TimeSpan aStart, TimeSpan aEnd, TimeSpan bStart, TimeSpan bEnd)
        => aStart < bEnd && bStart < aEnd;

    private static void AssertNoSubscriptionLaneConflictOnDate(
        List<SubscriptionSchedule> schedules,
        SubscriptionSchedule self,
        IReadOnlyList<Athlete> athletes,
        DateTime date,
        TimeSpan requestedStart,
        int requestedDurationMinutes,
        int requestedLane)
    {
        if (requestedLane <= 0)
        {
            return;
        }

        var requestedEnd = requestedStart.Add(TimeSpan.FromMinutes(requestedDurationMinutes));

        foreach (var other in schedules)
        {
            if (other.Id == self.Id || !other.IsEnabled || other.IsFullPackage)
            {
                continue;
            }

            if (other.AthleteId == self.AthleteId)
            {
                continue;
            }

            if (!HasEffectiveOccurrenceOnDate(other, date))
            {
                continue;
            }

            GetEffectiveOccurrence(other, date, out var oStart, out var oDur, out var oLane);
            if (oLane <= 0 || oLane != requestedLane)
            {
                continue;
            }

            var oEnd = oStart.Add(TimeSpan.FromMinutes(oDur));
            if (!TimeRangesOverlap(requestedStart, requestedEnd, oStart, oEnd))
            {
                continue;
            }

            var conflictingName = athletes.FirstOrDefault(a => a.Id == other.AthleteId)?.FullName ?? "başqa müştəri";
            throw new InvalidOperationException(
                $"Təəssüf ki, seçdiyiniz saatda Zolaq {requestedLane} doludur (Müştəri {conflictingName} tərəfindən). Zəhmət olmasa başqa vaxt seçin");
        }
    }

    private static async Task ValidateSessionOverlapForOccurrenceAsync(
        ITrainingCenterRepository repository,
        int laneNumber,
        DateTime dateLocal,
        TimeSpan startLocal,
        int durationMinutes,
        List<Athlete> athletes,
        Guid ignoreAthleteId,
        CancellationToken cancellationToken)
    {
        var lanes = await repository.GetLanesAsync(cancellationToken);
        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var lane = lanes.FirstOrDefault(l => l.Number == laneNumber);
        if (lane is null)
        {
            return;
        }

        var requestedStartLocal = dateLocal.Date + startLocal;
        var requestedEndLocal = requestedStartLocal.AddMinutes(durationMinutes);
        var requestedStartTod = requestedStartLocal.TimeOfDay;
        var requestedEndTod = requestedEndLocal.TimeOfDay;

        var laneSessions = sessions.Where(s => s.LaneId == lane.Id);
        foreach (var s in laneSessions)
        {
            if (s.AthleteId == ignoreAthleteId)
            {
                continue;
            }

            var sStartUtc = DateTimeAssumedUtc.AsUtc(s.StartTimeUtc);
            var sEndUtc = DateTimeAssumedUtc.AsUtc(s.EndTimeUtc);
            var sStartLocal = sStartUtc.ToLocalTime();
            var sEndLocal = sEndUtc.ToLocalTime();
            if (sStartLocal.Date != dateLocal.Date)
            {
                continue;
            }

            var sStartTod = sStartLocal.TimeOfDay;
            var sEndTod = sEndLocal.TimeOfDay;
            if (sEndTod <= sStartTod)
            {
                continue;
            }

            if (requestedStartTod < sEndTod && sStartTod < requestedEndTod)
            {
                var otherName = athletes.FirstOrDefault(a => a.Id == s.AthleteId)?.FullName ?? "başqa müştəri";
                throw new InvalidOperationException(
                    $"Təəssüf ki, seçdiyiniz saatda Zolaq {laneNumber} doludur (Müştəri {otherName} tərəfindən saat {sEndLocal:HH:mm}-a qədər). Zəhmət olmasa başqa vaxt seçin");
            }
        }
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
                    request.EndDateLocal,
                    request.PreferredLaneTypesByDayOfWeek,
                    request.IsFullPackage),
                cancellationToken);

            if (request.ServicePackageId is Guid pkgId && pkgId != Guid.Empty)
            {
                var athletes = await repository.GetAthletesAsync(cancellationToken);
                var athlete = request.AthleteId is Guid aid && aid != Guid.Empty
                    ? athletes.FirstOrDefault(x => x.Id == aid)
                    : athletes.FirstOrDefault(x =>
                        string.Equals(x.FullName, request.AthleteFullName.Trim(), StringComparison.OrdinalIgnoreCase));
                if (athlete is not null)
                {
                    var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
                    var latestSchedule = schedules
                        .Where(s => s.AthleteId == athlete.Id)
                        .OrderByDescending(s => s.CreatedAtUtc)
                        .FirstOrDefault();
                    await CustomerBillingService.RecordPackageAsync(
                        repository,
                        athlete.Id,
                        pkgId,
                        null,
                        null,
                        null,
                        request.DiscountAmount,
                        request.AmountPaidCash,
                        request.AmountPaidCard,
                        request.IsComplimentary,
                        null,
                        latestSchedule?.Id,
                        User.GetStaffMemberId(),
                        cancellationToken);
                }
            }

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
