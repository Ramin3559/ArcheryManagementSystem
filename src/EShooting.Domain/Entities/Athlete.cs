using EShooting.Domain.Enums;

namespace EShooting.Domain.Entities;

public sealed class Athlete
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? IdCardNumber { get; set; }
    /// <summary>Klubun verdiyi fiziki müştəri kartının nömrəsi.</summary>
    public string? ClubCardNumber { get; set; }
    /// <summary>Kart növü (Boz/Qırmızı/Qara/VIP-Qızılı). Kart yoxdursa null.</summary>
    public ClubCardType? ClubCardType { get; set; }
    public CustomerCategory Category { get; set; } = CustomerCategory.Amateur;
    public bool IsSubscriber { get; set; }
    public MembershipType MembershipType { get; set; } = MembershipType.FullCombo;
    public bool IsFullPackage { get; set; }
    public bool IsVip { get; set; }
    public bool IsGroupPlaceholder { get; set; }
    public bool IsActive { get; set; } = true;
    /// <summary>Soft-delete vaxtı (UTC). IsActive=false olanda doldurulur.</summary>
    public DateTime? DeletedAtUtc { get; set; }
    /// <summary>Resepsiya işçisi sildisə.</summary>
    public Guid? DeletedByStaffId { get; set; }
    /// <summary>Admin paneldən sildisə — admin login adı.</summary>
    public string? DeletedByAdminUserName { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? RegisteredByStaffId { get; set; }
}
