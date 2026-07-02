using EShooting.Domain.Enums;

namespace EShooting.Api.Contracts.Athletes;

public sealed class RegisterAthleteRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string IdCardNumber { get; set; } = string.Empty;
    public string ClubCardNumber { get; set; } = string.Empty;
    public CustomerCategory Category { get; set; } = CustomerCategory.Amateur;
    public bool IsSubscriber { get; set; }
    public MembershipType MembershipType { get; set; } = MembershipType.FullCombo;
    public bool IsVip { get; set; }
}
