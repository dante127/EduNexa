namespace SchoolOS.Domain.Staff;

public sealed class StaffMember
{
    public Guid Id { get; set; }

    /// <summary>Optional link to the identity user account (staff portal access).</summary>
    public Guid? UserId { get; set; }

    public string EmployeeNumber { get; set; } = string.Empty;

    public string FirstNameEn { get; set; } = string.Empty;

    public string LastNameEn { get; set; } = string.Empty;

    public string FirstNameAr { get; set; } = string.Empty;

    public string LastNameAr { get; set; } = string.Empty;

    public string Position { get; set; } = string.Empty;

    public string? Department { get; set; }

    public DateOnly HireDate { get; set; }

    public ContractType ContractType { get; set; } = ContractType.FullTime;

    public StaffStatus Status { get; set; } = StaffStatus.Active;

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public string? EmergencyContact { get; set; }

    public string? Notes { get; set; }
}
