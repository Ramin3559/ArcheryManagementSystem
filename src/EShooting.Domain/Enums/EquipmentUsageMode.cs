namespace EShooting.Domain.Enums;

/// <summary>Admin kataloqunda avadanlığın istifadə növü.</summary>
public enum EquipmentUsageMode
{
    /// <summary>Müştəri özünə götürür (satış).</summary>
    Sale = 0,

    /// <summary>Zalda oyun üçün icarə — təhvil alınır.</summary>
    Rental = 1,

    /// <summary>Resepsiyada satış və ya icarə seçilə bilər.</summary>
    Both = 2
}
