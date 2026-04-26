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
    public CustomerCategory Category { get; set; } = CustomerCategory.Amateur;
    public bool IsSubscriber { get; set; }
    public MembershipType MembershipType { get; set; } = MembershipType.FullCombo;
    public bool IsFullPackage { get; set; }
}
