namespace SchoolOS.Infrastructure.Identity;

/// <summary>
/// Opaque refresh token metadata for the JWT auth flow (docs/architecture.md §5).
/// Only the hash of the token value is stored; rotation chains via <see cref="ReplacedById"/>.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedById { get; set; }

    public System.Net.IPAddress? CreatedByIp { get; set; }

    public string? UserAgent { get; set; }
}
