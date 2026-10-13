namespace SchoolOS.Domain.Students;

/// <summary>
/// Enrollment lifecycle (docs/database-design.md §4): "Enrolled" = the open record for a
/// student+year; terminal statuses close the record and are never overwritten.
/// </summary>
public enum EnrollmentStatus
{
    Enrolled,
    Completed,
    Transferred,
    Withdrawn,
    Graduated
}
