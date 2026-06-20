using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.Equipment.Commands;

public sealed record SetEquipmentItemDeletedCommand(Guid Id, bool IsDeleted) : IRequest;

public sealed class SetEquipmentItemDeletedCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<SetEquipmentItemDeletedCommand>
{
    public async Task Handle(SetEquipmentItemDeletedCommand request, CancellationToken cancellationToken)
    {
        var item = await repository.GetEquipmentItemByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Avadanlıq tapılmadı.");

        item.IsDeleted = request.IsDeleted;
        if (request.IsDeleted)
        {
            item.IsActive = false;
        }

        item.UpdatedAtUtc = DateTime.UtcNow;
        await repository.UpdateEquipmentItemAsync(item, cancellationToken);
    }
}
