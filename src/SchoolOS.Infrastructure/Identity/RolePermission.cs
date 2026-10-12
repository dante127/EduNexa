namespace SchoolOS.Infrastructure.Identity;

/// <summary>
/// Many-to-many mapping between roles and permissions.
/// </summary>
public sealed class RolePermission
{
    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }
}
