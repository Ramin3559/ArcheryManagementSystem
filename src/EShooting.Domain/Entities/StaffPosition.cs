namespace EShooting.Domain.Entities;

/// <summary>İşçi vəzifəsi — göstərmə; standart icazə profili təyin edilə bilər.</summary>
public sealed class StaffPosition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? DefaultAccessProfileId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public AccessProfile? DefaultAccessProfile { get; set; }
}
