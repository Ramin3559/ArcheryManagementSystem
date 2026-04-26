namespace EShooting.Web.Auth;

/// <summary>
/// Reception üçün tək istifadəçi login məlumatları (appsettings və ya environment ilə override).
/// </summary>
public sealed class ReceptionAuthOptions
{
    public const string SectionName = "Reception";

    public string UserName { get; set; } = "reception";

    public string Password { get; set; } = "";
}
