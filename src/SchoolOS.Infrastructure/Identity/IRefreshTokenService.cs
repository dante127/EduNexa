namespace SchoolOS.Infrastructure.Identity;

public interface IRefreshTokenService
{
    Task<string> CreateAsync(Guid userId, string? createdByIp, string? userAgent, CancellationToken cancellationToken = default);

    Task<RefreshTokenValidationResult> ValidateAndRotateAsync(string token, string? createdByIp, string? userAgent, CancellationToken cancellationToken = default);

    Task<bool> RevokeAsync(string token, CancellationToken cancellationToken = default);

    Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public enum RefreshTokenValidationStatus
{
    Success,
    InvalidOrExpired,
    ReuseDetected
}

public sealed record RefreshTokenValidationResult(RefreshTokenValidationStatus Status, string? NewToken, Guid? UserId);
