using EShooting.Application.Common.Interfaces;
using EShooting.Application.Common.Models;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.Equipment.Queries;

public sealed record GetEquipmentItemsQuery(bool ActiveOnly = false) : IRequest<IReadOnlyCollection<EquipmentCatalogItem>>;

public sealed class GetEquipmentItemsQueryHandler(ITrainingCenterRepository repository)
    : IRequestHandler<GetEquipmentItemsQuery, IReadOnlyCollection<EquipmentCatalogItem>>
{
    public async Task<IReadOnlyCollection<EquipmentCatalogItem>> Handle(
        GetEquipmentItemsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await repository.GetEquipmentItemsAsync(request.ActiveOnly, cancellationToken);

        return items
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(Map)
            .ToArray();
    }

    internal static EquipmentCatalogItem Map(EquipmentItem x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Category = x.Category,
        Quantity = x.Quantity,
        Price = x.Price,
        IsActive = x.IsActive
    };
}
