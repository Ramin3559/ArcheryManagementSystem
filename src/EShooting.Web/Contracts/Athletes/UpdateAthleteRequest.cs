using EShooting.Domain.Enums;

namespace EShooting.Web.Contracts.Athletes;

public sealed class UpdateAthleteRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public string IdCardNumber { get; set; } = "";
    public string ClubCardNumber { get; set; } = "";
    public CustomerCategory Category { get; set; }
    public bool IsSubscriber { get; set; }
    public MembershipType MembershipType { get; set; }
    public bool IsVip { get; set; }
}
