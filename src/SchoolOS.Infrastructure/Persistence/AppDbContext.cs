using Microsoft.EntityFrameworkCore;

namespace SchoolOS.Infrastructure.Persistence;

/// <summary>
/// Single EF Core context for the modular monolith. Modules own their entities and are
/// grouped by PostgreSQL schema (see docs/database-design.md); entity sets are added
/// module by module during Phase 2+.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
    }
}
