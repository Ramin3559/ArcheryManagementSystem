using EShooting.Application.Common.Interfaces;
using EShooting.Domain.Entities;
using MediatR;

namespace EShooting.Application.Equipment.Commands;

public sealed record UpsertEquipmentItemCommand(
    Guid? Id,
    string Name,
    string? Category,
    int Quantity,
    decimal? Price,
    bool IsActive) : IRequest<Guid>;

public sealed class UpsertEquipmentItemCommandHandler(ITrainingCenterRepository repository)
    : IRequestHandler<UpsertEquipmentItemCommand, Guid>
{
    public async Task<Guid> Handle(UpsertEquipmentItemCommand request, CancellationToken cancellationToken)
    {
        Validate(request);

        var name = request.Name.Trim();
        var category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim();

        if (request.Id is null || request.Id == Guid.Empty)
        {
            var created = await repository.AddEquipmentItemAsync(new EquipmentItem
            {
                Name = name,
                Category = category,
                Quantity = request.Quantity,
                Price = request.Price,
                IsActive = request.IsActive,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            }, cancellationToken);

            return created.Id;
        }

        var existing = await repository.GetEquipmentItemByIdAsync(request.Id.Value, cancellationToken)
            ?? throw new InvalidOperationException("Avadanlıq tapılmadı.");

        existing.Name = name;
        existing.Category = category;
        existing.Quantity = request.Quantity;
        existing.Price = request.Price;
        existing.IsActive = request.IsActive;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        await repository.UpdateEquipmentItemAsync(existing, cancellationToken);
        return existing.Id;
    }

    private static void Validate(UpsertEquipmentItemCommand request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new InvalidOperationException("Avadanlıq adı mütləqdir.");
        }

        if (request.Quantity < 0)
        {
            throw new InvalidOperationException("Say mənfi ola bilməz.");
        }

        if (request.Price is < 0)
        {
            throw new InvalidOperationException("Qiymət mənfi ola bilməz.");
        }
    }
}
