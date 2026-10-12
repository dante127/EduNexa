namespace SchoolOS.Infrastructure.Identity;

/// <summary>
/// Fixed role set (docs/requirements.md §3). Roles group permissions; the permission
/// catalog is in <see cref="SchoolOsPermissions"/>.
/// </summary>
public static class SchoolOsRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string SchoolAdmin = "SchoolAdmin";
    public const string Principal = "Principal";
    public const string Teacher = "Teacher";
    public const string Accountant = "Accountant";
    public const string Receptionist = "Receptionist";
    public const string Parent = "Parent";
    public const string Student = "Student";

    public static IReadOnlyList<string> All { get; } =
    [
        SuperAdmin,
        SchoolAdmin,
        Principal,
        Teacher,
        Accountant,
        Receptionist,
        Parent,
        Student
    ];
}
