using EShooting.Web.Contracts.Sessions;
using EShooting.Application.Sessions.Commands;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common;
using EShooting.Domain.Enums;
using EShooting.Web.Helpers;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("sessions")]
public sealed class SessionsController(IMediator mediator, ITrainingCenterRepository repository, IRealtimeNotifier notifier) : ControllerBase
{
    [HttpGet("by-date")]
    public async Task<IActionResult> GetByDate(
        [FromQuery] DateTime dateLocal,
        CancellationToken cancellationToken)
    {
        var day = dateLocal.Date;
        var startUtc = DateTime.SpecifyKind(day, DateTimeKind.Local).ToUniversalTime();
        var endUtc = DateTime.SpecifyKind(day.AddDays(1), DateTimeKind.Local).ToUniversalTime();

        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var lanes = await repository.GetLanesAsync(cancellationToken);
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;

        var items = sessions
            .Where(s =>
            {
                var sStart = DateTimeAssumedUtc.AsUtc(s.StartTimeUtc);
                return sStart >= startUtc && sStart < endUtc;
            })
            .OrderBy(s => DateTimeAssumedUtc.AsUtc(s.StartTimeUtc))
            .Select(s =>
            {
                var sStartUtc = DateTimeAssumedUtc.AsUtc(s.StartTimeUtc);
                var sEndUtc = DateTimeAssumedUtc.AsUtc(s.EndTimeUtc);
                var laneNumber = lanes.FirstOrDefault(l => l.Id == s.LaneId)?.Number ?? 0;
                var athleteName = athletes.FirstOrDefault(a => a.Id == s.AthleteId)?.FullName ?? "—";
                var kind = athleteName.StartsWith("Qrup:", StringComparison.OrdinalIgnoreCase) ? "Qrup" : "Anlıq";
                var derivedStatus = s.Status;
                if (derivedStatus != SessionStatus.Completed)
                {
                    if (nowUtc >= sEndUtc)
                    {
                        derivedStatus = SessionStatus.Completed;
                    }
                    else if (nowUtc < sStartUtc)
                    {
                        derivedStatus = SessionStatus.Scheduled;
                    }
                    else
                    {
                        derivedStatus = SessionStatus.Active;
                    }
                }
                return new
                {
                    sessionId = s.Id,
                    athleteId = s.AthleteId,
                    athleteName,
                    laneNumber,
                    startTimeUtc = sStartUtc,
                    endTimeUtc = sEndUtc,
                    status = derivedStatus.ToString(),
                    statusLabel = LaneDisplayHelper.TranslateStatus(derivedStatus.ToString()),
                    kind,
                    subscriptionScheduleId = s.SubscriptionScheduleId
                };
            })
            .ToList();

        return Ok(new
        {
            dateLocal = day.ToString("yyyy-MM-dd"),
            sessions = items
        });
    }

    [HttpGet("active-by-athlete/{athleteId:guid}")]
    public async Task<IActionResult> GetActiveByAthlete([FromRoute] Guid athleteId, CancellationToken cancellationToken)
    {
        var active = await repository.TryGetActiveSessionForAthleteAsync(athleteId, cancellationToken);
        if (active is null)
        {
            return NotFound();
        }

        return Ok(new
        {
            sessionId = active.Value.SessionId,
            laneNumber = active.Value.LaneNumber,
            status = SessionStatus.Active.ToString()
        });
    }

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

            var session = await repository.GetSessionByIdAsync(sessionId, cancellationToken);
            var lanes = await repository.GetLanesAsync(cancellationToken);
            var laneNumber = session is null
                ? request.LaneNumber
                : (lanes.FirstOrDefault(l => l.Id == session.LaneId)?.Number ?? request.LaneNumber);

            return Ok(new { sessionId, laneNumber });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("tutulub", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = ex.Message });
            }
            // Smart lane allocation: if lane is reserved/busy, try other lanes automatically.
            if (ex.Message.Contains("rezerv", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("doludur", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("busy", StringComparison.OrdinalIgnoreCase))
            {
                if (request.LaneNumber <= 0)
                {
                    return Conflict(new { error = ex.Message });
                }

                var athletes = await repository.GetAthletesAsync(cancellationToken);
                var athlete = athletes.FirstOrDefault(x => x.Id == request.AthleteId);
                var allowed = athlete?.Category == CustomerCategory.Amateur
                    ? Enumerable.Range(1, 8).ToList()
                    : request.PreferredLaneType switch
                    {
                        PreferredLaneType.Short => Enumerable.Range(1, 8).ToList(),
                        PreferredLaneType.Long => Enumerable.Range(9, 3).ToList(),
                        _ => Enumerable.Range(1, 11).ToList()
                    };

                foreach (var alt in allowed.Where(x => x != request.LaneNumber))
                {
                    try
                    {
                        var sid = await mediator.Send(
                            new ScheduleSessionCommand(
                                request.AthleteId,
                                alt,
                                request.StartTimeUtc,
                                request.DurationMinutes,
                                request.IsEquipmentIssued,
                                request.PreferredLaneType),
                            cancellationToken);
                        return Ok(new
                        {
                            sessionId = sid,
                            laneNumber = alt,
                            message = $"Seçdiyiniz zolaq doludur, qeydiyyat avtomatik olaraq boş olan {alt} nömrəli zolağa keçirildi."
                        });
                    }
                    catch (InvalidOperationException)
                    {
                        // try next lane
                    }
                }

                return Conflict(new { error = "Seçdiyiniz vaxt aralığında bütün zolaqlar doludur. Zəhmət olmasa başqa vaxt seçin." });
            }

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
            if (ex.Message.Contains("tutulub", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = ex.Message });
            }
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
    /// Son daxil edilən xalı (last score) geri alır.
    /// </summary>
    [HttpDelete("{sessionId:guid}/scores/last")]
    public async Task<IActionResult> DeleteLastScore(Guid sessionId, CancellationToken cancellationToken)
    {
        try
        {
            var totalScore = await mediator.Send(new DeleteLastScoreCommand(sessionId), cancellationToken);
            if (totalScore is null)
            {
                return NotFound(new { error = "Sessiya tapılmadı." });
            }

            return Ok(new { totalScore });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = ex.Message });
        }
    }

    [HttpGet("equipment/pending")]
    public async Task<IActionResult> GetPendingEquipmentReturns(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var lanes = await repository.GetLanesAsync(cancellationToken);
        var athletes = await repository.GetAthletesAsync(cancellationToken);

        var buffer = LaneReservationRules.SessionBuffer;

        var pending = sessions
            .Where(s => s.IsEquipmentIssued && s.EquipmentReturnedAtUtc is null)
            .OrderBy(s => DateTimeAssumedUtc.AsUtc(s.EndTimeUtc))
            .Select(s =>
            {
                var laneNumber = lanes.FirstOrDefault(l => l.Id == s.LaneId)?.Number ?? 0;
                var athleteName = athletes.FirstOrDefault(a => a.Id == s.AthleteId)?.FullName ?? "—";
                var endUtc = DateTimeAssumedUtc.AsUtc(s.EndTimeUtc);
                var trainingEndUtc = endUtc - buffer;
                var remaining = trainingEndUtc - nowUtc;

                var color = remaining <= TimeSpan.Zero
                    ? "red"
                    : remaining <= TimeSpan.FromMinutes(5)
                        ? "yellow"
                        : "normal";

                return new
                {
                    sessionId = s.Id,
                    athleteName,
                    laneNumber,
                    trainingEndUtc,
                    color
                };
            })
            .ToList();

        return Ok(new { pending });
    }

    [HttpPost("{sessionId:guid}/equipment/return")]
    public async Task<IActionResult> ReturnEquipment([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await repository.GetSessionByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound(new { error = "Sessiya tapılmadı." });
        }

        if (!session.IsEquipmentIssued || session.EquipmentReturnedAtUtc is not null)
        {
            return NoContent();
        }

        session.EquipmentReturnedAtUtc = DateTime.UtcNow;
        await repository.UpdateSessionAsync(session, cancellationToken);

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var laneNumber = lanes.FirstOrDefault(l => l.Id == session.LaneId)?.Number ?? 0;
        if (laneNumber > 0)
        {
            await notifier.PublishLaneUpdateAsync(laneNumber, cancellationToken);
        }

        return NoContent();
    }

    [HttpPost("equipment/return-bulk")]
    public async Task<IActionResult> ReturnEquipmentBulk([FromBody] ReturnEquipmentBulkRequest request, CancellationToken cancellationToken)
    {
        if (request.SessionIds is null || request.SessionIds.Count == 0)
        {
            return BadRequest(new { error = "Sessiya seçilməyib." });
        }

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var touchedLaneNumbers = new HashSet<int>();

        foreach (var sessionId in request.SessionIds.Distinct())
        {
            var session = await repository.GetSessionByIdAsync(sessionId, cancellationToken);
            if (session is null) continue;
            if (!session.IsEquipmentIssued || session.EquipmentReturnedAtUtc is not null) continue;

            session.EquipmentReturnedAtUtc = DateTime.UtcNow;
            await repository.UpdateSessionAsync(session, cancellationToken);

            var laneNumber = lanes.FirstOrDefault(l => l.Id == session.LaneId)?.Number ?? 0;
            if (laneNumber > 0) touchedLaneNumbers.Add(laneNumber);
        }

        foreach (var laneNumber in touchedLaneNumbers)
        {
            await notifier.PublishLaneUpdateAsync(laneNumber, cancellationToken);
        }

        return NoContent();
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

    /// <summary>
    /// Full paket abunəçini gəldiyi anda uyğun boş xətdə aktiv sessiyaya yerləşdirir.
    /// 10 dəqiqə buffer qaydası LaneReservationRules ilə qorunur.
    /// </summary>
    [HttpPost("full-package/assign")]
    public async Task<IActionResult> AssignFullPackage([FromBody] FullPackageAssignRequest request, CancellationToken cancellationToken)
    {
        var duration = request.DurationMinutes;
        if (duration <= 0 || duration > 240)
        {
            return BadRequest(new { error = "DurationMinutes must be between 1 and 240." });
        }

        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var athlete = athletes.FirstOrDefault(x => x.Id == request.AthleteId);
        if (athlete is null)
        {
            return NotFound(new { error = "Athlete not found." });
        }

        if (!athlete.IsSubscriber || !athlete.IsFullPackage)
        {
            return BadRequest(new { error = "Athlete is not a Full Package subscriber." });
        }

        var allLanes = await repository.GetLanesAsync(cancellationToken);
        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var startUtc = nowUtc;
        var endUtc = startUtc.AddMinutes(duration);

        var candidates = athlete.Category == CustomerCategory.Amateur
            ? allLanes.Where(l => l.Number is >= 1 and <= 8).ToList()
            : allLanes.ToList();

        var selected = LaneReservationRules.SelectAvailableLane(candidates, sessions, startUtc, endUtc, nowUtc);
        if (selected is null)
        {
            var label = athlete.Category == CustomerCategory.Amateur ? "Qısa" : "uyğun";
            return Conflict(new { error = $"Təəssüf ki, hazırda bütün {label} xətlər doludur. Zəhmət olmasa bir az sonra yenidən yoxlayın." });
        }

        try
        {
            var sessionId = await mediator.Send(
                new ScheduleSessionCommand(
                    athlete.Id,
                    selected.Number,
                    startUtc,
                    duration,
                    request.IsEquipmentIssued,
                    PreferredLaneType.Any),
                cancellationToken);

            return Ok(new { sessionId, laneNumber = selected.Number });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
