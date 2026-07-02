using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

public sealed record SetAthleteActiveCommand(Guid AthleteId, bool IsActive) : IRequest;

public sealed class SetAthleteActiveCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<SetAthleteActiveCommand>
{
    public async Task Handle(SetAthleteActiveCommand request, CancellationToken cancellationToken)
    {
        var athlete = await repository.GetAthleteByIdAsync(request.AthleteId, cancellationToken)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");

        if (!request.IsActive)
        {
            var schedules = await repository.GetSubscriptionSchedulesAsync(cancellationToken);
            foreach (var schedule in schedules.Where(x => x.AthleteId == athlete.Id && x.IsEnabled))
            {
                schedule.IsEnabled = false;
                await repository.UpdateSubscriptionScheduleAsync(schedule, cancellationToken);
            }

            athlete.IsSubscriber = false;
            athlete.IsFullPackage = false;
        }

        athlete.IsActive = request.IsActive;
        await repository.UpdateAthleteAsync(athlete, cancellationToken);
    }
}
