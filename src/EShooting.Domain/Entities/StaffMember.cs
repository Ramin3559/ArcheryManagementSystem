namespace EShooting.Domain.Entities;

/// <summary>Resepsiya işçisi — admin qeydiyyatı.</summary>
public sealed class StaffMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid StaffPositionId { get; set; }
    public Guid AccessProfileId { get; set; }
    public string? PhoneNumber { get; set; }
    public string PinHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public StaffPosition? StaffPosition { get; set; }
    public AccessProfile? AccessProfile { get; set; }
}
