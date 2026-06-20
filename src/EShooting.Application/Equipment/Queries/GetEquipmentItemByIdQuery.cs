using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using MediatR;

namespace EShooting.Application.Equipment.Queries;

public sealed record GetEquipmentItemByIdQuery(Guid Id) : IRequest<EquipmentCatalogItem?>;

public sealed class GetEquipmentItemByIdQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetEquipmentItemByIdQuery, EquipmentCatalogItem?>
{
    public async Task<EquipmentCatalogItem?> Handle(
        GetEquipmentItemByIdQuery request,
        CancellationToken cancellationToken)
    {
        var item = await repository.GetEquipmentItemByIdAsync(request.Id, cancellationToken);
        return item is null ? null : GetEquipmentItemsQueryHandler.Map(item);
    }
}
