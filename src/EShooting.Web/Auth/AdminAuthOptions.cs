namespace EShooting.Web.Auth;

/// <summary>Admin panel girişi (appsettings + App_Data/admin-auth.json override).</summary>
public sealed class AdminAuthOptions
{
    public const string SectionName = "Admin";

    public string UserName { get; set; } = "admin";

    public string Password { get; set; } = "adminadmin";
}
