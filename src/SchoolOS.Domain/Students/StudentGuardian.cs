namespace SchoolOS.Domain.Students;

/// <summary>
/// Many-to-many link between students and guardians with relationship flags
/// (docs/database-design.md §4): at most one primary guardian per student.
/// </summary>
public sealed class StudentGuardian
{
    public Guid StudentId { get; set; }

    public Guid GuardianId { get; set; }

    public string Relationship { get; set; } = string.Empty;

    public bool IsPrimary { get; set; }

    public bool IsEmergencyContact { get; set; }

    public bool IsAuthorizedPickup { get; set; }

    public string? CustodyNotes { get; set; }
}
