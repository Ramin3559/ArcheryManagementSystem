using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Application.Sessions;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Sessions.Queries;

public sealed record GetLaneDashboardQuery : IRequest<IReadOnlyCollection<LaneDashboardItem>>;

public sealed class GetLaneDashboardQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetLaneDashboardQuery, IReadOnlyCollection<LaneDashboardItem>>
{
    private static bool HasActivation(EShooting.Domain.Entities.TrainingSession session)
    {
        return session.ActivatedAtUtc is not null || session.Status == SessionStatus.Active;
    }

    private static DateTime ResolveEffectiveStartUtc(EShooting.Domain.Entities.TrainingSession session)
    {
        return session.ActivatedAtUtc is DateTime activated
            ? DateTimeAssumedUtc.AsUtc(activated)
            : DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
    }

    private static DateTime ResolveEffectiveEndUtc(EShooting.Domain.Entities.TrainingSession session)
    {
        var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        var plannedDuration = plannedEnd > plannedStart ? plannedEnd - plannedStart : TimeSpan.Zero;
        var start = ResolveEffectiveStartUtc(session);
        return plannedDuration > TimeSpan.Zero ? start + plannedDuration : start;
    }

    public async Task<IReadOnlyCollection<LaneDashboardItem>> Handle(
        GetLaneDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        // Yalnız bu günün (yerli vaxtla) planlı sessiyalarını göstəririk.
        // Sabahkı və ya gələcək günün planları "Planlaşdırılıb" kimi görünməyəcək.
        var localNow = nowUtc.ToLocalTime();

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var equipmentIssues = await repository.GetSessionEquipmentIssuesAsync(cancellationToken);
        foreach (var stale in sessions.Where(x => SessionHousekeeping.ShouldAutoComplete(x, nowUtc)).ToList())
        {
            if (SessionEquipmentRules.HasPendingRentalEquipment(stale, equipmentIssues))
            {
                continue;
            }

            SessionHousekeeping.MarkCompleted(stale, nowUtc);
            await repository.UpdateSessionAsync(stale, cancellationToken);
        }

        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var subscriptionSchedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        var equipmentItems = await repository.GetEquipmentItemsAsync(activeOnly: false, cancellationToken);
        var equipmentNames = equipmentItems.ToDictionary(x => x.Id, x => x.Name);
        var athleteNameById = athletes.ToDictionary(x => x.Id, x => x.FullName ?? "—");
        var athleteById = athletes.ToDictionary(
            x => x.Id,
            x => new { x.FullName, x.FirstName, x.LastName, x.MembershipType, x.IsVip });

        var result = lanes
            .Where(l => !GymLaneRules.IsGymLane(l.Number))
            .OrderBy(x => x.Number)
            .Select(lane =>
            {
                var laneAllSessions = sessions
                    .Where(x => x.LaneId == lane.Id)
                    .OrderByDescending(x => x.StartTimeUtc)
                    .ToList();

                var laneSessions = laneAllSessions
                    .Where(x => x.Status != SessionStatus.Completed)
                    .Where(x => IsRelevantForLaneDisplay(x, nowUtc, localNow))
                    .OrderByDescending(x => x.StartTimeUtc)
                    .ToList();

                // 1) Hazırda canlı pəncərədə olan sessiya.
                // 2) Yoxdursa: ən yaxın gələcək planlı sessiya (bugün və ya sonrakı günlər).
                // 3) Yoxdursa: bu günün gecikmiş, hələ bağlanmamış sessiyası.
                var activeSession = laneSessions
                        .Where(x => IsInLiveWindow(x, nowUtc))
                        .OrderByDescending(x => DateTimeAssumedUtc.AsUtc(x.StartTimeUtc))
                        .FirstOrDefault()
                    ?? laneSessions
                        .Where(x =>
                        {
                            var startUtc = DateTimeAssumedUtc.AsUtc(x.StartTimeUtc);
                            return startUtc > nowUtc;
                        })
                        .OrderBy(x => DateTimeAssumedUtc.AsUtc(x.StartTimeUtc))
                        .FirstOrDefault()
                    ?? laneSessions
                        .Where(x => SessionHousekeeping.IsDisplayableOverdueSession(x, nowUtc))
                        .OrderByDescending(x => DateTimeAssumedUtc.AsUtc(x.EndTimeUtc))
                        .FirstOrDefault();

                var athlete = activeSession is null
                    ? null
                    : athleteById.GetValueOrDefault(activeSession.AthleteId);
                var athleteName = athlete?.FullName;
                var athleteFirstName = athlete?.FirstName;
                var athleteLastName = athlete?.LastName;
                var membershipType = athlete?.MembershipType;
                var queueAthleteNames = laneSessions
                    .OrderBy(x => DateTimeAssumedUtc.AsUtc(x.StartTimeUtc))
                    .Select(x => athleteById.GetValueOrDefault(x.AthleteId))
                    .Where(x => !string.IsNullOrWhiteSpace(x?.FullName))
                    .Select(x => x!.FullName)
                    .ToList();
                var warning = BuildWarning(activeSession, nowUtc);
                var status = ResolveStatus(activeSession, nowUtc);
                DateTime? startTimeUtc = activeSession is null
                    ? null
                    : (HasActivation(activeSession)
                        ? ResolveEffectiveStartUtc(activeSession)
                        : DateTimeAssumedUtc.AsUtc(activeSession.StartTimeUtc));
                DateTime? endTimeUtc = activeSession is null
                    ? null
                    : (HasActivation(activeSession)
                        ? ResolveEffectiveEndUtc(activeSession)
                        : DateTimeAssumedUtc.AsUtc(activeSession.EndTimeUtc));

                var isOpenEndedSession = activeSession is not null
                    && startTimeUtc is not null
                    && endTimeUtc is not null
                    && (!HasValidTimeWindow(startTimeUtc.Value, endTimeUtc.Value)
                        || WalkInSubscriptionRules.HasActiveWalkIn(
                            subscriptionSchedules,
                            activeSession.AthleteId,
                            localNow));
                var isAthleteVip = (athlete?.IsVip ?? false)
                    || (activeSession is not null
                        && WalkInSubscriptionRules.HasActiveWalkIn(
                            subscriptionSchedules,
                            activeSession.AthleteId,
                            localNow));

                if (isOpenEndedSession
                    && startTimeUtc is not null
                    && endTimeUtc is not null
                    && endTimeUtc.Value > startTimeUtc.Value)
                {
                    endTimeUtc = startTimeUtc;
                }

                DateTime? cooldownUntilUtc = null;
                if (status == "Idle")
                {
                    var lastEndedUtc = laneAllSessions
                        .Select(x => DateTimeAssumedUtc.AsUtc(x.EndTimeUtc))
                        .Where(end => end > DateTime.MinValue && end <= nowUtc)
                        .OrderByDescending(end => end)
                        .FirstOrDefault();

                    if (lastEndedUtc > DateTime.MinValue)
                    {
                        cooldownUntilUtc = lastEndedUtc + LaneReservationRules.SessionBuffer;
                    }
                }

                var pendingRental = SessionEquipmentRules.ResolveLanePendingRental(
                    laneAllSessions,
                    equipmentIssues,
                    equipmentNames,
                    athleteNameById,
                    nowUtc);

                return new LaneDashboardItem
                {
                    SessionId = activeSession?.Id,
                    ScoreCount = activeSession?.Scores.Count ?? 0,
                    LaneNumber = lane.Number,
                    LaneType = lane.LaneType,
                    AthleteName = athleteName,
                    AthleteFirstName = athleteFirstName,
                    AthleteLastName = athleteLastName,
                    AthleteMembershipType = membershipType,
                    QueueAthleteNames = queueAthleteNames,
                    StartTimeUtc = startTimeUtc,
                    EndTimeUtc = endTimeUtc,
                    CooldownUntilUtc = cooldownUntilUtc,
                    TotalScore = activeSession?.TotalScore ?? 0,
                    Status = status,
                    Warning = warning,
                    IsEquipmentIssued = activeSession?.IsEquipmentIssued ?? false,
                    IsEquipmentReturned = activeSession?.EquipmentReturnedAtUtc is not null,
                    HasPendingRentalEquipment = pendingRental is not null,
                    PendingRentalSessionId = pendingRental?.SessionId,
                    PendingRentalAthleteName = pendingRental?.AthleteName,
                    PendingRentalEquipmentSummary = pendingRental is null
                        ? null
                        : string.Join(", ", pendingRental.EquipmentLabels),
                    IsSessionOpen = activeSession?.Status != SessionStatus.Completed,
                    IsOpenEndedSession = isOpenEndedSession,
                    IsAthleteVip = isAthleteVip
                };
            })
            .ToList();

        return result;
    }

    private static bool IsRelevantForLaneDisplay(
        EShooting.Domain.Entities.TrainingSession session,
        DateTime nowUtc,
        DateTime localNow)
    {
        var start = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        if (start.ToLocalTime().Date >= localNow.Date)
        {
            return true;
        }

        if (IsInLiveWindow(session, nowUtc))
        {
            return true;
        }

        return SessionHousekeeping.IsDisplayableOverdueSession(session, nowUtc);
    }

    private static bool IsOverdueOpenSession(EShooting.Domain.Entities.TrainingSession session, DateTime nowUtc)
    {
        var start = ResolveEffectiveStartUtc(session);
        var end = ResolveEffectiveEndUtc(session);
        if (!HasValidTimeWindow(start, end))
        {
            return session.Status == SessionStatus.Active && nowUtc >= start;
        }

        return nowUtc >= end;
    }

    private static bool IsInLiveWindow(EShooting.Domain.Entities.TrainingSession session, DateTime nowUtc)
    {
        if (!HasActivation(session))
        {
            return false;
        }

        var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        var start = ResolveEffectiveStartUtc(session);
        var end = ResolveEffectiveEndUtc(session);
        if (!HasValidTimeWindow(plannedStart, plannedEnd))
        {
            if (session.Status == SessionStatus.Completed)
            {
                return false;
            }

            if (nowUtc < start)
            {
                return false;
            }

            // Köhnə günlərin açıq VIP sessiyalarını TV-də aktiv sayma.
            if (start.ToLocalTime().Date < nowUtc.ToLocalTime().Date)
            {
                return false;
            }

            return true;
        }

        return nowUtc >= start && nowUtc < end;
    }

    private static string BuildWarning(EShooting.Domain.Entities.TrainingSession? session, DateTime nowUtc)
    {
        if (session is null)
        {
            return "Ready";
        }

        var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);

        if (!HasActivation(session))
        {
            if (nowUtc < plannedStart)
            {
                return $"Starts in {FormatDuration(plannedStart - nowUtc)}";
            }
            return "Waiting";
        }

        var start = ResolveEffectiveStartUtc(session);
        var end = ResolveEffectiveEndUtc(session);
        if (!HasValidTimeWindow(plannedStart, plannedEnd))
        {
            if (session.Status == SessionStatus.Completed)
            {
                return "Time is over";
            }

            if (nowUtc < start)
            {
                return $"Starts in {FormatDuration(start - nowUtc)}";
            }

            return "In progress";
        }

        if (nowUtc < start)
        {
            return $"Starts in {FormatDuration(start - nowUtc)}";
        }

        var remaining = end - nowUtc;
        if (remaining <= TimeSpan.Zero)
        {
            return "Time is over";
        }

        if (remaining <= TimeSpan.FromMinutes(1))
        {
            return "1 minute remaining";
        }

        if (remaining <= TimeSpan.FromMinutes(5))
        {
            return "5 minutes remaining";
        }

        return "In progress";
    }

    private static string ResolveStatus(EShooting.Domain.Entities.TrainingSession? session, DateTime nowUtc)
    {
        if (session is null)
        {
            return "Idle";
        }

        if (session.Status == SessionStatus.Completed)
        {
            return "Completed";
        }

        var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);

        if (!HasActivation(session))
        {
            return "Scheduled";
        }

        var start = ResolveEffectiveStartUtc(session);
        var end = ResolveEffectiveEndUtc(session);
        if (!HasValidTimeWindow(plannedStart, plannedEnd))
        {
            if (nowUtc < start)
            {
                return "Scheduled";
            }

            return "Active";
        }

        // Vaxt bitibsə və ya DB-də tamamlanıbsa — heç vaxt "Active" qalmamalıdır.
        if (nowUtc >= end)
        {
            return "Completed";
        }

        if (nowUtc < start)
        {
            return "Scheduled";
        }

        // Buraya qədər: nowUtc < end (əks halda yuxarıda "Completed"), start <= nowUtc  =>  aktiv interval
        return "Active";
    }

    private static bool HasValidTimeWindow(DateTime startUtc, DateTime endUtc)
    {
        return endUtc > startUtc;
    }

    private static string FormatDuration(TimeSpan span)
    {
        if (span <= TimeSpan.Zero)
        {
            return "00:00";
        }

        var totalSeconds = (int)Math.Floor(span.TotalSeconds);
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;

        if (hours > 0)
        {
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        }

        return $"{minutes:D2}:{seconds:D2}";
    }
}
