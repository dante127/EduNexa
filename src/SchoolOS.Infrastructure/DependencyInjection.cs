using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolOS.Application.Common;
using SchoolOS.Infrastructure.Identity;
using SchoolOS.Infrastructure.Persistence;
using SchoolOS.Infrastructure.Storage;

namespace SchoolOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is not configured. " +
                "Set ConnectionStrings__Default or configure appsettings.");

        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());

        services.Configure<StorageOptions>(options =>
        {
            var section = configuration.GetSection(StorageOptions.SectionName);
            if (!string.IsNullOrWhiteSpace(section["Root"]))
            {
                options.Root = section["Root"]!;
            }

            if (long.TryParse(section["MaxFileSizeBytes"], out var maxBytes) && maxBytes > 0)
            {
                options.MaxFileSizeBytes = maxBytes;
            }
        });

        services.AddSingleton<IFileStorage, LocalFileStorage>();

        services.Configure<JwtOptions>(options =>
        {
            var section = configuration.GetSection(JwtOptions.SectionName);
            options.Issuer = section["Issuer"] ?? options.Issuer;
            options.Audience = section["Audience"] ?? options.Audience;
            options.SigningKey = section["SigningKey"] ?? options.SigningKey;
            if (int.TryParse(section["AccessTokenMinutes"], out var accessMinutes) && accessMinutes > 0)
            {
                options.AccessTokenMinutes = accessMinutes;
            }

            if (int.TryParse(section["RefreshTokenDays"], out var refreshDays) && refreshDays > 0)
            {
                options.RefreshTokenDays = refreshDays;
            }
        });

        services.Configure<DevAdminSeederOptions>(options =>
        {
            var section = configuration.GetSection(DevAdminSeederOptions.SectionName);
            options.Enabled = bool.TryParse(section["Enabled"], out var enabled) && enabled;
            if (!string.IsNullOrWhiteSpace(section["Email"]))
            {
                options.Email = section["Email"]!;
            }

            if (!string.IsNullOrWhiteSpace(section["Password"]))
            {
                options.Password = section["Password"]!;
            }
        });

        services.AddIdentityCore<AppUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<AppRole>()
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        services.AddScoped<DevAdminSeeder>();

        return services;
    }
}
