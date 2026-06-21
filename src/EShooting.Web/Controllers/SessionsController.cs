using EShooting.Web.Contracts.Sessions;
using EShooting.Application.Sessions.Commands;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common;
using EShooting.Application.Common.Models;
using EShooting.Domain.Entities;
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
                var kind = LaneDisplayHelper.IsGroupAthleteName(athleteName) ? "Qrup" : "Anlıq";
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
        var equipmentIssues = MapEquipmentIssues(request.EquipmentIssues);
        try
        {
            var sessionId = await mediator.Send(new ScheduleSessionCommand(
                request.AthleteId,
                request.LaneNumber,
                request.StartTimeUtc,
                request.DurationMinutes,
                request.IsEquipmentIssued,
                request.PreferredLaneType,
                equipmentIssues), cancellationToken);

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
                                request.PreferredLaneType,
                                equipmentIssues),
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
        var equipmentItems = await repository.GetEquipmentItemsAsync(activeOnly: false, cancellationToken);
        var issues = await repository.GetSessionEquipmentIssuesAsync(cancellationToken);
        var buffer = LaneReservationRules.SessionBuffer;

        var pending = new List<(DateTime TrainingEndUtc, object Row)>();

        foreach (var issue in issues.Where(x => x.IssueType == EquipmentIssueType.Rental && x.ReturnedAtUtc is null))
        {
            var session = sessions.FirstOrDefault(s => s.Id == issue.SessionId);
            if (session is null) continue;

            var startUtc = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
            if (nowUtc < startUtc) continue;

            var laneNumber = lanes.FirstOrDefault(l => l.Id == session.LaneId)?.Number ?? 0;
            var athleteName = athletes.FirstOrDefault(a => a.Id == session.AthleteId)?.FullName ?? "—";
            var equipmentName = equipmentItems.FirstOrDefault(e => e.Id == issue.EquipmentItemId)?.Name ?? "Avadanlıq";
            var endUtc = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
            var trainingEndUtc = endUtc - buffer;
            var remaining = trainingEndUtc - nowUtc;
            var color = remaining <= TimeSpan.Zero
                ? "red"
                : remaining <= TimeSpan.FromMinutes(5)
                    ? "yellow"
                    : "normal";

            pending.Add((trainingEndUtc, new
            {
                issueId = issue.Id,
                sessionId = session.Id,
                athleteName,
                laneNumber,
                equipmentName,
                issueType = issue.IssueType.ToString(),
                trainingEndUtc,
                color
            }));
        }

        foreach (var session in sessions.Where(s => s.IsEquipmentIssued && s.EquipmentReturnedAtUtc is null))
        {
            if (issues.Any(i => i.SessionId == session.Id && i.IssueType == EquipmentIssueType.Rental))
            {
                continue;
            }

            var startUtc = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
            if (nowUtc < startUtc) continue;

            var laneNumber = lanes.FirstOrDefault(l => l.Id == session.LaneId)?.Number ?? 0;
            var athleteName = athletes.FirstOrDefault(a => a.Id == session.AthleteId)?.FullName ?? "—";
            var endUtc = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
            var trainingEndUtc = endUtc - buffer;
            var remaining = trainingEndUtc - nowUtc;
            var color = remaining <= TimeSpan.Zero
                ? "red"
                : remaining <= TimeSpan.FromMinutes(5)
                    ? "yellow"
                    : "normal";

            pending.Add((trainingEndUtc, new
            {
                issueId = (Guid?)null,
                sessionId = session.Id,
                athleteName,
                laneNumber,
                equipmentName = "Avadanlıq",
                issueType = "Rental",
                trainingEndUtc,
                color
            }));
        }

        return Ok(new { pending = pending.OrderBy(x => x.TrainingEndUtc).Select(x => x.Row).ToList() });
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
        var issueIds = request.IssueIds ?? [];
        var sessionIds = request.SessionIds ?? [];
        if (issueIds.Count == 0 && sessionIds.Count == 0)
        {
            return BadRequest(new { error = "Avadanlıq seçilməyib." });
        }

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var touchedLaneNumbers = new HashSet<int>();
        var touchedSessionIds = new HashSet<Guid>();

        foreach (var issueId in issueIds.Distinct())
        {
            var issue = await repository.GetSessionEquipmentIssueByIdAsync(issueId, cancellationToken);
            if (issue is null || issue.IssueType != EquipmentIssueType.Rental || issue.ReturnedAtUtc is not null)
            {
                continue;
            }

            issue.ReturnedAtUtc = DateTime.UtcNow;
            await repository.UpdateSessionEquipmentIssueAsync(issue, cancellationToken);
            touchedSessionIds.Add(issue.SessionId);
        }

        foreach (var sessionId in sessionIds.Distinct())
        {
            var session = await repository.GetSessionByIdAsync(sessionId, cancellationToken);
            if (session is null) continue;

            var issues = (await repository.GetSessionEquipmentIssuesAsync(cancellationToken))
                .Where(x => x.SessionId == sessionId && x.IssueType == EquipmentIssueType.Rental && x.ReturnedAtUtc is null)
                .ToList();

            if (issues.Count > 0)
            {
                foreach (var issue in issues)
                {
                    issue.ReturnedAtUtc = DateTime.UtcNow;
                    await repository.UpdateSessionEquipmentIssueAsync(issue, cancellationToken);
                }

                touchedSessionIds.Add(sessionId);
                continue;
            }

            if (!session.IsEquipmentIssued || session.EquipmentReturnedAtUtc is not null) continue;

            session.EquipmentReturnedAtUtc = DateTime.UtcNow;
            await repository.UpdateSessionAsync(session, cancellationToken);
            touchedSessionIds.Add(sessionId);
        }

        foreach (var sessionId in touchedSessionIds)
        {
            await SyncSessionEquipmentReturnStatusAsync(sessionId, cancellationToken);
            var session = await repository.GetSessionByIdAsync(sessionId, cancellationToken);
            if (session is null) continue;
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
        try
        {
            await mediator.Send(new CompleteSessionCommand(sessionId), cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Full paket abunəçini gəldiyi anda uyğun boş xətdə aktiv sessiyaya yerləşdirir.
    /// 10 dəqiqə buffer qaydası LaneReservationRules ilə qorunur.
    /// </summary>
    [HttpPost("full-package/assign")]
    public async Task<IActionResult> AssignFullPackage([FromBody] FullPackageAssignRequest request, CancellationToken cancellationToken)
    {
        var duration = request.DurationMinutes;
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var athlete = athletes.FirstOrDefault(x => x.Id == request.AthleteId);
        if (athlete is null)
        {
            return NotFound(new { error = "Athlete not found." });
        }

        if (!athlete.IsSubscriber || !athlete.IsFullPackage)
        {
            return BadRequest(new { error = "Bu müştərinin aktiv çevik abunəsi yoxdur." });
        }

        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var activeWalkIn = WalkInSubscriptionRules.GetActiveWalkInSchedule(
            schedules,
            athlete.Id,
            DateTime.Now);

        if (activeWalkIn is null)
        {
            return BadRequest(new { error = "Çevik abunə müddəti bitib və ya aktiv deyil. Yeniləyin." });
        }

        var sessionDuration = activeWalkIn.DurationMinutes;
        if (sessionDuration <= 0 && duration > 0)
        {
            sessionDuration = duration;
        }

        if (sessionDuration < 0 || sessionDuration > 600)
        {
            return BadRequest(new { error = "DurationMinutes must be between 0 and 600." });
        }
        var allLanes = await repository.GetLanesAsync(cancellationToken);
        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var startUtc = nowUtc;
        var endUtc = startUtc.AddMinutes(sessionDuration);

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
                    sessionDuration,
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

    private static IReadOnlyList<SessionEquipmentIssueRequest> MapEquipmentIssues(IReadOnlyList<SessionEquipmentIssueDto>? items)
    {
        if (items is null || items.Count == 0)
        {
            return [];
        }

        return items
            .Where(x => x.EquipmentItemId != Guid.Empty)
            .Select(x => new SessionEquipmentIssueRequest
            {
                EquipmentItemId = x.EquipmentItemId,
                IssueType = x.IssueType
            })
            .ToList();
    }

    private async Task SyncSessionEquipmentReturnStatusAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await repository.GetSessionByIdAsync(sessionId, cancellationToken);
        if (session is null || !session.IsEquipmentIssued)
        {
            return;
        }

        var rentals = (await repository.GetSessionEquipmentIssuesAsync(cancellationToken))
            .Where(x => x.SessionId == sessionId && x.IssueType == EquipmentIssueType.Rental)
            .ToList();

        if (rentals.Count == 0)
        {
            return;
        }

        if (rentals.All(x => x.ReturnedAtUtc is not null))
        {
            session.EquipmentReturnedAtUtc = DateTime.UtcNow;
            await repository.UpdateSessionAsync(session, cancellationToken);
        }
    }
}
