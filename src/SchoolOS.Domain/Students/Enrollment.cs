namespace SchoolOS.Domain.Students;

public sealed class Enrollment
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid GradeLevelId { get; set; }

    public Guid ClassSectionId { get; set; }

    public EnrollmentStatus Status { get; set; } = EnrollmentStatus.Enrolled;

    public DateOnly StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    public string? Notes { get; set; }
}
