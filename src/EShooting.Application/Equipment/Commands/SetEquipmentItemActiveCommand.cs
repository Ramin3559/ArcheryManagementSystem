using EShooting.Application.Common.Interfaces;
using MediatR;

namespace EShooting.Application.Equipment.Commands;

public sealed record SetEquipmentItemActiveCommand(Guid Id, bool IsActive) : IRequest;

public sealed class SetEquipmentItemActiveCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<SetEquipmentItemActiveCommand>
{
    public async Task Handle(SetEquipmentItemActiveCommand request, CancellationToken cancellationToken)
    {
        var item = await repository.GetEquipmentItemByIdAsync(request.Id, cancellationToken)
            ?? throw new InvalidOperationException("Avadanlıq tapılmadı.");

        item.IsActive = request.IsActive;
        item.UpdatedAtUtc = DateTime.UtcNow;
        await repository.UpdateEquipmentItemAsync(item, cancellationToken);
    }
}
