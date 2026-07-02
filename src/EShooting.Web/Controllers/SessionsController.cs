using EShooting.Web.Contracts.Sessions;
using EShooting.Application.Common;
using EShooting.Application.Customers;
using EShooting.Application.Equipment;
using EShooting.Application.Sessions.Commands;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using EShooting.Web.Auth;
using EShooting.Web.Helpers;
using EShooting.Web.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EShooting.Web.Controllers;

[ApiController]
[Route("sessions")]
public sealed class SessionsController(IMediator mediator, ITrainingCenterRepository repository, IRealtimeNotifier notifier) : ControllerBase
{
    private static bool HasActivation(TrainingSession session)
        => session.ActivatedAtUtc is not null || session.Status == SessionStatus.Active;

    private static DateTime ResolveEffectiveStartUtc(TrainingSession session)
        => session.ActivatedAtUtc is DateTime a ? DateTimeAssumedUtc.AsUtc(a) : DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);

    private static DateTime ResolveEffectiveEndUtc(TrainingSession session)
    {
        var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        var duration = plannedEnd > plannedStart ? plannedEnd - plannedStart : TimeSpan.Zero;
        var start = ResolveEffectiveStartUtc(session);
        return duration > TimeSpan.Zero ? start + duration : start;
    }

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
                var effectiveStartUtc = HasActivation(s) ? ResolveEffectiveStartUtc(s) : sStartUtc;
                var effectiveEndUtc = HasActivation(s) ? ResolveEffectiveEndUtc(s) : sEndUtc;
                var laneNumber = lanes.FirstOrDefault(l => l.Id == s.LaneId)?.Number ?? 0;
                var athleteName = athletes.FirstOrDefault(a => a.Id == s.AthleteId)?.FullName ?? "—";
                var kind = LaneDisplayHelper.IsGroupAthleteName(athleteName) ? "Qrup" : "Anlıq";
                var derivedStatus = s.Status;
                if (derivedStatus != SessionStatus.Completed)
                {
                    var isOpenEnded = sEndUtc <= sStartUtc;
                    if (!HasActivation(s))
                    {
                        derivedStatus = SessionStatus.Scheduled;
                    }
                    else if (!isOpenEnded && nowUtc >= effectiveEndUtc)
                    {
                        derivedStatus = SessionStatus.Completed;
                    }
                    else if (nowUtc < effectiveStartUtc)
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
                    startTimeUtc = effectiveStartUtc,
                    endTimeUtc = effectiveEndUtc,
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
        if (ReceptionPermissionGate.DenyUnless(this,ReceptionStaffClaims.CanManageSessions) is { } denied)
        {
            return denied;
        }

        if (request.IsComplimentary
            && ReceptionPermissionGate.DenyUnless(this,ReceptionStaffClaims.CanGrantComplimentarySession) is { } compDenied)
        {
            return compDenied;
        }

        if (!request.IsComplimentary)
        {
            try
            {
                PaymentSettlementRules.EnsureDiscountAllowed(
                    request.DiscountAmount,
                    User.HasReceptionPermission(ReceptionStaffClaims.CanApplyDiscount));
            }
            catch (InvalidOperationException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { error = ex.Message });
            }
        }

        var equipmentIssues = MapEquipmentIssues(request.EquipmentIssues);
        if ((request.IsEquipmentIssued || equipmentIssues.Count > 0)
            && ReceptionPermissionGate.DenyUnless(this,ReceptionStaffClaims.CanManageEquipment) is { } equipDenied)
        {
            return equipDenied;
        }

        try
        {
            var sessionId = await mediator.Send(new ScheduleSessionCommand(
                request.AthleteId,
                request.LaneNumber,
                request.StartTimeUtc,
                request.DurationMinutes,
                request.IsEquipmentIssued,
                request.PreferredLaneType,
                equipmentIssues,
                User.GetStaffMemberId(),
                request.ForceOpenEnded), cancellationToken);

            var session = await repository.GetSessionByIdAsync(sessionId, cancellationToken);
            var lanes = await repository.GetLanesAsync(cancellationToken);
            var laneNumber = session is null
                ? request.LaneNumber
                : (lanes.FirstOrDefault(l => l.Id == session.LaneId)?.Number ?? request.LaneNumber);

            if (request.ServicePackageId is Guid pkgId && pkgId != Guid.Empty)
            {
                await CustomerBillingService.RecordSessionBookingBillingAsync(
                    repository,
                    request.AthleteId,
                    pkgId,
                    sessionId,
                    request.DiscountAmount,
                    request.AmountPaidCash,
                    request.AmountPaidCard,
                    request.IsComplimentary,
                    User.GetStaffMemberId(),
                    cancellationToken);
            }

            return Ok(new { sessionId, laneNumber });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("tutulub", StringComparison.OrdinalIgnoreCase))
            {
                return Conflict(new { error = ex.Message });
            }
            if (ex.Message.Contains("aktivdir", StringComparison.OrdinalIgnoreCase))
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
                                equipmentIssues,
                                User.GetStaffMemberId(),
                                request.ForceOpenEnded),
                            cancellationToken);

                        if (request.ServicePackageId is Guid altPkgId && altPkgId != Guid.Empty)
                        {
                            await CustomerBillingService.RecordSessionBookingBillingAsync(
                                repository,
                                request.AthleteId,
                                altPkgId,
                                sid,
                                request.DiscountAmount,
                                request.AmountPaidCash,
                                request.AmountPaidCard,
                                request.IsComplimentary,
                                User.GetStaffMemberId(),
                                cancellationToken);
                        }

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
    [Authorize(Policy = PlansetAuthDefaults.Policy)]
    public async Task<IActionResult> GetPendingEquipmentReturns(CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var lanes = await repository.GetLanesAsync(cancellationToken);
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var equipmentItems = await repository.GetEquipmentItemsAsync(activeOnly: false, cancellationToken);
        var issues = await repository.GetSessionEquipmentIssuesAsync(cancellationToken);
        var staffMembers = await repository.GetStaffMembersAsync(activeOnly: false, cancellationToken);
        var staffNames = staffMembers.ToDictionary(
            s => s.Id,
            s => $"{s.FirstName} {s.LastName}".Trim());
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
            var quantity = Math.Max(1, issue.Quantity);
            var issuedByStaffName = issue.IssuedByStaffId is Guid issuerId && staffNames.TryGetValue(issuerId, out var issuerName)
                ? issuerName
                : "—";
            var endUtc = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
            var trainingEndUtc = endUtc - buffer;
            var remaining = trainingEndUtc - nowUtc;
            var color = remaining <= TimeSpan.Zero
                ? "red"
                : remaining <= TimeSpan.FromMinutes(5)
                    ? "yellow"
                    : "normal";
            var timeStatusLabel = color switch
            {
                "red" => "Vaxt bitib",
                "yellow" => "Tezliklə bitir",
                _ => "Aktiv"
            };

            pending.Add((trainingEndUtc, new
            {
                issueId = issue.Id,
                equipmentItemId = issue.EquipmentItemId,
                sessionId = session.Id,
                athleteName,
                laneNumber,
                equipmentName,
                quantity,
                issuedByStaffName,
                issueType = issue.IssueType.ToString(),
                trainingEndUtc,
                color,
                timeStatusLabel
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
            var timeStatusLabel = color switch
            {
                "red" => "Vaxt bitib",
                "yellow" => "Tezliklə bitir",
                _ => "Aktiv"
            };

            pending.Add((trainingEndUtc, new
            {
                issueId = (Guid?)null,
                equipmentItemId = (Guid?)null,
                sessionId = session.Id,
                athleteName,
                laneNumber,
                equipmentName = "Avadanlıq",
                quantity = 1,
                issuedByStaffName = "—",
                issueType = "Rental",
                trainingEndUtc,
                color,
                timeStatusLabel
            }));
        }

        return Ok(new { pending = pending.OrderBy(x => x.TrainingEndUtc).Select(x => x.Row).ToList() });
    }

    [HttpPost("{sessionId:guid}/equipment/return")]
    [Authorize(Policy = PlansetAuthDefaults.Policy)]
    public async Task<IActionResult> ReturnEquipment([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await repository.GetSessionByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound(new { error = "Sessiya tapılmadı." });
        }

        var staffId = User.GetPlansetStaffMemberId();
        var issues = (await repository.GetSessionEquipmentIssuesAsync(cancellationToken))
            .Where(x => x.SessionId == sessionId && x.IssueType == EquipmentIssueType.Rental && x.ReturnedAtUtc is null)
            .ToList();

        if (issues.Count > 0)
        {
            foreach (var issue in issues)
            {
                await ReturnRentalIssueAsync(issue, staffId, 0, cancellationToken);
            }
        }
        else if (session.IsEquipmentIssued && session.EquipmentReturnedAtUtc is null)
        {
            session.EquipmentReturnedAtUtc = DateTime.UtcNow;
            await repository.UpdateSessionAsync(session, cancellationToken);
        }
        else
        {
            return NoContent();
        }

        await SyncSessionEquipmentReturnStatusAsync(sessionId, cancellationToken);

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var laneNumber = lanes.FirstOrDefault(l => l.Id == session.LaneId)?.Number ?? 0;
        if (laneNumber > 0)
        {
            await notifier.PublishLaneUpdateAsync(laneNumber, cancellationToken);
        }

        return NoContent();
    }

    /// <summary>
    /// Planşetdən (zal) aktiv sessiyaya icarə avadanlığı verir.
    /// Resepsiyada yalnız satış olur.
    /// </summary>
    [HttpPost("{sessionId:guid}/equipment/issue-rental")]
    [Authorize(Policy = PlansetAuthDefaults.Policy)]
    public async Task<IActionResult> IssueRentalEquipment(
        [FromRoute] Guid sessionId,
        [FromBody] IReadOnlyList<SessionEquipmentIssueDto>? items,
        CancellationToken cancellationToken)
    {
        if (!User.CanIssueEquipmentRental())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Avadanlıq icarə vermək üçün icazəniz yoxdur." });
        }

        var session = await repository.GetSessionByIdAsync(sessionId, cancellationToken);
        if (session is null)
        {
            return NotFound(new { error = "Sessiya tapılmadı." });
        }

        var requested = (items ?? [])
            .Where(x => x.EquipmentItemId != Guid.Empty && (x.Quantity <= 0 ? 1 : x.Quantity) > 0)
            .ToList();
        if (requested.Count == 0)
        {
            return BadRequest(new { error = "Avadanlıq seçilməyib." });
        }

        var staffId = User.GetPlansetStaffMemberId();

        foreach (var row in requested)
        {
            var item = await repository.GetEquipmentItemByIdAsync(row.EquipmentItemId, cancellationToken);
            if (item is null || !item.IsActive || item.IsDeleted)
            {
                return BadRequest(new { error = "Seçilmiş avadanlıq tapılmadı və ya deaktivdir." });
            }

            EquipmentIssuanceRules.ValidateIssueType(item, EquipmentIssueType.Rental);
            var qty = row.Quantity > 0 ? row.Quantity : 1;
            EquipmentIssuanceRules.ApplyStockOnIssue(item, EquipmentIssueType.Rental, qty);
            await repository.UpdateEquipmentItemAsync(item, cancellationToken);

            await repository.AddSessionEquipmentIssuesAsync(new[]
            {
                new SessionEquipmentIssue
                {
                    SessionId = session.Id,
                    EquipmentItemId = item.Id,
                    IssueType = EquipmentIssueType.Rental,
                    Quantity = qty,
                    UnitPrice = EquipmentIssuanceRules.ResolveUnitPrice(item, EquipmentIssueType.Rental),
                    IssuedByStaffId = staffId,
                    ReturnedAtUtc = null
                }
            }, cancellationToken);
        }

        session.IsEquipmentIssued = true;
        await repository.UpdateSessionAsync(session, cancellationToken);
        await notifier.PublishLaneUpdateAsync(
            (await repository.GetLanesAsync(cancellationToken)).FirstOrDefault(l => l.Id == session.LaneId)?.Number ?? 0,
            cancellationToken);
        return Ok(new { ok = true });
    }

    [HttpGet("{sessionId:guid}/equipment/rentals")]
    [Authorize(Policy = PlansetAuthDefaults.Policy)]
    public async Task<IActionResult> GetSessionRentals([FromRoute] Guid sessionId, CancellationToken cancellationToken)
    {
        var issues = (await repository.GetSessionEquipmentIssuesAsync(cancellationToken))
            .Where(x => x.SessionId == sessionId && x.IssueType == EquipmentIssueType.Rental)
            .ToList();
        var items = await repository.GetEquipmentItemsAsync(activeOnly: false, cancellationToken);
        var map = items.ToDictionary(x => x.Id, x => x.Name);

        var result = issues
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                id = x.Id,
                equipmentItemId = x.EquipmentItemId,
                equipmentName = map.GetValueOrDefault(x.EquipmentItemId) ?? "Avadanlıq",
                quantity = x.Quantity,
                returnedAtUtc = x.ReturnedAtUtc,
                issuedAtUtc = x.CreatedAtUtc
            })
            .ToList();

        return Ok(new { items = result, pendingCount = result.Count(x => x.returnedAtUtc is null) });
    }

    [HttpPost("equipment/return-bulk")]
    [Authorize(Policy = PlansetAuthDefaults.Policy)]
    public async Task<IActionResult> ReturnEquipmentBulk([FromBody] ReturnEquipmentBulkRequest request, CancellationToken cancellationToken)
    {
        var issueIds = request.IssueIds ?? [];
        var sessionIds = request.SessionIds ?? [];
        var damagedLines = (request.Damaged ?? [])
            .Where(x => x.SessionId != Guid.Empty && x.EquipmentItemId != Guid.Empty && x.Quantity > 0)
            .ToList();
        if (issueIds.Count == 0 && sessionIds.Count == 0)
        {
            return BadRequest(new { error = "Avadanlıq seçilməyib." });
        }

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var touchedLaneNumbers = new HashSet<int>();
        var touchedSessionIds = new HashSet<Guid>();
        var staffId = User.GetPlansetStaffMemberId();

        foreach (var issueId in issueIds.Distinct())
        {
            var issue = await repository.GetSessionEquipmentIssueByIdAsync(issueId, cancellationToken);
            if (issue is null || issue.IssueType != EquipmentIssueType.Rental || issue.ReturnedAtUtc is not null)
            {
                continue;
            }

            var damagedQty = ResolveDamagedQuantity(damagedLines, issue.SessionId, issue.EquipmentItemId, issue.Quantity);
            await ReturnRentalIssueAsync(issue, staffId, damagedQty, cancellationToken);
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
                    var damagedQty = ResolveDamagedQuantity(damagedLines, issue.SessionId, issue.EquipmentItemId, issue.Quantity);
                    await ReturnRentalIssueAsync(issue, staffId, damagedQty, cancellationToken);
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
    /// Köhnə avtomatik zolaq təyini — artıq istifadə olunmur; VIP üçün zolağı resepsiya seçməlidir.
    /// </summary>
    [HttpPost("full-package/assign")]
    public IActionResult AssignFullPackage([FromBody] FullPackageAssignRequest request)
    {
        _ = request;
        return BadRequest(new { error = "Zolağı resepsiya əl ilə seçməlidir. «Zolağa yaz» ilə sessiya yaradın." });
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
                IssueType = x.IssueType,
                Quantity = x.Quantity > 0 ? x.Quantity : 1
            })
            .ToList();
    }

    private static int ResolveDamagedQuantity(
        IReadOnlyList<ReturnEquipmentDamagedLine> damagedLines,
        Guid sessionId,
        Guid equipmentItemId,
        int issuedQuantity)
    {
        var maxQty = Math.Max(1, issuedQuantity);
        var damaged = damagedLines
            .Where(x => x.SessionId == sessionId && x.EquipmentItemId == equipmentItemId)
            .Sum(x => x.Quantity);
        return Math.Clamp(damaged, 0, maxQty);
    }

    private async Task ReturnRentalIssueAsync(
        SessionEquipmentIssue issue,
        Guid? returnedByStaffId,
        int damagedQuantity,
        CancellationToken cancellationToken)
    {
        if (issue.IssueType != EquipmentIssueType.Rental || issue.ReturnedAtUtc is not null)
        {
            return;
        }

        var qty = Math.Max(1, issue.Quantity);
        var damaged = Math.Clamp(damagedQuantity, 0, qty);
        var good = qty - damaged;

        var item = await repository.GetEquipmentItemByIdAsync(issue.EquipmentItemId, cancellationToken);
        if (item is not null)
        {
            if (good > 0)
            {
                EquipmentIssuanceRules.ApplyStockOnReturn(item, EquipmentIssueType.Rental, good);
            }

            if (damaged > 0)
            {
                EquipmentIssuanceRules.ApplyDamagedOnReturn(item, damaged);
            }

            await repository.UpdateEquipmentItemAsync(item, cancellationToken);
        }

        issue.ReturnedAtUtc = DateTime.UtcNow;
        issue.ReturnedByStaffId = returnedByStaffId;
        await repository.UpdateSessionEquipmentIssueAsync(issue, cancellationToken);
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

    [HttpPost("{id:guid}/activate")]
    public async Task<IActionResult> Activate([FromRoute] Guid id, [FromBody] ActivateSessionRequest request, CancellationToken cancellationToken)
    {
        if (ReceptionPermissionGate.DenyUnless(this, ReceptionStaffClaims.CanManageSessions) is { } denied)
        {
            return denied;
        }

        try
        {
            var laneNumber = await mediator.Send(new ActivateSessionCommand(id, request.LaneNumber), cancellationToken);
            return Ok(new { sessionId = id, laneNumber });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("tutulub", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("üst-üstə", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("rezerv", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("doludur", StringComparison.OrdinalIgnoreCase))
            {
                var session = await repository.GetSessionByIdAsync(id, cancellationToken);
                if (session is null)
                {
                    return Conflict(new { error = ex.Message });
                }

                var lanes = await repository.GetLanesAsync(cancellationToken);
                var allSessions = await repository.GetSessionsLightAsync(cancellationToken);
                var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
                var athletes = await repository.GetAthletesAsync(cancellationToken);
                var athlete = athletes.FirstOrDefault(a => a.Id == session.AthleteId);

                var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
                var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
                var duration = plannedEnd > plannedStart ? plannedEnd - plannedStart : TimeSpan.Zero;
                var nowUtc = DateTime.UtcNow;
                var reqStart = nowUtc;
                var reqEnd = duration > TimeSpan.Zero ? nowUtc.Add(duration) : nowUtc;

                static bool IsShortLane(int n) => n is >= 1 and <= 8;
                static bool IsLongLane(int n) => n is >= 9 and <= 11;

                var plannedLaneNumber = lanes.FirstOrDefault(l => l.Id == session.LaneId)?.Number ?? 0;
                var preferLong = plannedLaneNumber > 0 && IsLongLane(plannedLaneNumber);

                var allowed = lanes
                    .Where(l => !GymLaneRules.IsGymLane(l.Number))
                    .Where(l =>
                    {
                        if (athlete?.Category == CustomerCategory.Amateur) return IsShortLane(l.Number);
                        if (preferLong) return IsLongLane(l.Number);
                        return true;
                    })
                    .OrderBy(l => l.Number)
                    .ToList();

                var available = allowed
                    .Where(l =>
                    {
                        if (duration > TimeSpan.Zero
                            && LaneReservationRules.HasSubscriberConflictOnLane(schedules, l.Number, reqStart, reqEnd))
                        {
                            return false;
                        }

                        return allSessions
                            .Where(s => s.Id != session.Id && s.LaneId == l.Id)
                            .All(s => !LaneReservationRules.OverlapsSession(s, reqStart, reqEnd, nowUtc));
                    })
                    .Select(l => l.Number)
                    .ToList();

                return Conflict(new { error = ex.Message, availableLaneNumbers = available });
            }
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (ReceptionPermissionGate.DenyUnless(this, ReceptionStaffClaims.CanManageSessions) is { } denied)
        {
            return denied;
        }

        var session = await repository.GetSessionByIdAsync(id, cancellationToken);
        if (session is null)
        {
            return NotFound(new { error = "Sessiya tapılmadı." });
        }

        if (session.Status == SessionStatus.Completed)
        {
            return Ok(new { sessionId = id });
        }

        session.Status = SessionStatus.Completed;
        session.ActivatedAtUtc = null;
        session.EndTimeUtc = session.StartTimeUtc;
        await repository.UpdateSessionAsync(session, cancellationToken);

        var laneNumber = (await repository.GetLanesAsync(cancellationToken)).FirstOrDefault(l => l.Id == session.LaneId)?.Number ?? 0;
        if (laneNumber > 0)
        {
            await notifier.PublishLaneUpdateAsync(laneNumber, cancellationToken);
        }

        return Ok(new { sessionId = id });
    }
}

public sealed class ActivateSessionRequest
{
    public int LaneNumber { get; set; } = 0;
}
