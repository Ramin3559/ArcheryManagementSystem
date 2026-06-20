using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.StaffPositions.Commands;

public sealed record SetStaffPositionDeletedCommand(Guid Id, bool IsDeleted) : IRequest;

public sealed class SetStaffPositionDeletedCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<SetStaffPositionDeletedCommand>
{
    public async Task Handle(SetStaffPositionDeletedCommand request, CancellationToken cancellationToken)
    {
        var position = await repository.GetStaffPositionByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Vəzifə tapılmadı.");

        position.IsDeleted = request.IsDeleted;
        if (request.IsDeleted)
        {
            position.IsActive = false;
        }

        position.UpdatedAtUtc = DateTime.UtcNow;
        await repository.UpdateStaffPositionAsync(position, cancellationToken);
    }
}
