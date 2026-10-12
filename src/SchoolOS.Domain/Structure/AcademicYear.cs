namespace SchoolOS.Domain.Structure;

public sealed class AcademicYear
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public AcademicPeriodStatus Status { get; set; } = AcademicPeriodStatus.Planned;
}
