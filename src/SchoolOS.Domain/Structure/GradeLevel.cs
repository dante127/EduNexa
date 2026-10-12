namespace SchoolOS.Domain.Structure;

public sealed class GradeLevel
{
    public Guid Id { get; set; }

    public int Sequence { get; set; }

    public string NameEn { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;
}
