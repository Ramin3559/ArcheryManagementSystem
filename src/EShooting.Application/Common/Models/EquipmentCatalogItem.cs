namespace EShooting.Application.Common.Models;

public sealed class EquipmentCatalogItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
    public int Quantity { get; init; }
    public decimal? Price { get; init; }
    public bool IsActive { get; init; }
}
