using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SchoolOS.Api.ErrorHandling;
using SchoolOS.Application;
using SchoolOS.Infrastructure;
using SchoolOS.Infrastructure.Identity;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog(configuration => configuration
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console());

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// Rate limiting: named policy "auth", partitioned per client IP, limits configurable.
var authPermitLimit = builder.Configuration.GetValue("RateLimiting:Auth:PermitLimit", 10);
var authWindowSeconds = builder.Configuration.GetValue("RateLimiting:Auth:WindowSeconds", 60);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromSeconds(authWindowSeconds),
                QueueLimit = 0
            }));
});

// Authentication: JWT bearer (HS256); access tokens are issued by the auth endpoints.
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "SchoolOS";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "SchoolOS.Client";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"] ?? string.Empty;
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = jwtSigningKey.Length >= 32
                ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey))
                : null,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = jwtSigningKey.Length >= 32,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

// One policy per permission code ("perm:<code>"); endpoints require the matching policy.
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in SchoolOsPermissions.All)
    {
        options.AddPolicy($"perm:{permission.Code}", policy =>
            policy.RequireAuthenticatedUser().RequireClaim("permission", permission.Code));
    }
});

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live");
app.MapHealthChecks("/health/ready");

// Dev-only bootstrap (no-op unless Seed:DevAdmin:Enabled; refuses in Production).
try
{
    using var scope = app.Services.CreateScope();
    var seeder = scope.ServiceProvider.GetRequiredService<DevAdminSeeder>();
    await seeder.SeedAsync();
}
catch (Exception ex)
{
    Log.Warning(ex, "Dev admin seeding failed; continuing without seeding.");
}

app.Run();

public partial class Program { }
