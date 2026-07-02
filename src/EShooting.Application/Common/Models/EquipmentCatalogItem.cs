using EShooting.Domain.Enums;

namespace EShooting.Application.Common.Models;

public sealed class EquipmentCatalogItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Category { get; init; }
    public EquipmentUsageMode UsageMode { get; init; }
    public int RentalQuantity { get; init; }
    public int SaleQuantity { get; init; }
    public int Quantity { get; init; }
    public int DamagedQuantity { get; init; }
    public decimal? Price { get; init; }
    /// <summary>1 ədəd satış qiyməti (vahid).</summary>
    public decimal? UnitPrice { get; init; }
    public bool IsActive { get; init; }
}
