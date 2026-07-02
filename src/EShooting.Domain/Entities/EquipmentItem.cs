using EShooting.Domain.Enums;

namespace EShooting.Domain.Entities;

/// <summary>Admin kataloqu — avadanlıq.</summary>
public sealed class EquipmentItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Category { get; set; }
    public EquipmentUsageMode UsageMode { get; set; } = EquipmentUsageMode.Both;
    /// <summary>Zalda icarə üçün işlək stok.</summary>
    public int RentalQuantity { get; set; }
    /// <summary>Resepsiyada satış üçün stok.</summary>
    public int SaleQuantity { get; set; }
    /// <summary>Cəmi işlək stok (zal + satış).</summary>
    public int Quantity { get; set; }
    /// <summary>İşlək stokdan çıxarılmış — sıradan çıxan / xarab (zaldan).</summary>
    public int DamagedQuantity { get; set; }
    /// <summary>1 ədəd satış qiyməti (vahid).</summary>
    public decimal? Price { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
