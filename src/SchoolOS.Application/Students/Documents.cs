using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Students;

namespace SchoolOS.Application.Students;

/// <summary>
/// Upload rules for student documents (docs: uploads are validated by type, size, and path).
/// Storage paths are always server-generated; client file names are display-only.
/// </summary>
public static class StudentDocumentRules
{
    private static readonly Dictionary<string, string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["application/pdf"] = ".pdf",
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp"
    };

    public static string? ExtensionFor(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return null;
        }

        var normalized = contentType.Split(';')[0].Trim();
        return AllowedMimeTypes.TryGetValue(normalized, out var extension) ? extension : null;
    }

    public static bool IsAllowedMimeType(string? contentType) => ExtensionFor(contentType) is not null;

    public static bool IsValidDocumentType(string? value) =>
        Enum.TryParse<DocumentType>(value, ignoreCase: true, out _);
}

public sealed record StudentDocumentDto(
    Guid Id,
    string DocumentType,
    string FileName,
    string MimeType,
    long SizeBytes,
    string VerificationStatus,
    Guid? VerifiedByUserId,
    DateTimeOffset? VerifiedAt,
    string? Notes,
    Guid UploadedByUserId,
    DateTimeOffset UploadedAt);

internal static class StudentDocumentMapping
{
    public static StudentDocumentDto ToDto(this StudentDocument document) =>
        new(
            document.Id,
            document.DocumentType.ToString(),
            document.FileName,
            document.MimeType,
            document.SizeBytes,
            document.VerificationStatus.ToString(),
            document.VerifiedByUserId,
            document.VerifiedAt,
            document.Notes,
            document.UploadedByUserId,
            document.UploadedAt);
}

public sealed record ListStudentDocumentsQuery(Guid StudentId) : IRequest<List<StudentDocumentDto>>;

public sealed class ListStudentDocumentsHandler(IAppDbContext db)
    : IRequestHandler<ListStudentDocumentsQuery, List<StudentDocumentDto>>
{
    public async Task<List<StudentDocumentDto>> Handle(ListStudentDocumentsQuery request, CancellationToken cancellationToken)
    {
        var studentExists = await db.Students.AnyAsync(student => student.Id == request.StudentId, cancellationToken);
        if (!studentExists)
        {
            throw new NotFoundException("Student", request.StudentId);
        }

        var documents = await db.StudentDocuments.AsNoTracking()
            .Where(document => document.StudentId == request.StudentId && document.DeletedAt == null)
            .OrderByDescending(document => document.UploadedAt)
            .ToListAsync(cancellationToken);

        return documents.Select(document => document.ToDto()).ToList();
    }
}

public sealed record UploadStudentDocumentCommand(
    Guid StudentId,
    string DocumentType,
    string FileName,
    string ContentType,
    long Size,
    Stream Content,
    Guid UploadedByUserId,
    string? Notes) : IRequest<StudentDocumentDto>;

public sealed class UploadStudentDocumentValidator : AbstractValidator<UploadStudentDocumentCommand>
{
    public UploadStudentDocumentValidator(IOptions<StorageOptions> storageOptions)
    {
        var maxBytes = storageOptions.Value.MaxFileSizeBytes;

        RuleFor(x => x.DocumentType)
            .Must(StudentDocumentRules.IsValidDocumentType)
            .WithMessage("Document type is not valid.");
        RuleFor(x => x.FileName).NotEmpty().MaximumLength(255);
        RuleFor(x => x.ContentType)
            .Must(StudentDocumentRules.IsAllowedMimeType)
            .WithMessage("File type is not allowed (allowed: PDF, JPEG, PNG, WebP).");
        RuleFor(x => x.Size)
            .GreaterThan(0).WithMessage("File is empty.")
            .LessThanOrEqualTo(maxBytes).WithMessage($"File exceeds the maximum size of {maxBytes} bytes.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class UploadStudentDocumentHandler(IAppDbContext db, IFileStorage storage)
    : IRequestHandler<UploadStudentDocumentCommand, StudentDocumentDto>
{
    public async Task<StudentDocumentDto> Handle(UploadStudentDocumentCommand request, CancellationToken cancellationToken)
    {
        var studentExists = await db.Students.AnyAsync(student => student.Id == request.StudentId, cancellationToken);
        if (!studentExists)
        {
            throw new NotFoundException("Student", request.StudentId);
        }

        var extension = StudentDocumentRules.ExtensionFor(request.ContentType)
            ?? throw new ConflictException("document_type_not_allowed", "File type is not allowed.");
        var mimeType = request.ContentType.Split(';')[0].Trim();

        // Server-generated relative path; the client file name never touches the filesystem.
        var documentId = Guid.NewGuid();
        var relativePath = $"students/{request.StudentId:N}/{documentId:N}{extension}";
        await storage.SaveAsync(request.Content, relativePath, cancellationToken);

        var document = new StudentDocument
        {
            Id = documentId,
            StudentId = request.StudentId,
            DocumentType = Enum.Parse<DocumentType>(request.DocumentType, ignoreCase: true),
            FileName = request.FileName.Trim(),
            MimeType = mimeType,
            SizeBytes = request.Size,
            StoragePath = relativePath,
            VerificationStatus = DocumentVerificationStatus.Pending,
            UploadedByUserId = request.UploadedByUserId,
            UploadedAt = DateTimeOffset.UtcNow,
            Notes = request.Notes
        };

        db.StudentDocuments.Add(document);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            // Do not leave the stored file behind if the row cannot be persisted.
            await storage.DeleteAsync(relativePath, CancellationToken.None);
            throw;
        }

        return document.ToDto();
    }
}

public sealed record VerifyStudentDocumentCommand(
    Guid StudentId,
    Guid DocumentId,
    string Status,
    string? Notes,
    Guid VerifiedByUserId) : IRequest<StudentDocumentDto>;

public sealed class VerifyStudentDocumentValidator : AbstractValidator<VerifyStudentDocumentCommand>
{
    public VerifyStudentDocumentValidator()
    {
        RuleFor(x => x.Status)
            .Must(status => status is "Verified" or "Rejected")
            .WithMessage("Status must be 'Verified' or 'Rejected'.");
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class VerifyStudentDocumentHandler(IAppDbContext db)
    : IRequestHandler<VerifyStudentDocumentCommand, StudentDocumentDto>
{
    public async Task<StudentDocumentDto> Handle(VerifyStudentDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await db.StudentDocuments
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.DocumentId
                    && candidate.StudentId == request.StudentId
                    && candidate.DeletedAt == null,
                cancellationToken)
            ?? throw new NotFoundException("StudentDocument", request.DocumentId);

        if (document.VerificationStatus != DocumentVerificationStatus.Pending)
        {
            throw new ConflictException("document_not_pending", "Only pending documents can be verified or rejected.");
        }

        document.VerificationStatus = Enum.Parse<DocumentVerificationStatus>(request.Status, ignoreCase: true);
        document.VerifiedByUserId = request.VerifiedByUserId;
        document.VerifiedAt = DateTimeOffset.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            document.Notes = request.Notes;
        }

        await db.SaveChangesAsync(cancellationToken);
        return document.ToDto();
    }
}

public sealed record DeleteStudentDocumentCommand(Guid StudentId, Guid DocumentId) : IRequest;

public sealed class DeleteStudentDocumentHandler(IAppDbContext db)
    : IRequestHandler<DeleteStudentDocumentCommand>
{
    public async Task Handle(DeleteStudentDocumentCommand request, CancellationToken cancellationToken)
    {
        var document = await db.StudentDocuments
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.DocumentId
                    && candidate.StudentId == request.StudentId
                    && candidate.DeletedAt == null,
                cancellationToken)
            ?? throw new NotFoundException("StudentDocument", request.DocumentId);

        document.DeletedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed record StudentDocumentDownload(Stream Content, string FileName, string MimeType);

public sealed record GetStudentDocumentDownloadQuery(Guid StudentId, Guid DocumentId)
    : IRequest<StudentDocumentDownload>;

public sealed class GetStudentDocumentDownloadHandler(IAppDbContext db, IFileStorage storage)
    : IRequestHandler<GetStudentDocumentDownloadQuery, StudentDocumentDownload>
{
    public async Task<StudentDocumentDownload> Handle(GetStudentDocumentDownloadQuery request, CancellationToken cancellationToken)
    {
        var document = await db.StudentDocuments.AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == request.DocumentId
                    && candidate.StudentId == request.StudentId
                    && candidate.DeletedAt == null,
                cancellationToken)
            ?? throw new NotFoundException("StudentDocument", request.DocumentId);

        var content = await storage.OpenReadAsync(document.StoragePath, cancellationToken)
            ?? throw new NotFoundException("StudentDocumentFile", document.Id);

        return new StudentDocumentDownload(content, document.FileName, document.MimeType);
    }
}
