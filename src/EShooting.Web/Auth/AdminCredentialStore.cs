using System.Text.Json;
using Microsoft.Extensions.Options;

namespace EShooting.Web.Auth;

public interface IAdminCredentialStore
{
    AdminAuthOptions GetCredentials();

    bool TryValidate(string userName, string password);

    bool TryChangePassword(string currentPassword, string newPassword, out string? error);
}

public sealed class AdminCredentialStore : IAdminCredentialStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly AdminAuthOptions _defaults;
    private readonly object _lock = new();

    public AdminCredentialStore(IWebHostEnvironment env, IOptions<AdminAuthOptions> defaults)
    {
        _defaults = defaults.Value;
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "admin-auth.json");
    }

    public AdminAuthOptions GetCredentials()
    {
        lock (_lock)
        {
            return LoadLocked();
        }
    }

    public bool TryValidate(string userName, string password)
    {
        var creds = GetCredentials();
        return string.Equals(userName, creds.UserName, StringComparison.Ordinal)
            && string.Equals(password, creds.Password, StringComparison.Ordinal);
    }

    public bool TryChangePassword(string currentPassword, string newPassword, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            error = "Yeni şifrə boş ola bilməz.";
            return false;
        }

        if (newPassword.Length < 6)
        {
            error = "Yeni şifrə ən azı 6 simvol olmalıdır.";
            return false;
        }

        lock (_lock)
        {
            var creds = LoadLocked();
            if (!string.Equals(currentPassword, creds.Password, StringComparison.Ordinal))
            {
                error = "Köhnə şifrə yanlışdır.";
                return false;
            }

            creds.Password = newPassword;
            SaveLocked(creds);
            return true;
        }
    }

    private AdminAuthOptions LoadLocked()
    {
        if (!File.Exists(_filePath))
        {
            return Clone(_defaults);
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var fromFile = JsonSerializer.Deserialize<AdminAuthOptions>(json);
            if (fromFile is null || string.IsNullOrWhiteSpace(fromFile.UserName) || string.IsNullOrWhiteSpace(fromFile.Password))
            {
                return Clone(_defaults);
            }

            return fromFile;
        }
        catch
        {
            return Clone(_defaults);
        }
    }

    private void SaveLocked(AdminAuthOptions creds)
    {
        var json = JsonSerializer.Serialize(creds, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private static AdminAuthOptions Clone(AdminAuthOptions source) =>
        new()
        {
            UserName = source.UserName,
            Password = source.Password
        };
}
