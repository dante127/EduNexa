using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolOS.Domain.Students;

namespace SchoolOS.Infrastructure.Persistence.Configurations;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("students", AppModuleSchemas.Students, table =>
        {
            table.HasCheckConstraint(
                "ck_students_status",
                "status IN ('Applicant', 'Active', 'Transferred', 'Withdrawn', 'Graduated')");
            table.HasCheckConstraint("ck_students_gender", "gender IN ('Male', 'Female')");
        });

        builder.HasKey(student => student.Id);
        builder.Property(student => student.StudentNumber).HasMaxLength(20).IsRequired();
        builder.HasIndex(student => student.StudentNumber).IsUnique();
        builder.Property(student => student.FirstNameEn).HasMaxLength(100).IsRequired();
        builder.Property(student => student.LastNameEn).HasMaxLength(100).IsRequired();
        builder.Property(student => student.FirstNameAr).HasMaxLength(100).IsRequired();
        builder.Property(student => student.LastNameAr).HasMaxLength(100).IsRequired();
        builder.Property(student => student.Gender).HasMaxLength(10).IsRequired();
        builder.Property(student => student.Nationality).HasMaxLength(100);
        builder.Property(student => student.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(student => student.PhotoPath).HasMaxLength(300);
        builder.Property(student => student.Phone).HasMaxLength(30);
        builder.Property(student => student.Email).HasMaxLength(200);
        builder.Property(student => student.Address).HasMaxLength(300);

        builder.HasIndex(student => new { student.LastNameEn, student.FirstNameEn });
        builder.HasIndex(student => student.Status);
    }
}

public sealed class GuardianConfiguration : IEntityTypeConfiguration<Guardian>
{
    public void Configure(EntityTypeBuilder<Guardian> builder)
    {
        builder.ToTable("guardians", AppModuleSchemas.Students);
        builder.HasKey(guardian => guardian.Id);
        builder.Property(guardian => guardian.FullNameEn).HasMaxLength(150).IsRequired();
        builder.Property(guardian => guardian.FullNameAr).HasMaxLength(150).IsRequired();
        builder.Property(guardian => guardian.Phone).HasMaxLength(30);
        builder.Property(guardian => guardian.AltPhone).HasMaxLength(30);
        builder.Property(guardian => guardian.Email).HasMaxLength(200);
        builder.Property(guardian => guardian.Occupation).HasMaxLength(150);
        builder.Property(guardian => guardian.Address).HasMaxLength(300);
        builder.HasIndex(guardian => guardian.FullNameEn);
    }
}

public sealed class StudentGuardianConfiguration : IEntityTypeConfiguration<StudentGuardian>
{
    public void Configure(EntityTypeBuilder<StudentGuardian> builder)
    {
        builder.ToTable("student_guardians", AppModuleSchemas.Students, table =>
            table.HasCheckConstraint(
                "ck_student_guardians_relationship",
                "relationship IN ('Father', 'Mother', 'Guardian', 'Other')"));

        builder.HasKey(link => new { link.StudentId, link.GuardianId });
        builder.Property(link => link.Relationship).HasMaxLength(20).IsRequired();
        builder.Property(link => link.CustodyNotes).HasMaxLength(500);

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(link => link.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Guardian>()
            .WithMany()
            .HasForeignKey(link => link.GuardianId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.ToTable("enrollments", AppModuleSchemas.Students, table =>
            table.HasCheckConstraint(
                "ck_enrollments_status",
                "status IN ('Enrolled', 'Completed', 'Transferred', 'Withdrawn', 'Graduated')"));

        builder.HasKey(enrollment => enrollment.Id);
        builder.Property(enrollment => enrollment.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(enrollment => enrollment.Notes).HasMaxLength(500);

        // One open enrollment per student+year (docs/database-design.md §4).
        builder.HasIndex(enrollment => new { enrollment.StudentId, enrollment.AcademicYearId })
            .IsUnique()
            .HasFilter("status = 'Enrolled'")
            .HasDatabaseName("ux_enrollments_open");

        builder.HasIndex(enrollment => enrollment.StudentId);
        builder.HasIndex(enrollment => enrollment.ClassSectionId);

        builder.HasOne<Student>()
            .WithMany()
            .HasForeignKey(enrollment => enrollment.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Structure.AcademicYear>()
            .WithMany()
            .HasForeignKey(enrollment => enrollment.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Structure.GradeLevel>()
            .WithMany()
            .HasForeignKey(enrollment => enrollment.GradeLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Structure.ClassSection>()
            .WithMany()
            .HasForeignKey(enrollment => enrollment.ClassSectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class AdmissionApplicationConfiguration : IEntityTypeConfiguration<AdmissionApplication>
{
    public void Configure(EntityTypeBuilder<AdmissionApplication> builder)
    {
        builder.ToTable("admission_applications", AppModuleSchemas.Students, table =>
            table.HasCheckConstraint(
                "ck_admission_applications_status",
                "status IN ('Submitted', 'UnderReview', 'Approved', 'Rejected', 'Enrolled')"));

        builder.HasKey(application => application.Id);
        builder.Property(application => application.ApplicantFirstNameEn).HasMaxLength(100).IsRequired();
        builder.Property(application => application.ApplicantLastNameEn).HasMaxLength(100).IsRequired();
        builder.Property(application => application.ApplicantFirstNameAr).HasMaxLength(100).IsRequired();
        builder.Property(application => application.ApplicantLastNameAr).HasMaxLength(100).IsRequired();
        builder.Property(application => application.Gender).HasMaxLength(10).IsRequired();
        builder.Property(application => application.Nationality).HasMaxLength(100);
        builder.Property(application => application.GuardianName).HasMaxLength(150).IsRequired();
        builder.Property(application => application.GuardianPhone).HasMaxLength(30);
        builder.Property(application => application.GuardianEmail).HasMaxLength(200);
        builder.Property(application => application.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(application => application.DecisionNotes).HasMaxLength(500);

        builder.HasIndex(application => application.Status);

        builder.HasOne<Domain.Structure.AcademicYear>()
            .WithMany()
            .HasForeignKey(application => application.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Structure.GradeLevel>()
            .WithMany()
            .HasForeignKey(application => application.DesiredGradeLevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class StudentNumberCounterConfiguration : IEntityTypeConfiguration<StudentNumberCounter>
{
    public void Configure(EntityTypeBuilder<StudentNumberCounter> builder)
    {
        builder.ToTable("student_number_counters", AppModuleSchemas.Students);
        builder.HasKey(counter => counter.Year);
        builder.Property(counter => counter.LastNumber);
    }
}
