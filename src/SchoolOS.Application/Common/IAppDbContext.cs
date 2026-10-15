using Microsoft.EntityFrameworkCore;
using SchoolOS.Domain.Staff;
using SchoolOS.Domain.Structure;
using SchoolOS.Domain.Students;

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

    DbSet<Student> Students { get; }

    DbSet<Guardian> Guardians { get; }

    DbSet<StudentGuardian> StudentGuardians { get; }

    DbSet<Enrollment> Enrollments { get; }

    DbSet<AdmissionApplication> AdmissionApplications { get; }

    DbSet<StudentNumberCounter> StudentNumberCounters { get; }

    DbSet<StudentDocument> StudentDocuments { get; }

    DbSet<StaffMember> StaffMembers { get; }

    DbSet<TeacherAssignment> TeacherAssignments { get; }

    Task<int> NextStudentNumberAsync(int year, CancellationToken cancellationToken = default);

    Task<long> NextEmployeeNumberAsync(CancellationToken cancellationToken = default);

    Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
