using System.Security.Cryptography;
using System.Text;

namespace SchoolOS.Infrastructure.Identity;

/// <summary>
/// Deterministic GUIDs for fixed seed data (roles, permissions) so EF Core HasData
/// stays stable across migrations. Not used for runtime entities and not
/// security-sensitive.
/// </summary>
public static class SchoolOsIdentityIds
{
    public static Guid Role(string roleName) => Deterministic("schoolos.role:" + roleName);

    public static Guid Permission(string permissionCode) => Deterministic("schoolos.permission:" + permissionCode);

    private static Guid Deterministic(string seed)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
        return new Guid(hash.AsSpan(0, 16));
    }
}
