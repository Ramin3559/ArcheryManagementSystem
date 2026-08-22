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
        => SessionActivationRules.HasActivation(session);

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

        // Yalnız bu günün (Bakı vaxtı) planlı sessiyalarını göstəririk.
        // Sabahkı və ya gələcək günün planları "Planlaşdırılıb" kimi görünməyəcək.
        var localNow = AzerbaijanTime.NowLocal;

        // Abunə təqvimi var, TrainingSession yoxdursa — bu gün üçün yaradılır.
        // Sinxron xətası zolaq panelini boş qoymamalıdır (serverdə unique index 500 verirdi).
        try
        {
            await SubscriptionPlannedSessionSync.EnsureForLocalDateAsync(repository, localNow.Date, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Panel zolaqları yenə də yükləsin.
        }

        IReadOnlyCollection<EShooting.Domain.Entities.Lane> lanes;
        try
        {
            lanes = await repository.GetLanesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return [];
        }
        IReadOnlyCollection<EShooting.Domain.Entities.TrainingSession> sessions;
        try
        {
            sessions = await repository.GetSessionsLightAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            sessions = [];
        }

        IReadOnlyDictionary<Guid, SessionScoreTotals> scoreTotals;
        try
        {
            var openSessionIds = sessions
                .Where(x => x.Status != SessionStatus.Completed)
                .Select(x => x.Id)
                .Distinct()
                .ToList();
            scoreTotals = await repository.GetSessionScoreTotalsAsync(openSessionIds, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            scoreTotals = new Dictionary<Guid, SessionScoreTotals>();
        }

        IReadOnlyCollection<EShooting.Domain.Entities.SessionEquipmentIssue> equipmentIssues;
        try
        {
            equipmentIssues = await repository.GetSessionEquipmentIssuesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            equipmentIssues = [];
        }
        foreach (var stale in sessions.Where(x => SessionHousekeeping.ShouldAutoComplete(x, nowUtc)).ToList())
        {
            try
            {
                if (SessionEquipmentRules.HasPendingRentalEquipment(stale, equipmentIssues))
                {
                    continue;
                }

                var hadActivation = SessionActivationRules.HasActivation(stale);
                var athleteId = stale.AthleteId;
                var dayLocal = AzerbaijanTime.UtcToLocalDate(DateTimeAssumedUtc.AsUtc(stale.StartTimeUtc));
                SessionHousekeeping.MarkCompleted(stale, nowUtc);
                await repository.UpdateSessionAsync(stale, cancellationToken);

                if (hadActivation)
                {
                    await SubscriptionPlannedSessionConsume.CompleteLeftoverSameDayPlannedAsync(
                        repository,
                        sessions,
                        athleteId,
                        dayLocal,
                        excludeSessionId: stale.Id,
                        nowUtc,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Bir seansın bağlanması paneli dayandırmasın.
            }
        }

        IReadOnlyCollection<EShooting.Domain.Entities.Athlete> athletes;
        try
        {
            athletes = await repository.GetAthletesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            athletes = [];
        }

        IReadOnlyCollection<EShooting.Domain.Entities.SubscriptionSchedule> subscriptionSchedules;
        try
        {
            subscriptionSchedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            subscriptionSchedules = [];
        }

        IReadOnlyCollection<EShooting.Domain.Entities.EquipmentItem> equipmentItems;
        try
        {
            equipmentItems = await repository.GetEquipmentItemsAsync(activeOnly: false, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            equipmentItems = [];
        }
        var equipmentNames = equipmentItems
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().Name);
        var athleteNameById = athletes
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First().FullName ?? "—");
        var athleteById = athletes
            .GroupBy(x => x.Id)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var x = g.First();
                    return new { x.FullName, x.FirstName, x.LastName, x.MembershipType, x.IsVip };
                });

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
                    // Aylıq pool (zolaqsız) planlar zolaq kartında «Planlaşdırılıb» göstərilmir.
                    .Where(x => !IsUnassignedPoolScheduleSession(x, subscriptionSchedules, localNow.Date))
                    .OrderByDescending(x => x.StartTimeUtc)
                    .ToList();

                // 1) Hazırda canlı pəncərədə olan sessiya.
                // 2) Yoxdursa: bu günün ən yaxın gələcək planlı sessiyası.
                // 3) Yoxdursa: bu günün gecikmiş / aktivasiya gözləyən sessiyası.
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
                        .Where(x => IsPendingActivationWindow(x, nowUtc))
                        .OrderByDescending(x => DateTimeAssumedUtc.AsUtc(x.StartTimeUtc))
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

                var score = activeSession is not null
                    && scoreTotals.TryGetValue(activeSession.Id, out var sessionScore)
                    ? sessionScore
                    : default;
                var totalScore = Math.Max(0, score.Total);

                return new LaneDashboardItem
                {
                    SessionId = activeSession?.Id,
                    ScoreCount = score.Count,
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
                    TotalScore = totalScore,
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
                    IsSessionActivated = activeSession is not null && HasActivation(activeSession),
                    IsOpenEndedSession = isOpenEndedSession,
                    IsAthleteVip = isAthleteVip
                };
            })
            .ToList();

        return result;
    }

    private static bool IsUnassignedPoolScheduleSession(
        EShooting.Domain.Entities.TrainingSession session,
        IReadOnlyCollection<EShooting.Domain.Entities.SubscriptionSchedule> schedules,
        DateTime dayLocal)
    {
        if (HasActivation(session))
        {
            return false;
        }

        if (session.SubscriptionScheduleId is not Guid sid)
        {
            return false;
        }

        var schedule = schedules.FirstOrDefault(s => s.Id == sid);
        if (schedule is null)
        {
            return false;
        }

        return SubscriptionPoolCapacity.ResolveExplicitLaneNumber(schedule, dayLocal) <= 0;
    }

    private static bool IsPendingActivationWindow(EShooting.Domain.Entities.TrainingSession session, DateTime nowUtc)
    {
        if (HasActivation(session) || session.Status == SessionStatus.Completed)
        {
            return false;
        }

        var plannedStart = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var plannedEnd = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        if (!HasValidTimeWindow(plannedStart, plannedEnd))
        {
            return false;
        }

        return nowUtc >= plannedStart && nowUtc < plannedEnd;
    }

    private static bool IsRelevantForLaneDisplay(
        EShooting.Domain.Entities.TrainingSession session,
        DateTime nowUtc,
        DateTime localNow)
    {
        var start = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        // Gələcək günlərin planları zolaq kartında görünməsin — yalnız cari (Bakı) gün.
        if (AzerbaijanTime.UtcToLocalDate(start) == localNow.Date)
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
            if (AzerbaijanTime.UtcToLocalDate(start) < AzerbaijanTime.UtcToLocalDate(nowUtc))
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
