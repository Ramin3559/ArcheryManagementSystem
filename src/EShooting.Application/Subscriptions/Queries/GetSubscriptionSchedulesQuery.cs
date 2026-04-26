using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using MediatR;

namespace EShooting.Application.Subscriptions.Queries;

public sealed record GetSubscriptionSchedulesQuery(Guid? AthleteId = null) : IRequest<IReadOnlyCollection<SubscriptionScheduleItem>>;

public sealed class GetSubscriptionSchedulesQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetSubscriptionSchedulesQuery, IReadOnlyCollection<SubscriptionScheduleItem>>
{
    public async Task<IReadOnlyCollection<SubscriptionScheduleItem>> Handle(
        GetSubscriptionSchedulesQuery request,
        CancellationToken cancellationToken)
    {
        var athletes = await repository.GetAthletesAsync(cancellationToken);
        var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);

        var filtered = schedules
            .Where(x => request.AthleteId is null || x.AthleteId == request.AthleteId.Value)
            .OrderBy(x => x.DayOfWeek)
            .ThenBy(x => x.StartTimeLocal)
            .Select(x => new SubscriptionScheduleItem
            {
                Id = x.Id,
                AthleteId = x.AthleteId,
                AthleteName = athletes.FirstOrDefault(a => a.Id == x.AthleteId)?.FullName ?? "Unknown",
                DayOfWeek = x.DayOfWeek,
                StartTimeLocal = x.StartTimeLocal,
                DurationMinutes = x.DurationMinutes,
                ActiveFromDateLocal = x.ActiveFromDateLocal,
                ActiveToDateLocal = x.ActiveToDateLocal,
                IsEnabled = x.IsEnabled,
                LastAssignedLaneNumber = x.LastAssignedLaneNumber,
                LastAutoStartedAtUtc = x.LastAutoStartedAtUtc
            })
            .ToList();

        return filtered;
    }
}
