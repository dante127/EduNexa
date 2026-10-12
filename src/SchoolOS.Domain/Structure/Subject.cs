namespace SchoolOS.Domain.Structure;

public sealed class Subject
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
