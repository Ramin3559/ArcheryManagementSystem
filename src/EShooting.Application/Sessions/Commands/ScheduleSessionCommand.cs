using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Application.Equipment;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Sessions.Commands;

public sealed record ScheduleSessionCommand(
    Guid AthleteId,
    int LaneNumber,
    DateTime StartTimeUtc,
    int DurationMinutes,
    bool IsEquipmentIssued,
    PreferredLaneType PreferredLaneType,
    IReadOnlyList<SessionEquipmentIssueRequest>? EquipmentIssues = null,
    Guid? IssuedByStaffId = null,
    bool ForceOpenEnded = false) : IRequest<Guid>;

public sealed class ScheduleSessionCommandHandler(
    ITrainingCenterRepository repository,
    IRealtimeNotifier notifier) : IRequestHandler<ScheduleSessionCommand, Guid>
{
    public async Task<Guid> Handle(ScheduleSessionCommand request, CancellationToken cancellationToken)
    {
        if (request.DurationMinutes < 0)
        {
            throw new InvalidOperationException("Müddət mənfi ola bilməz.");
        }

        var startTimeUtc = AzerbaijanTime.NormalizeScheduleInputToUtc(request.StartTimeUtc);
        var nowUtc = DateTime.UtcNow;

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var athlete = athletes.FirstOrDefault(x => x.Id == request.AthleteId)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");

        var allSessions = await repository.GetSessionsLightAsync(cancellationToken);
        var subscriptionSchedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);

        var athleteActiveSession = allSessions
            .FirstOrDefault(s => s.AthleteId == request.AthleteId
                && SessionHousekeeping.IsAthleteSessionCurrentlyActive(s, nowUtc));
        if (athleteActiveSession is not null)
        {
            var activeLaneNumber = lanes.FirstOrDefault(l => l.Id == athleteActiveSession.LaneId)?.Number ?? 0;
            throw new InvalidOperationException(activeLaneNumber > 0
                ? $"Bu şəxs hazırda Zolaq {activeLaneNumber}-də aktivdir. Aktivlik dayandırıldıqdan sonra yeni xətdə yazıla bilər."
                : "Bu şəxs hazırda başqa bir zolaqda aktivdir. Aktivlik dayandırıldıqdan sonra yeni xətdə yazıla bilər.");
        }

        var isOpenEnded = request.ForceOpenEnded
            || request.DurationMinutes == 0
            || WalkInSubscriptionRules.HasActiveWalkIn(subscriptionSchedules, athlete.Id, DateTime.Now);
        var requestedEndTimeUtc = isOpenEnded
            ? startTimeUtc
            : startTimeUtc.AddMinutes(request.DurationMinutes);

        static bool IsShortLane(int number) => number is >= 1 and <= 8;
        static bool IsLongLane(int number) => number is >= 9 and <= 11;

        // Category rules: Amateur can only use short lanes.
        if (athlete.Category == CustomerCategory.Amateur && !GymLaneRules.IsGymLane(request.LaneNumber))
        {
            if (request.LaneNumber > 0 && !IsShortLane(request.LaneNumber))
            {
                throw new InvalidOperationException("Həvəskar yalnız 1-8 zolaqlarda ola bilər.");
            }

            if (request.LaneNumber == 0 && request.PreferredLaneType == PreferredLaneType.Long)
            {
                throw new InvalidOperationException("Həvəskar üçün yalnız qısa xətlər (1-8) mümkündür.");
            }
        }

        // Pick lane: manual (LaneNumber>0) or auto (LaneNumber==0).
        Lane? lane;
        if (request.LaneNumber > 0)
        {
            lane = lanes.FirstOrDefault(l => l.Number == request.LaneNumber);
            if (lane is null)
            {
                throw new InvalidOperationException($"{request.LaneNumber} nömrəli zolaq mövcud deyil.");
            }
        }
        else
        {
            var preferred = request.PreferredLaneType;
            if (athlete.Category == CustomerCategory.Amateur)
            {
                preferred = PreferredLaneType.Short;
            }

            var candidates = preferred switch
            {
                PreferredLaneType.Short => lanes.Where(l => IsShortLane(l.Number)).ToList(),
                PreferredLaneType.Long => lanes.Where(l => IsLongLane(l.Number)).ToList(),
                _ => lanes.Where(l => !GymLaneRules.IsGymLane(l.Number)).ToList()
            };

            // Ensure global manual capacity keeps subscriber slots free.
            if (!isOpenEnded && request.DurationMinutes > 0
                && !LaneReservationRules.HasManualCapacityForSlot(
                    lanes,
                    allSessions,
                    subscriptionSchedules,
                    startTimeUtc,
                    requestedEndTimeUtc,
                    nowUtc))
            {
                throw new InvalidOperationException(
                    "Bu vaxt üçün zolaq təyin edilə bilməz. Abunəçilər üçün rezerv olunmuş boş yerlər saxlanılmalıdır.");
            }

            lane = candidates
                .OrderBy(x => x.Number)
                .FirstOrDefault(l =>
                {
                    // Avoid lanes reserved by subscription schedule on that time.
                    if (!isOpenEnded && request.DurationMinutes > 0
                        && LaneReservationRules.HasSubscriberConflictOnLane(
                            subscriptionSchedules,
                            l.Number,
                            startTimeUtc,
                            requestedEndTimeUtc))
                    {
                        return false;
                    }

                    return allSessions
                        .Where(s => s.LaneId == l.Id)
                        .All(s => !LaneReservationRules.OverlapsSession(s, startTimeUtc, requestedEndTimeUtc, nowUtc));
                });

            if (lane is null)
            {
                var label = preferred switch
                {
                    PreferredLaneType.Long => "uzun (9-11)",
                    PreferredLaneType.Short => "qısa (1-8)",
                    _ => "uyğun"
                };
                throw new InvalidOperationException($"Təəssüf ki, seçdiyiniz vaxtda bütün {label} xətlər doludur. Zəhmət olmasa başqa vaxt seçin.");
            }
        }

        var isGymLane = GymLaneRules.IsGymLane(lane.Number);

        if (!isGymLane)
        {
            foreach (var stale in allSessions.Where(x => x.LaneId == lane.Id).ToList())
            {
                if (!SessionHousekeeping.ShouldAutoComplete(stale, nowUtc))
                {
                    continue;
                }

                SessionHousekeeping.MarkCompleted(stale, nowUtc);
                await repository.UpdateSessionAsync(stale, cancellationToken);
            }

            var existingLaneSessions = allSessions
                .Where(x => x.LaneId == lane.Id && x.Status != SessionStatus.Completed);

            var hasOverlap = existingLaneSessions.Any(x =>
                LaneReservationRules.OverlapsSession(x, startTimeUtc, requestedEndTimeUtc, nowUtc));

            if (hasOverlap)
            {
                var conflict = existingLaneSessions
                    .Select(s => new { Session = s, Start = DateTimeAssumedUtc.AsUtc(s.StartTimeUtc), End = DateTimeAssumedUtc.AsUtc(s.EndTimeUtc) })
                    .FirstOrDefault(x => LaneReservationRules.OverlapsSession(x.Session, startTimeUtc, requestedEndTimeUtc, nowUtc));

                var who = conflict is null
                    ? "başqa müştəri"
                    : (athletes.FirstOrDefault(a => a.Id == conflict.Session.AthleteId)?.FullName ?? "başqa müştəri");
                var untilLocal = conflict is null ? "" : conflict.End.ToLocalTime().ToString("HH:mm");
                var tail = string.IsNullOrWhiteSpace(untilLocal) ? "" : $" ({who} tərəfindən saat {untilLocal}-a qədər)";
                throw new InvalidOperationException($"Bu zolaq seçdiyiniz zaman aralığında tutulub{tail}.");
            }

            if (!isOpenEnded)
            {
                if (!LaneReservationRules.HasManualCapacityForSlot(
                        lanes,
                        allSessions,
                        subscriptionSchedules,
                        startTimeUtc,
                        requestedEndTimeUtc,
                        nowUtc))
                {
                    throw new InvalidOperationException(
                        "Bu vaxt üçün zolaq təyin edilə bilməz. Abunəçilər üçün rezerv olunmuş boş yerlər saxlanılmalıdır.");
                }

                if (LaneReservationRules.HasSubscriberConflictOnLane(
                        subscriptionSchedules,
                        lane.Number,
                        startTimeUtc,
                        requestedEndTimeUtc))
                {
                    throw new InvalidOperationException(
                        $"{lane.Number} nömrəli zolaq həmin vaxt aralığında abunə rezervasiyası ilə üst-üstə düşür.");
                }
            }
        }

        var equipmentIssues = request.EquipmentIssues ?? [];
        var hasRentalEquipment = equipmentIssues.Any(x => x.IssueType == EquipmentIssueType.Rental);
        var legacyEquipmentFlag = request.IsEquipmentIssued && equipmentIssues.Count == 0;

        var session = new TrainingSession
        {
            AthleteId = request.AthleteId,
            LaneId = lane.Id,
            StartTimeUtc = startTimeUtc,
            EndTimeUtc = requestedEndTimeUtc,
            Status = startTimeUtc <= nowUtc ? SessionStatus.Active : SessionStatus.Scheduled,
            IsEquipmentIssued = hasRentalEquipment || legacyEquipmentFlag,
            EquipmentReturnedAtUtc = null
        };

        var created = await repository.AddSessionAsync(session, cancellationToken);

        if (equipmentIssues.Count > 0)
        {
            var issueRows = new List<SessionEquipmentIssue>();
            foreach (var issue in equipmentIssues)
            {
                var catalogItem = await repository.GetEquipmentItemByIdAsync(issue.EquipmentItemId, cancellationToken)
                    ?? throw new InvalidOperationException("Seçilmiş avadanlıq tapılmadı.");
                if (!catalogItem.IsActive || catalogItem.IsDeleted)
                {
                    throw new InvalidOperationException($"«{catalogItem.Name}» deaktivdir və verilə bilməz.");
                }

                EquipmentIssuanceRules.ValidateIssueType(catalogItem, issue.IssueType);
                var quantity = issue.Quantity > 0 ? issue.Quantity : 1;
                EquipmentIssuanceRules.ApplyStockOnIssue(catalogItem, issue.IssueType, quantity);
                await repository.UpdateEquipmentItemAsync(catalogItem, cancellationToken);

                issueRows.Add(new SessionEquipmentIssue
                {
                    SessionId = created.Id,
                    EquipmentItemId = catalogItem.Id,
                    IssueType = issue.IssueType,
                    Quantity = quantity,
                    UnitPrice = EquipmentIssuanceRules.ResolveUnitPrice(catalogItem, issue.IssueType),
                    IssuedByStaffId = request.IssuedByStaffId,
                    ReturnedAtUtc = null
                });
            }

            await repository.AddSessionEquipmentIssuesAsync(issueRows, cancellationToken);
        }

        await notifier.PublishLaneUpdateAsync(lane.Number, cancellationToken);
        return created.Id;
    }
}
