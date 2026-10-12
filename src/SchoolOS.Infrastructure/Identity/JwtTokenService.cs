using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SchoolOS.Infrastructure.Persistence;

namespace SchoolOS.Infrastructure.Identity;

/// <summary>
/// Issues short-lived HS256 access tokens carrying role and permission claims
/// (docs/architecture.md §5). Permission claims are resolved from the
/// identity.role_permissions ⋈ identity.permissions tables.
/// </summary>
public sealed class JwtTokenService(
    UserManager<AppUser> userManager,
    AppDbContext dbContext,
    IOptions<JwtOptions> options) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public async Task<AccessTokenResult> CreateAccessTokenAsync(
        AppUser user,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.SigningKey) || _options.SigningKey.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:SigningKey is not configured (minimum 32 characters). " +
                "Set the Jwt__SigningKey environment variable or the Jwt:SigningKey configuration value.");
        }

        var roles = await userManager.GetRolesAsync(user);

        var permissionCodes = await (
                from rolePermission in dbContext.RolePermissions
                join permission in dbContext.Permissions on rolePermission.PermissionId equals permission.Id
                join role in dbContext.Roles on rolePermission.RoleId equals role.Id
                where roles.Contains(role.Name!)
                select permission.Code)
            .Distinct()
            .ToListAsync(cancellationToken);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("email", user.Email ?? string.Empty),
            new("name", user.UserName ?? string.Empty),
            new("jti", Guid.NewGuid().ToString("N"))
        };
        claims.AddRange(roles.Select(role => new Claim("role", role)));
        claims.AddRange(permissionCodes.Select(code => new Claim("permission", code)));

        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
