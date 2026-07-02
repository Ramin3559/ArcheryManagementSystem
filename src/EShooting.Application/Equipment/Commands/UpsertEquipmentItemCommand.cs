using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.Equipment.Commands;

public sealed record UpsertEquipmentItemCommand(
    Guid? Id,
    string Name,
    string? Category,
    int RentalQuantity,
    int SaleQuantity,
    int DamagedQuantity,
    decimal? Price) : IRequest<Guid>;

public sealed class UpsertEquipmentItemCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<UpsertEquipmentItemCommand, Guid>
{
    public async Task<Guid> Handle(UpsertEquipmentItemCommand request, CancellationToken cancellationToken)
    {
        Validate(request);

        var name = request.Name.Trim();
        var category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();
        var rental = Math.Max(0, request.RentalQuantity);
        var sale = Math.Max(0, request.SaleQuantity);
        var damaged = Math.Max(0, request.DamagedQuantity);

        if (request.Id is null || request.Id == Guid.Empty)
        {
            var created = new EquipmentItem
            {
                Name = name,
                Category = category,
                RentalQuantity = rental,
                SaleQuantity = sale,
                DamagedQuantity = damaged,
                Price = request.Price,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            EquipmentIssuanceRules.SyncDerivedFields(created);
            created = await repository.AddEquipmentItemAsync(created, cancellationToken);
            return created.Id;
        }

        var existing = await repository.GetEquipmentItemByIdAsync(request.Id.Value, cancellationToken)
            ?? throw new InvalidOperationException("Avadanlıq tapılmadı.");

        var damagedDelta = damaged - existing.DamagedQuantity;
        if (damagedDelta > 0)
        {
            rental = Math.Max(0, rental - damagedDelta);
        }

        existing.Name = name;
        existing.Category = category;
        existing.RentalQuantity = rental;
        existing.SaleQuantity = sale;
        existing.DamagedQuantity = damaged;
        existing.Price = request.Price;
        existing.IsActive = true;
        EquipmentIssuanceRules.SyncDerivedFields(existing);

        await repository.UpdateEquipmentItemAsync(existing, cancellationToken);
        return existing.Id;
    }

    private static void Validate(UpsertEquipmentItemCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Avadanlıq adı mütləqdir.");
        }

        if (request.RentalQuantity < 0 || request.SaleQuantity < 0)
        {
            throw new InvalidOperationException("Say mənfi ola bilməz.");
        }

        if (request.DamagedQuantity < 0)
        {
            throw new InvalidOperationException("Xarab say mənfi ola bilməz.");
        }

        if (request.Price is < 0)
        {
            throw new InvalidOperationException("Qiymət mənfi ola bilməz.");
        }
    }
}
