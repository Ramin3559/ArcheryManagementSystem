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
        UsageMode = x.UsageMode,
        RentalQuantity = x.RentalQuantity,
        SaleQuantity = x.SaleQuantity,
        Quantity = x.Quantity,
        DamagedQuantity = x.DamagedQuantity,
        Price = x.Price,
        UnitPrice = x.Price,
        IsActive = x.IsActive
    };
}
