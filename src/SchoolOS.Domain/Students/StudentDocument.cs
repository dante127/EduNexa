namespace SchoolOS.Domain.Students;

public sealed class StudentDocument
{
    public Guid Id { get; set; }

    public Guid StudentId { get; set; }

    public DocumentType DocumentType { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string MimeType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string StoragePath { get; set; } = string.Empty;

    public DocumentVerificationStatus VerificationStatus { get; set; } = DocumentVerificationStatus.Pending;

    public Guid? VerifiedByUserId { get; set; }

    public DateTimeOffset? VerifiedAt { get; set; }

    public Guid UploadedByUserId { get; set; }

    public DateTimeOffset UploadedAt { get; set; }

    public string? Notes { get; set; }

    /// <summary>Soft delete only (docs/database-design.md); the stored file is swept later.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
