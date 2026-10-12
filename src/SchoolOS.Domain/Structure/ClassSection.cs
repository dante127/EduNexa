namespace SchoolOS.Domain.Structure;

public sealed class ClassSection
{
    public Guid Id { get; set; }

    public Guid AcademicYearId { get; set; }

    public Guid GradeLevelId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Capacity { get; set; }
}
