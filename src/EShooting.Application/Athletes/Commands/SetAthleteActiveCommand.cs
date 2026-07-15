using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

public sealed record SetAthleteActiveCommand(
    Guid AthleteId,
    bool IsActive,
    Guid? DeletedByStaffId = null,
    string? DeletedByAdminUserName = null) : IRequest;

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
            athlete.IsActive = false;
            athlete.DeletedAtUtc = DateTime.UtcNow;
            athlete.DeletedByStaffId = request.DeletedByStaffId is Guid sid && sid != Guid.Empty
                ? sid
                : null;
            athlete.DeletedByAdminUserName = string.IsNullOrWhiteSpace(request.DeletedByAdminUserName)
                ? null
                : request.DeletedByAdminUserName.Trim();
        }
        else
        {
            athlete.IsActive = true;
            athlete.DeletedAtUtc = null;
            athlete.DeletedByStaffId = null;
            athlete.DeletedByAdminUserName = null;
        }

        await repository.UpdateAthleteAsync(athlete, cancellationToken);
    }
}
