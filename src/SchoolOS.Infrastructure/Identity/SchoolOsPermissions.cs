namespace SchoolOS.Infrastructure.Identity;

/// <summary>A permission catalog entry definition (code + owning module).</summary>
public sealed record PermissionDefinition(string Code, string Module);

/// <summary>
/// Starter permission catalog and default role mappings (docs/architecture.md §5,
/// docs/security-plan.md §3). Adjustments at runtime will be possible through the
/// admin module once it lands.
/// </summary>
public static class SchoolOsPermissions
{
    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new("students.view", "Students"),
        new("students.manage", "Students"),
        new("structure.view", "Structure"),
        new("structure.manage", "Structure"),
        new("staff.view", "Staff"),
        new("staff.manage", "Staff"),
        new("attendance.take", "Attendance"),
        new("attendance.correct", "Attendance"),
        new("grades.enter", "Academics"),
        new("grades.approve", "Academics"),
        new("grades.publish", "Academics"),
        new("homework.manage", "Learning"),
        new("announcements.manage", "Communication"),
        new("finance.invoices.manage", "Finance"),
        new("finance.payments.post", "Finance"),
        new("finance.refunds.approve", "Finance"),
        new("reports.view", "Reports"),
        new("admin.users.manage", "Administration")
    ];

    /// <summary>
    /// Default role -> permission codes. Parent and Student portal access is resource-scoped
    /// (parent-child checks), so they intentionally carry no module permissions here.
    /// </summary>
    public static IReadOnlyDictionary<string, string[]> DefaultRolePermissions { get; } =
        new Dictionary<string, string[]>
        {
            [SchoolOsRoles.SuperAdmin] = All.Select(p => p.Code).ToArray(),
            [SchoolOsRoles.SchoolAdmin] = All.Select(p => p.Code).ToArray(),
            [SchoolOsRoles.Principal] =
            [
                "students.view",
                "staff.view",
                "staff.manage",
                "structure.view",
                "grades.approve",
                "grades.publish",
                "reports.view"
            ],
            [SchoolOsRoles.Teacher] =
            [
                "students.view",
                "structure.view",
                "attendance.take",
                "attendance.correct",
                "grades.enter",
                "homework.manage"
            ],
            [SchoolOsRoles.Accountant] =
            [
                "structure.view",
                "finance.invoices.manage",
                "finance.payments.post",
                "reports.view"
            ],
            [SchoolOsRoles.Receptionist] =
            [
                "students.view",
                "students.manage",
                "structure.view"
            ],
            [SchoolOsRoles.Parent] = [],
            [SchoolOsRoles.Student] = []
        };
}
