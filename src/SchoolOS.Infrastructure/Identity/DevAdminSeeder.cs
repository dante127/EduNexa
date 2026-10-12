using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SchoolOS.Infrastructure.Identity;

public sealed class DevAdminSeederOptions
{
    public const string SectionName = "Seed:DevAdmin";

    public bool Enabled { get; set; }

    public string Email { get; set; } = "admin@schoolos.local";

    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Development-only bootstrap: idempotently ensures a SuperAdmin account exists so the
/// local environment is usable. Refuses to run in Production. See docs/security-plan.md §6.
/// </summary>
public sealed class DevAdminSeeder(
    UserManager<AppUser> userManager,
    IHostEnvironment environment,
    IOptions<DevAdminSeederOptions> options,
    ILogger<DevAdminSeeder> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        if (environment.IsProduction())
        {
            logger.LogWarning("Dev admin seeding is disabled in Production; skipping.");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogWarning("Dev admin seeding skipped: Seed:DevAdmin:Password is not configured.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(settings.Email);
        if (existing is not null)
        {
            return;
        }

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            UserName = settings.Email,
            Email = settings.Email,
            EmailConfirmed = true,
            Status = UserStatuses.Active
        };

        var createResult = await userManager.CreateAsync(user, settings.Password);
        if (!createResult.Succeeded)
        {
            logger.LogError(
                "Dev admin creation failed: {Errors}",
                string.Join("; ", createResult.Errors.Select(error => error.Description)));
            return;
        }

        var roleResult = await userManager.AddToRoleAsync(user, SchoolOsRoles.SuperAdmin);
        if (!roleResult.Succeeded)
        {
            logger.LogError(
                "Dev admin role assignment failed: {Errors}",
                string.Join("; ", roleResult.Errors.Select(error => error.Description)));
        }

        logger.LogWarning(
            "Seeded development admin {Email}. DEV-ONLY account: change its password and never enable " +
            "Seed:DevAdmin in production.",
            settings.Email);
    }
}
