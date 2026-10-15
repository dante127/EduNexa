using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolOS.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace SchoolOS.Tests;

/// <summary>
/// Integration-test host: starts a throwaway PostgreSQL container, applies EF migrations,
/// and hosts the API against it. Configuration overrides are provided as ENVIRONMENT
/// VARIABLES before the host is built — startup-time configuration reads (which
/// WebApplicationFactory's ConfigureAppConfiguration does not reach) then pick them up.
/// </summary>
public sealed class SchoolOsAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    internal const string TestSigningKey = "integration-test-signing-key-0123456789abcdef";

    private string? _storageRoot;

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17")
        .WithDatabase("schoolos_tests")
        .WithUsername("schoolos")
        .WithPassword("schoolos_tests")
        .Build();

    public async Task InitializeAsync()
    {
        await _database.StartAsync();

        // Must be set BEFORE the host is built (first access to Services/CreateClient).
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", _database.GetConnectionString());
        Environment.SetEnvironmentVariable("Jwt__SigningKey", TestSigningKey);
        Environment.SetEnvironmentVariable("RateLimiting__Auth__PermitLimit", "10000");
        Environment.SetEnvironmentVariable("Seed__DevAdmin__Enabled", "false");
        _storageRoot = Path.Combine(Path.GetTempPath(), $"schoolos-tests-storage-{Guid.NewGuid():N}$");
        Environment.SetEnvironmentVariable("Storage__Root", _storageRoot);

        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();

        Environment.SetEnvironmentVariable("ConnectionStrings__Default", null);
        Environment.SetEnvironmentVariable("Jwt__SigningKey", null);
        Environment.SetEnvironmentVariable("RateLimiting__Auth__PermitLimit", null);
        Environment.SetEnvironmentVariable("Seed__DevAdmin__Enabled", null);
        Environment.SetEnvironmentVariable("Storage__Root", null);

        if (_storageRoot is not null && Directory.Exists(_storageRoot))
        {
            try
            {
                Directory.Delete(_storageRoot, recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup; leftover temp files are harmless.
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
    }
}
