namespace SchoolOS.Infrastructure.Identity;

public interface IJwtTokenService
{
    Task<AccessTokenResult> CreateAccessTokenAsync(AppUser user, CancellationToken cancellationToken = default);
}

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
