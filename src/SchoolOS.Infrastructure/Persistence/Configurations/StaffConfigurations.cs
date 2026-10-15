using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolOS.Domain.Staff;
using SchoolOS.Infrastructure.Identity;

namespace SchoolOS.Infrastructure.Persistence.Configurations;

public sealed class StaffMemberConfiguration : IEntityTypeConfiguration<StaffMember>
{
    public void Configure(EntityTypeBuilder<StaffMember> builder)
    {
        builder.ToTable("staff_members", AppModuleSchemas.Staff, table =>
        {
            table.HasCheckConstraint("ck_staff_members_status", "status IN ('Active', 'OnLeave', 'Terminated')");
            table.HasCheckConstraint(
                "ck_staff_members_contract_type",
                "contract_type IN ('FullTime', 'PartTime', 'Contract')");
        });

        builder.HasKey(staff => staff.Id);
        builder.Property(staff => staff.EmployeeNumber).HasMaxLength(30).IsRequired();
        builder.HasIndex(staff => staff.EmployeeNumber).IsUnique();
        builder.Property(staff => staff.FirstNameEn).HasMaxLength(100).IsRequired();
        builder.Property(staff => staff.LastNameEn).HasMaxLength(100).IsRequired();
        builder.Property(staff => staff.FirstNameAr).HasMaxLength(100).IsRequired();
        builder.Property(staff => staff.LastNameAr).HasMaxLength(100).IsRequired();
        builder.Property(staff => staff.Position).HasMaxLength(100).IsRequired();
        builder.Property(staff => staff.Department).HasMaxLength(100);
        builder.Property(staff => staff.ContractType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(staff => staff.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(staff => staff.Phone).HasMaxLength(30);
        builder.Property(staff => staff.Email).HasMaxLength(200);
        builder.Property(staff => staff.EmergencyContact).HasMaxLength(200);
        builder.Property(staff => staff.Notes).HasMaxLength(500);

        builder.HasIndex(staff => staff.Status);
        builder.HasIndex(staff => staff.Department);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(staff => staff.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(staff => staff.UserId).IsUnique();
    }
}

public sealed class TeacherAssignmentConfiguration : IEntityTypeConfiguration<TeacherAssignment>
{
    public void Configure(EntityTypeBuilder<TeacherAssignment> builder)
    {
        builder.ToTable("teacher_assignments", AppModuleSchemas.Staff);
        builder.HasKey(assignment => assignment.Id);

        builder.HasIndex(assignment => new
            {
                assignment.AcademicYearId,
                assignment.StaffMemberId,
                assignment.SubjectId,
                assignment.ClassSectionId
            })
            .IsUnique()
            .HasDatabaseName("ux_teacher_assignments");

        builder.HasIndex(assignment => assignment.StaffMemberId);
        builder.HasIndex(assignment => assignment.ClassSectionId);
        builder.HasIndex(assignment => assignment.SubjectId);

        builder.HasOne<StaffMember>()
            .WithMany()
            .HasForeignKey(assignment => assignment.StaffMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Structure.Subject>()
            .WithMany()
            .HasForeignKey(assignment => assignment.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Structure.AcademicYear>()
            .WithMany()
            .HasForeignKey(assignment => assignment.AcademicYearId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Domain.Structure.ClassSection>()
            .WithMany()
            .HasForeignKey(assignment => assignment.ClassSectionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
