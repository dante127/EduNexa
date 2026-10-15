using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Staff;
using SchoolOS.Domain.Structure;
using SchoolOS.Domain.Students;
using SchoolOS.Infrastructure.Identity;

namespace SchoolOS.Infrastructure.Persistence;

/// <summary>
/// Single EF Core context for the modular monolith. Modules own their entities and are
/// grouped by PostgreSQL schema (see docs/database-design.md); entity sets are added
/// module by module during Phase 2+. Identity tables live in the "identity" schema
/// with snake_case names (custom-mapped; no AspNet* names).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<AppUser, AppRole, Guid>(options), IAppDbContext
{
    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<School> Schools => Set<School>();

    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();

    public DbSet<AcademicTerm> AcademicTerms => Set<AcademicTerm>();

    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();

    public DbSet<ClassSection> ClassSections => Set<ClassSection>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<Classroom> Classrooms => Set<Classroom>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Guardian> Guardians => Set<Guardian>();

    public DbSet<StudentGuardian> StudentGuardians => Set<StudentGuardian>();

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    public DbSet<AdmissionApplication> AdmissionApplications => Set<AdmissionApplication>();

    public DbSet<StudentNumberCounter> StudentNumberCounters => Set<StudentNumberCounter>();

    public DbSet<StudentDocument> StudentDocuments => Set<StudentDocument>();

    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();

    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();

    public async Task<int> NextStudentNumberAsync(int year, CancellationToken cancellationToken = default)
    {
        // Lock the counter row so concurrent enrollments cannot allocate the same number.
        var counter = await StudentNumberCounters
            .FromSqlInterpolated($"SELECT * FROM students.student_number_counters WHERE year = {year} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (counter is null)
        {
            counter = new StudentNumberCounter { Year = year, LastNumber = 1 };
            StudentNumberCounters.Add(counter);
        }
        else
        {
            counter.LastNumber += 1;
        }

        return counter.LastNumber;
    }

    public async Task<long> NextEmployeeNumberAsync(CancellationToken cancellationToken = default)
    {
        // Sequence-backed allocation (staff.employee_number_seq); gaps are acceptable
        // (docs/database-design.md).
        return await Database
            .SqlQueryRaw<long>("SELECT nextval('staff.employee_number_seq') AS \"Value\"")
            .SingleAsync(cancellationToken);
    }

    public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Users.AnyAsync(user => user.Id == userId, cancellationToken);

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        ConfigureIdentityTables(builder);
        ConfigureAccessControl(builder);
        ConfigureRefreshTokens(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    private static void ConfigureIdentityTables(ModelBuilder builder)
    {
        builder.Entity<AppUser>(entity =>
        {
            entity.ToTable("users", AppModuleSchemas.Identity, table =>
            {
                table.HasCheckConstraint("ck_users_status", "status IN ('Invited', 'Active', 'Deactivated')");
                table.HasCheckConstraint("ck_users_preferred_language", "preferred_language IN ('en', 'ar')");
            });

            entity.Property(u => u.Status).HasMaxLength(20).HasDefaultValue(UserStatuses.Active);
            entity.Property(u => u.PreferredLanguage).HasMaxLength(5).HasDefaultValue("en");
        });

        builder.Entity<AppRole>().ToTable("roles", AppModuleSchemas.Identity);
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles", AppModuleSchemas.Identity);
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims", AppModuleSchemas.Identity);
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims", AppModuleSchemas.Identity);
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins", AppModuleSchemas.Identity);
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens", AppModuleSchemas.Identity);
    }

    private static void ConfigureAccessControl(ModelBuilder builder)
    {
        builder.Entity<Permission>(entity =>
        {
            entity.ToTable("permissions", AppModuleSchemas.Identity);
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Code).HasMaxLength(100).IsRequired();
            entity.HasIndex(p => p.Code).IsUnique();
            entity.Property(p => p.Module).HasMaxLength(50).IsRequired();
            entity.Property(p => p.Description).HasMaxLength(300);
        });

        builder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("role_permissions", AppModuleSchemas.Identity);
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });
            entity.HasOne<AppRole>().WithMany().HasForeignKey(rp => rp.RoleId);
            entity.HasOne<Permission>().WithMany().HasForeignKey(rp => rp.PermissionId);
        });

        SeedAccessControl(builder);
    }

    private static void SeedAccessControl(ModelBuilder builder)
    {
        builder.Entity<AppRole>().HasData(SchoolOsRoles.All.Select(role => new AppRole
        {
            Id = SchoolOsIdentityIds.Role(role),
            Name = role,
            NormalizedName = role.ToUpperInvariant(),
            ConcurrencyStamp = SchoolOsIdentityIds.Role(role).ToString()
        }));

        builder.Entity<Permission>().HasData(SchoolOsPermissions.All.Select(permission => new Permission
        {
            Id = SchoolOsIdentityIds.Permission(permission.Code),
            Code = permission.Code,
            Module = permission.Module
        }));

        builder.Entity<RolePermission>().HasData(SchoolOsPermissions.DefaultRolePermissions
            .SelectMany(pair => pair.Value.Select(code => new RolePermission
            {
                RoleId = SchoolOsIdentityIds.Role(pair.Key),
                PermissionId = SchoolOsIdentityIds.Permission(code)
            })));
    }

    private static void ConfigureRefreshTokens(ModelBuilder builder)
    {
        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens", AppModuleSchemas.Identity);
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(t => t.TokenHash).IsUnique();
            entity.Property(t => t.UserAgent).HasMaxLength(512);
            entity.HasIndex(t => t.UserId)
                .HasDatabaseName("ix_refresh_tokens_user_active")
                .HasFilter("revoked_at IS NULL");

            entity.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<RefreshToken>()
                .WithMany()
                .HasForeignKey(t => t.ReplacedById)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
