using Microsoft.EntityFrameworkCore;
using SchoolOS.Domain.Structure;

namespace SchoolOS.Application.Common;

/// <summary>
/// Application-layer view of the persistence context (implemented by the Infrastructure
/// <c>AppDbContext</c>). Handlers depend on this contract instead of the concrete context,
/// keeping module dependencies pointing inward (docs/architecture.md §2).
/// </summary>
public interface IAppDbContext
{
    DbSet<AcademicYear> AcademicYears { get; }

    DbSet<AcademicTerm> AcademicTerms { get; }

    DbSet<GradeLevel> GradeLevels { get; }

    DbSet<ClassSection> ClassSections { get; }

    DbSet<Subject> Subjects { get; }

    DbSet<Classroom> Classrooms { get; }

    DbSet<School> Schools { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
