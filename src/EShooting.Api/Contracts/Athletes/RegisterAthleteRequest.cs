namespace EShooting.Web.Contracts.Athletes;
using EShooting.Domain.Enums;

/// <summary>
/// Yeni idmancinin qeydiyyati ucun request modeli.
/// </summary>
public sealed class RegisterAthleteRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? IdCardNumber { get; set; }
    public CustomerCategory Category { get; set; } = CustomerCategory.Amateur;

    /// <summary>
    /// Idmancinin abonent olub-olmadigini gosterir.
    /// </summary>
    public bool IsSubscriber { get; set; }

    public MembershipType MembershipType { get; set; } = MembershipType.FullCombo;
}
