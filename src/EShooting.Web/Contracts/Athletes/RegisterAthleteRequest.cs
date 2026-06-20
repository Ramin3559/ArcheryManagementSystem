using EShooting.Domain.Enums;

namespace EShooting.Web.Contracts.Athletes;

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

    /// <summary>
    /// Abunəlik növü (ArcheryOnly, GymOnly, FullCombo).
    /// </summary>
    public MembershipType MembershipType { get; set; } = MembershipType.FullCombo;

    public bool IsVip { get; set; }
}
