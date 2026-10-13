namespace SchoolOS.Domain.Students;

public sealed class Guardian
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string FullNameEn { get; set; } = string.Empty;

    public string FullNameAr { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public string? AltPhone { get; set; }

    public string? Email { get; set; }

    public string? Occupation { get; set; }

    public string? Address { get; set; }
}
