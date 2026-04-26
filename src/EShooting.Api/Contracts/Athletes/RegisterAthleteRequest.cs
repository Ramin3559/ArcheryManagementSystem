namespace EShooting.Web.Contracts.Athletes;

/// <summary>
/// Yeni idmancinin qeydiyyati ucun request modeli.
/// </summary>
public sealed class RegisterAthleteRequest
{
    /// <summary>
    /// Idmancinin tam adi.
    /// </summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>
    /// Idmancinin abonent olub-olmadigini gosterir.
    /// </summary>
    public bool IsSubscriber { get; set; }
}
