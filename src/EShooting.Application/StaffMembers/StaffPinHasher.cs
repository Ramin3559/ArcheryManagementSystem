using System.Security.Cryptography;
using System.Text;

namespace EShooting.Application.StaffMembers;

public static class StaffPinHasher
{
    public static string Hash(string pin)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(pin.Trim()));
        return Convert.ToBase64String(bytes);
    }

    public static bool Verify(string pinHash, string pin) => Hash(pin) == pinHash;
}
