namespace SchoolOS.Domain.Staff;

/// <summary>
/// Teacher eligibility: staff x subject x section x academic year. Drives workload and
/// timetable eligibility (docs/database-design.md A5).
/// </summary>
public sealed class TeacherAssignment
{
    public Guid Id { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid StaffMemberId { get; set; }

    public Guid SubjectId { get; set; }

    public Guid ClassSectionId { get; set; }
}
