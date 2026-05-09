using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Sessions.Queries;

public sealed record GetLaneDashboardQuery : IRequest<IReadOnlyCollection<LaneDashboardItem>>;

public sealed class GetLaneDashboardQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetLaneDashboardQuery, IReadOnlyCollection<LaneDashboardItem>>
{
    public async Task<IReadOnlyCollection<LaneDashboardItem>> Handle(
        GetLaneDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        // Yalnız bu günün (yerli vaxtla) planlı sessiyalarını göstəririk.
        // Sabahkı və ya gələcək günün planları "Planlaşdırılıb" kimi görünməyəcək.
        var localNow = nowUtc.ToLocalTime();
        var localTomorrowMidnight = localNow.Date.AddDays(1);
        var endOfTodayUtc = localTomorrowMidnight.ToUniversalTime();

        var lanes = await repository.GetLanesAsync(cancellationToken);
        var sessions = await repository.GetSessionsAsync(cancellationToken);
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var athleteById = athletes.ToDictionary(x => x.Id, x => new { x.FullName, x.MembershipType });

        var result = lanes
            .OrderBy(x => x.Number)
            .Select(lane =>
            {
                var laneAllSessions = sessions
                    .Where(x => x.LaneId == lane.Id)
                    .OrderByDescending(x => x.StartTimeUtc)
                    .ToList();

                var laneSessions = laneAllSessions
                    .Where(x => x.Status != SessionStatus.Completed)
                    .OrderByDescending(x => x.StartTimeUtc)
                    .ToList();

                // 1) Hazırda canlı pəncərədə olan sessiya.
                // 2) Yoxdursa: yalnız BUGÜNÜN gələcək saatları üçün ən tez planlı sessiya.
                //    Sabah üçün və ya keçmişdə qalmış planlı sessiyalar Planlı kimi göstərilməyəcək —
                //    zolaq Boş olaraq qalacaq.
                var activeSession = laneSessions.FirstOrDefault(x => IsInLiveWindow(x, nowUtc))
                    ?? laneSessions
                        .Where(x =>
                        {
                            var startUtc = DateTimeAssumedUtc.AsUtc(x.StartTimeUtc);
                            return startUtc > nowUtc && startUtc < endOfTodayUtc;
                        })
                        .OrderBy(x => DateTimeAssumedUtc.AsUtc(x.StartTimeUtc))
                        .FirstOrDefault();

                var athleteName = activeSession is null
                    ? null
                    : athleteById.GetValueOrDefault(activeSession.AthleteId)?.FullName;
                var membershipType = activeSession is null
                    ? null
                    : athleteById.GetValueOrDefault(activeSession.AthleteId)?.MembershipType;
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
                    : DateTimeAssumedUtc.AsUtc(activeSession.StartTimeUtc);
                DateTime? endTimeUtc = activeSession is null
                    ? null
                    : DateTimeAssumedUtc.AsUtc(activeSession.EndTimeUtc);

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

                return new LaneDashboardItem
                {
                    SessionId = activeSession?.Id,
                    ScoreCount = activeSession?.Scores.Count ?? 0,
                    LaneNumber = lane.Number,
                    LaneType = lane.LaneType,
                    AthleteName = athleteName,
                    AthleteMembershipType = membershipType,
                    QueueAthleteNames = queueAthleteNames,
                    StartTimeUtc = startTimeUtc,
                    EndTimeUtc = endTimeUtc,
                    CooldownUntilUtc = cooldownUntilUtc,
                    TotalScore = activeSession?.TotalScore ?? 0,
                    Status = status,
                    Warning = warning,
                    IsEquipmentIssued = activeSession?.IsEquipmentIssued ?? false,
                    IsEquipmentReturned = (activeSession?.EquipmentReturnedAtUtc is not null)
                };
            })
            .ToList();

        return result;
    }

    private static bool IsInLiveWindow(EShooting.Domain.Entities.TrainingSession session, DateTime nowUtc)
    {
        var start = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var end = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        if (!HasValidTimeWindow(start, end))
        {
            return session.Status == SessionStatus.Active;
        }

        return nowUtc >= start && nowUtc < end;
    }

    private static string BuildWarning(EShooting.Domain.Entities.TrainingSession? session, DateTime nowUtc)
    {
        if (session is null)
        {
            return "Ready";
        }

        var start = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var end = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        if (!HasValidTimeWindow(start, end))
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

        var start = DateTimeAssumedUtc.AsUtc(session.StartTimeUtc);
        var end = DateTimeAssumedUtc.AsUtc(session.EndTimeUtc);
        if (!HasValidTimeWindow(start, end))
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
