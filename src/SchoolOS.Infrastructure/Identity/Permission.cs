namespace SchoolOS.Infrastructure.Identity;

/// <summary>
/// Permission catalog entry (docs/database-design.md §2, permissions table).
/// Codes follow the module.action convention, e.g. grades.approve.
/// </summary>
public sealed class Permission
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Module { get; set; } = string.Empty;

    public string? Description { get; set; }
}
