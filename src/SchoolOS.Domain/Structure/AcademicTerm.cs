namespace SchoolOS.Domain.Structure;

public sealed class AcademicTerm
{
    public Guid Id { get; set; }

    public Guid AcademicYearId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Sequence { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public AcademicPeriodStatus Status { get; set; } = AcademicPeriodStatus.Planned;
}
