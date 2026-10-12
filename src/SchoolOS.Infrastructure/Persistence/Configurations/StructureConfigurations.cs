using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolOS.Domain.Structure;

namespace SchoolOS.Infrastructure.Persistence.Configurations;

public sealed class SchoolConfiguration : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.ToTable("schools", AppModuleSchemas.Structure);
        builder.HasKey(school => school.Id);
        builder.Property(school => school.NameEn).HasMaxLength(200).IsRequired();
        builder.Property(school => school.NameAr).HasMaxLength(200).IsRequired();
        builder.Property(school => school.Timezone).HasMaxLength(64).IsRequired();
        builder.Property(school => school.DefaultLocale).HasMaxLength(5).IsRequired();
        builder.Property(school => school.CurrencyCode).HasMaxLength(3).IsRequired();
        builder.Property(school => school.Address).HasMaxLength(300);
        builder.Property(school => school.LogoPath).HasMaxLength(300);
        builder.Property(school => school.Phone).HasMaxLength(30);
        builder.Property(school => school.Email).HasMaxLength(200);

        builder.HasData(new School
        {
            Id = School.SingletonId,
            NameEn = "SchoolOS",
            NameAr = "SchoolOS",
            Timezone = "Asia/Damascus",
            DefaultLocale = "en",
            CurrencyCode = "USD"
        });
    }
}

public sealed class AcademicYearConfiguration : IEntityTypeConfiguration<AcademicYear>
{
    public void Configure(EntityTypeBuilder<AcademicYear> builder)
    {
        builder.ToTable("academic_years", AppModuleSchemas.Structure, table =>
        {
            table.HasCheckConstraint("ck_academic_years_dates", "end_date > start_date");
            table.HasCheckConstraint("ck_academic_years_status", "status IN ('Planned', 'Active', 'Closed')");
        });

        builder.HasKey(year => year.Id);
        builder.Property(year => year.Name).HasMaxLength(20).IsRequired();
        builder.Property(year => year.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(year => year.Name).IsUnique();
        builder.HasIndex(year => year.Status)
            .IsUnique()
            .HasFilter("status = 'Active'")
            .HasDatabaseName("ux_academic_years_single_active");
    }
}

public sealed class AcademicTermConfiguration : IEntityTypeConfiguration<AcademicTerm>
{
    public void Configure(EntityTypeBuilder<AcademicTerm> builder)
    {
        builder.ToTable("academic_terms", AppModuleSchemas.Structure, table =>
        {
            table.HasCheckConstraint("ck_academic_terms_dates", "end_date > start_date");
            table.HasCheckConstraint("ck_academic_terms_status", "status IN ('Planned', 'Active', 'Closed')");
            table.HasCheckConstraint("ck_academic_terms_sequence", "sequence > 0");
        });

        builder.HasKey(term => term.Id);
        builder.Property(term => term.Name).HasMaxLength(50).IsRequired();
        builder.Property(term => term.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(term => new { term.AcademicYearId, term.Sequence }).IsUnique();
        builder.HasIndex(term => term.AcademicYearId)
            .IsUnique()
            .HasFilter("status = 'Active'")
            .HasDatabaseName("ux_academic_terms_one_active");

        builder.HasOne<AcademicYear>()
            .WithMany()
            .HasForeignKey(term => term.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GradeLevelConfiguration : IEntityTypeConfiguration<GradeLevel>
{
    public void Configure(EntityTypeBuilder<GradeLevel> builder)
    {
        builder.ToTable("grade_levels", AppModuleSchemas.Structure, table =>
            table.HasCheckConstraint("ck_grade_levels_sequence", "sequence > 0"));

        builder.HasKey(level => level.Id);
        builder.Property(level => level.NameEn).HasMaxLength(100).IsRequired();
        builder.Property(level => level.NameAr).HasMaxLength(100).IsRequired();
        builder.HasIndex(level => level.Sequence).IsUnique();
    }
}

public sealed class ClassSectionConfiguration : IEntityTypeConfiguration<ClassSection>
{
    public void Configure(EntityTypeBuilder<ClassSection> builder)
    {
        builder.ToTable("class_sections", AppModuleSchemas.Structure, table =>
            table.HasCheckConstraint("ck_class_sections_capacity", "capacity > 0"));

        builder.HasKey(section => section.Id);
        builder.Property(section => section.Name).HasMaxLength(20).IsRequired();
        builder.HasIndex(section => new { section.AcademicYearId, section.GradeLevelId, section.Name }).IsUnique();

        builder.HasOne<AcademicYear>()
            .WithMany()
            .HasForeignKey(section => section.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<GradeLevel>()
            .WithMany()
            .HasForeignKey(section => section.GradeLevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("subjects", AppModuleSchemas.Structure);
        builder.HasKey(subject => subject.Id);
        builder.Property(subject => subject.Code).HasMaxLength(20).IsRequired();
        builder.Property(subject => subject.NameEn).HasMaxLength(150).IsRequired();
        builder.Property(subject => subject.NameAr).HasMaxLength(150).IsRequired();
        builder.HasIndex(subject => subject.Code).IsUnique();
    }
}

public sealed class ClassroomConfiguration : IEntityTypeConfiguration<Classroom>
{
    public void Configure(EntityTypeBuilder<Classroom> builder)
    {
        builder.ToTable("classrooms", AppModuleSchemas.Structure, table =>
            table.HasCheckConstraint("ck_classrooms_capacity", "capacity >= 0"));

        builder.HasKey(room => room.Id);
        builder.Property(room => room.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(room => room.Name).IsUnique();
    }
}
