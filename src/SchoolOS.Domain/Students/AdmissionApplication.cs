namespace SchoolOS.Domain.Students;

public sealed class AdmissionApplication
{
    public Guid Id { get; set; }

    public string ApplicantFirstNameEn { get; set; } = string.Empty;

    public string ApplicantLastNameEn { get; set; } = string.Empty;

    public string ApplicantFirstNameAr { get; set; } = string.Empty;

    public string ApplicantLastNameAr { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public string Gender { get; set; } = string.Empty;

    public string? Nationality { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid DesiredGradeLevelId { get; set; }

    public string GuardianName { get; set; } = string.Empty;

    public string? GuardianPhone { get; set; }

    public string? GuardianEmail { get; set; }

    public AdmissionApplicationStatus Status { get; set; } = AdmissionApplicationStatus.Submitted;

    public DateTimeOffset SubmittedAt { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? DecisionNotes { get; set; }

    public Guid? ConvertedStudentId { get; set; }
}
