namespace SchoolOS.Infrastructure.Persistence;

/// <summary>
/// PostgreSQL schema names owned by each module (see docs/database-design.md).
/// Entity ownership stays visible at the database level; cross-schema writes follow module boundaries.
/// </summary>
public static class AppModuleSchemas
{
    public const string Identity = "identity";
    public const string Structure = "structure";
    public const string Students = "students";
    public const string Staff = "staff";
    public const string Attendance = "attendance";
    public const string Academics = "academics";
    public const string Finance = "finance";
    public const string Learning = "learning";
    public const string Communication = "communication";
    public const string Audit = "audit";

    public static IReadOnlyList<string> All { get; } =
    [
        Identity,
        Structure,
        Students,
        Staff,
        Attendance,
        Academics,
        Finance,
        Learning,
        Communication,
        Audit
    ];
}
