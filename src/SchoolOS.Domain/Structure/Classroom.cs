namespace SchoolOS.Domain.Structure;

public sealed class Classroom
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Capacity { get; set; }

    public bool IsActive { get; set; } = true;
}
