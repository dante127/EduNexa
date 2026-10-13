namespace SchoolOS.Domain.Students;

public sealed class Student
{
    public Guid Id { get; set; }

    public string StudentNumber { get; set; } = string.Empty;

    public string FirstNameEn { get; set; } = string.Empty;

    public string LastNameEn { get; set; } = string.Empty;

    public string FirstNameAr { get; set; } = string.Empty;

    public string LastNameAr { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public string Gender { get; set; } = string.Empty;

    public string? Nationality { get; set; }

    public StudentStatus Status { get; set; } = StudentStatus.Active;

    public Guid? UserId { get; set; }

    public string? PhotoPath { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }
}
