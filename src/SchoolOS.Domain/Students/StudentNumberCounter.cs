namespace SchoolOS.Domain.Students;

/// <summary>
/// Per-year atomic counter for student numbers (format YYYY-####, docs/requirements.md Q7).
/// Rows are locked (SELECT ... FOR UPDATE) during allocation to keep numbers sequential.
/// </summary>
public sealed class StudentNumberCounter
{
    public int Year { get; set; }

    public int LastNumber { get; set; }
}
