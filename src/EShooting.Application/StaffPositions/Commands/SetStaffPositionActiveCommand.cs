using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.StaffPositions.Commands;

public sealed record SetStaffPositionActiveCommand(Guid Id, bool IsActive) : IRequest;

public sealed class SetStaffPositionActiveCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<SetStaffPositionActiveCommand>
{
    public async Task Handle(SetStaffPositionActiveCommand request, CancellationToken cancellationToken)
    {
        var item = await repository.GetStaffPositionByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Vəzifə tapılmadı.");

        item.IsActive = request.IsActive;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await repository.UpdateStaffPositionAsync(item, cancellationToken);
    }
}
