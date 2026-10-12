using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolOS.Infrastructure.Persistence;

namespace SchoolOS.Infrastructure.Identity;

/// <summary>
/// Opaque rotating refresh tokens (docs/security-plan.md §2): only the SHA-256 hash is stored;
/// rotation links old -> new via ReplacedById; presenting an already-revoked token revokes
/// all active sessions for the user (reuse detection).
/// </summary>
public sealed class RefreshTokenService(
    AppDbContext dbContext,
    IOptions<JwtOptions> options) : IRefreshTokenService
{
    private readonly JwtOptions _options = options.Value;

    public async Task<string> CreateAsync(
        Guid userId,
        string? createdByIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var (rawToken, entity) = CreateTokenEntity(userId, createdByIp, userAgent);
        dbContext.RefreshTokens.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return rawToken;
    }

    public async Task<RefreshTokenValidationResult> ValidateAndRotateAsync(
        string token,
        string? createdByIp,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        var hash = Hash(token);
        var existing = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing is null)
        {
            return new RefreshTokenValidationResult(RefreshTokenValidationStatus.InvalidOrExpired, null, null);
        }

        if (existing.RevokedAt is not null)
        {
            // Reuse of a revoked token: treat as compromise; revoke every active session for the user.
            await RevokeAllForUserAsync(existing.UserId, cancellationToken);
            return new RefreshTokenValidationResult(RefreshTokenValidationStatus.ReuseDetected, null, existing.UserId);
        }

        if (existing.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            return new RefreshTokenValidationResult(RefreshTokenValidationStatus.InvalidOrExpired, null, existing.UserId);
        }

        var (newRawToken, newEntity) = CreateTokenEntity(existing.UserId, createdByIp, userAgent);
        existing.RevokedAt = DateTimeOffset.UtcNow;
        existing.ReplacedById = newEntity.Id;
        dbContext.RefreshTokens.Add(newEntity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RefreshTokenValidationResult(RefreshTokenValidationStatus.Success, newRawToken, existing.UserId);
    }

    public async Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default)
    {
        var hash = Hash(token);
        var existing = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        if (existing.RevokedAt is null)
        {
            existing.RevokedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, now), cancellationToken);
    }

    private (string RawToken, RefreshToken Entity) CreateTokenEntity(Guid userId, string? createdByIp, string? userAgent)
    {
        var rawToken = GenerateRawToken();
        var now = DateTimeOffset.UtcNow;
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = Hash(rawToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_options.RefreshTokenDays),
            CreatedByIp = IPAddress.TryParse(createdByIp, out var ip) ? ip : null,
            UserAgent = userAgent is { Length: > 512 } ? userAgent[..512] : userAgent
        };
        return (rawToken, entity);
    }

    private static string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
