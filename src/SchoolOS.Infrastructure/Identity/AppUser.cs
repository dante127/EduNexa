using Microsoft.AspNetCore.Identity;

namespace SchoolOS.Infrastructure.Identity;

/// <summary>
/// Application user. Extends ASP.NET Core Identity with the SchoolOS fields
/// defined in docs/database-design.md §2 (status, preferred language, last login).
/// </summary>
public sealed class AppUser : IdentityUser<Guid>
{
    public string Status { get; set; } = UserStatuses.Active;

    public string PreferredLanguage { get; set; } = "en";

    public DateTimeOffset? LastLoginAt { get; set; }
}

public static class UserStatuses
{
    public const string Invited = "Invited";
    public const string Active = "Active";
    public const string Deactivated = "Deactivated";
}
