namespace SchoolOS.Domain.Structure;

/// <summary>
/// School profile &amp; settings — a singleton row per deployment (docs/database-design.md §3).
/// </summary>
public sealed class School
{
    public static readonly Guid SingletonId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public Guid Id { get; set; }

    public string NameEn { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string Timezone { get; set; } = "UTC";

    public string DefaultLocale { get; set; } = "en";

    public string CurrencyCode { get; set; } = "USD";

    public string? Address { get; set; }

    public string? LogoPath { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }
}
