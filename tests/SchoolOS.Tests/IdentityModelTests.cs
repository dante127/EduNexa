using Microsoft.EntityFrameworkCore;
using SchoolOS.Infrastructure.Identity;
using SchoolOS.Infrastructure.Persistence;
using Xunit;

namespace SchoolOS.Tests;

/// <summary>
/// Model-level tests: verify the Identity & Access persistence mapping matches
/// docs/database-design.md §2 (schema/table names, key indexes, seed catalogs).
/// These tests do not touch a database.
/// </summary>
public sealed class IdentityModelTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Port=15432;Database=schoolos;Username=schoolos;Password=schoolos_dev_password")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options);
    }

    [Theory]
    [InlineData(typeof(AppUser), "users")]
    [InlineData(typeof(AppRole), "roles")]
    [InlineData(typeof(Permission), "permissions")]
    [InlineData(typeof(RolePermission), "role_permissions")]
    [InlineData(typeof(RefreshToken), "refresh_tokens")]
    public void Entity_MapsToIdentitySchemaTable(Type clrType, string expectedTable)
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(clrType);

        Assert.NotNull(entityType);
        Assert.Equal(expectedTable, entityType!.GetTableName());
        Assert.Equal("identity", entityType.GetSchema());
    }

    [Fact]
    public void RefreshToken_HasUniqueTokenHashIndex_AndActiveUserIndex()
    {
        using var context = CreateContext();

        var entityType = context.Model.FindEntityType(typeof(RefreshToken))!;
        var indexes = entityType.GetIndexes().ToList();

        Assert.Contains(indexes, i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(RefreshToken.TokenHash)));
        Assert.Contains(
            indexes,
            i => i.GetDatabaseName() == "ix_refresh_tokens_user_active" && i.GetFilter() is not null);
    }

    [Fact]
    public void Catalogs_AreCompleteAndInternallyConsistent()
    {
        Assert.Equal(8, SchoolOsRoles.All.Count);
        Assert.Equal(8, SchoolOsRoles.All.Distinct().Count());

        Assert.Equal(
            SchoolOsPermissions.All.Count,
            SchoolOsPermissions.All.Select(p => p.Code).Distinct().Count());

        Assert.All(
            SchoolOsPermissions.DefaultRolePermissions.Keys,
            role => Assert.Contains(role, SchoolOsRoles.All));

        Assert.All(
            SchoolOsPermissions.DefaultRolePermissions.Values.SelectMany(codes => codes),
            code => Assert.Contains(SchoolOsPermissions.All, p => p.Code == code));
    }
}
