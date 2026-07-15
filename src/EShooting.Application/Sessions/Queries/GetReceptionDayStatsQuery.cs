using EShooting.Application.Common;
using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Enums;
using MediatR;

namespace EShooting.Application.Sessions.Queries;

public sealed record ReceptionDayStatsDto(
    int IncomingCustomersToday,
    int ActiveSessions,
    int ScheduledSessionsToday,
    int CompletedSessionsToday);

public sealed record GetReceptionDayStatsQuery : IRequest<ReceptionDayStatsDto>;

public sealed class GetReceptionDayStatsQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetReceptionDayStatsQuery, ReceptionDayStatsDto>
{
    public async Task<ReceptionDayStatsDto> Handle(
        GetReceptionDayStatsQuery request,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;
        var todayLocal = AzerbaijanTime.TodayLocal;

        await SubscriptionPlannedSessionSync.EnsureForLocalDateAsync(repository, todayLocal, cancellationToken);

        var sessions = await repository.GetSessionsLightAsync(cancellationToken);
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var athleteById = athletes.ToDictionary(x => x.Id);

        static DateTime StartLocalDate(EShooting.Domain.Entities.TrainingSession s) =>
            AzerbaijanTime.UtcToLocalDate(DateTimeAssumedUtc.AsUtc(s.StartTimeUtc));

        var todaySessions = sessions
            .Where(s => StartLocalDate(s) == todayLocal)
            .Where(s =>
            {
                if (!athleteById.TryGetValue(s.AthleteId, out var a))
                {
                    return true;
                }

                return !a.IsGroupPlaceholder;
            })
            .ToList();

        var incomingCustomers = todaySessions
            .Select(s => s.AthleteId)
            .Distinct()
            .Count();

        var active = todaySessions.Count(s => SessionHousekeeping.IsAthleteSessionCurrentlyActive(s, nowUtc));
        var completed = todaySessions.Count(s => s.Status == SessionStatus.Completed);
        var scheduled = todaySessions.Count(s =>
            s.Status != SessionStatus.Completed
            && !SessionHousekeeping.IsAthleteSessionCurrentlyActive(s, nowUtc));

        return new ReceptionDayStatsDto(incomingCustomers, active, scheduled, completed);
    }
}
