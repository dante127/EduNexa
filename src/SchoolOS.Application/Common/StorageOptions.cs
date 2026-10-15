namespace SchoolOS.Application.Common;

/// <summary>
/// File storage configuration. <see cref="Root"/> is interpreted by the infrastructure
/// implementation relative to the content root when not an absolute path.
/// </summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Root { get; set; } = "storage";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
