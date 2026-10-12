using Microsoft.AspNetCore.Identity;

namespace SchoolOS.Infrastructure.Identity;

/// <summary>
/// Application role. The fixed catalog lives in <see cref="SchoolOsRoles"/>.
/// </summary>
public sealed class AppRole : IdentityRole<Guid>
{
    public AppRole()
    {
    }

    public AppRole(string roleName) : base(roleName)
    {
    }
}
