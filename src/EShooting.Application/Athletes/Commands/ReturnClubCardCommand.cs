using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.Athletes.Commands;

public sealed record ReturnClubCardCommand(Guid AthleteId, Guid? StaffId = null) : IRequest;

public sealed class ReturnClubCardCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<ReturnClubCardCommand>
{
    public async Task Handle(ReturnClubCardCommand request, CancellationToken cancellationToken)
    {
        var athlete = await repository.GetAthleteByIdAsync(request.AthleteId, cancellationToken)
            ?? throw new InvalidOperationException("Müştəri tapılmadı.");

        await ClubCardAssignmentService.ReturnCardAsync(
            repository,
            athlete,
            request.StaffId,
            cancellationToken);
    }
}
