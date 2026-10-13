using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Students;

namespace SchoolOS.Application.Students;

public sealed record GuardianDto(
    Guid Id,
    string FullNameEn,
    string FullNameAr,
    string? Phone,
    string? AltPhone,
    string? Email,
    string? Occupation,
    string? Address);

internal static class GuardianMapping
{
    public static GuardianDto ToDto(this Guardian guardian) =>
        new(
            guardian.Id,
            guardian.FullNameEn,
            guardian.FullNameAr,
            guardian.Phone,
            guardian.AltPhone,
            guardian.Email,
            guardian.Occupation,
            guardian.Address);
}

public sealed record ListGuardiansQuery(string? Search) : IRequest<List<GuardianDto>>;

public sealed class ListGuardiansHandler(IAppDbContext db)
    : IRequestHandler<ListGuardiansQuery, List<GuardianDto>>
{
    public async Task<List<GuardianDto>> Handle(ListGuardiansQuery request, CancellationToken cancellationToken)
    {
        var query = db.Guardians.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            var lowered = term.ToLowerInvariant();
            query = query.Where(guardian =>
                guardian.FullNameEn.ToLower().Contains(lowered)
                || guardian.FullNameAr.ToLower().Contains(lowered)
                || (guardian.Phone != null && guardian.Phone.ToLower().Contains(lowered)));
        }

        var guardians = await query
            .OrderBy(guardian => guardian.FullNameEn)
            .Take(100)
            .ToListAsync(cancellationToken);

        return guardians.Select(guardian => guardian.ToDto()).ToList();
    }
}

public sealed record CreateGuardianCommand(
    string FullNameEn,
    string FullNameAr,
    string? Phone,
    string? AltPhone,
    string? Email,
    string? Occupation,
    string? Address) : IRequest<GuardianDto>;

public sealed class CreateGuardianValidator : AbstractValidator<CreateGuardianCommand>
{
    public CreateGuardianValidator()
    {
        RuleFor(x => x.FullNameEn).NotEmpty().MaximumLength(150);
        RuleFor(x => x.FullNameAr).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.AltPhone).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Occupation).MaximumLength(150);
        RuleFor(x => x.Address).MaximumLength(300);
    }
}

public sealed class CreateGuardianHandler(IAppDbContext db)
    : IRequestHandler<CreateGuardianCommand, GuardianDto>
{
    public async Task<GuardianDto> Handle(CreateGuardianCommand request, CancellationToken cancellationToken)
    {
        var guardian = new Guardian
        {
            Id = Guid.NewGuid(),
            FullNameEn = request.FullNameEn,
            FullNameAr = request.FullNameAr,
            Phone = request.Phone,
            AltPhone = request.AltPhone,
            Email = request.Email,
            Occupation = request.Occupation,
            Address = request.Address
        };

        db.Guardians.Add(guardian);
        await db.SaveChangesAsync(cancellationToken);
        return guardian.ToDto();
    }
}

public sealed record UpdateGuardianCommand(
    Guid Id,
    string FullNameEn,
    string FullNameAr,
    string? Phone,
    string? AltPhone,
    string? Email,
    string? Occupation,
    string? Address) : IRequest<GuardianDto>;

public sealed class UpdateGuardianValidator : AbstractValidator<UpdateGuardianCommand>
{
    public UpdateGuardianValidator()
    {
        RuleFor(x => x.FullNameEn).NotEmpty().MaximumLength(150);
        RuleFor(x => x.FullNameAr).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.AltPhone).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Occupation).MaximumLength(150);
        RuleFor(x => x.Address).MaximumLength(300);
    }
}

public sealed class UpdateGuardianHandler(IAppDbContext db)
    : IRequestHandler<UpdateGuardianCommand, GuardianDto>
{
    public async Task<GuardianDto> Handle(UpdateGuardianCommand request, CancellationToken cancellationToken)
    {
        var guardian = await db.Guardians.SingleOrDefaultAsync(g => g.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Guardian", request.Id);

        guardian.FullNameEn = request.FullNameEn;
        guardian.FullNameAr = request.FullNameAr;
        guardian.Phone = request.Phone;
        guardian.AltPhone = request.AltPhone;
        guardian.Email = request.Email;
        guardian.Occupation = request.Occupation;
        guardian.Address = request.Address;

        await db.SaveChangesAsync(cancellationToken);
        return guardian.ToDto();
    }
}

public sealed record LinkGuardianCommand(
    Guid StudentId,
    Guid GuardianId,
    string Relationship,
    bool IsPrimary,
    bool IsEmergencyContact,
    bool IsAuthorizedPickup,
    string? CustodyNotes) : IRequest<StudentGuardianDto>;

public sealed class LinkGuardianValidator : AbstractValidator<LinkGuardianCommand>
{
    public LinkGuardianValidator()
    {
        RuleFor(x => x.Relationship).Must(value => value is "Father" or "Mother" or "Guardian" or "Other")
            .WithMessage("Relationship must be Father, Mother, Guardian or Other.");
        RuleFor(x => x.CustodyNotes).MaximumLength(500);
    }
}

public sealed class LinkGuardianHandler(IAppDbContext db)
    : IRequestHandler<LinkGuardianCommand, StudentGuardianDto>
{
    public async Task<StudentGuardianDto> Handle(LinkGuardianCommand request, CancellationToken cancellationToken)
    {
        var studentExists = await db.Students.AnyAsync(s => s.Id == request.StudentId, cancellationToken);
        if (!studentExists)
        {
            throw new NotFoundException("Student", request.StudentId);
        }

        var guardian = await db.Guardians.SingleOrDefaultAsync(g => g.Id == request.GuardianId, cancellationToken)
            ?? throw new NotFoundException("Guardian", request.GuardianId);

        var existing = await db.StudentGuardians
            .AnyAsync(link => link.StudentId == request.StudentId && link.GuardianId == request.GuardianId, cancellationToken);
        if (existing)
        {
            throw new ConflictException("guardian_already_linked", "This guardian is already linked to the student.");
        }

        if (request.IsPrimary)
        {
            await DemoteOtherPrimariesAsync(db, request.StudentId, null, cancellationToken);
        }

        var link = new StudentGuardian
        {
            StudentId = request.StudentId,
            GuardianId = request.GuardianId,
            Relationship = request.Relationship,
            IsPrimary = request.IsPrimary,
            IsEmergencyContact = request.IsEmergencyContact,
            IsAuthorizedPickup = request.IsAuthorizedPickup,
            CustodyNotes = request.CustodyNotes
        };

        db.StudentGuardians.Add(link);
        await db.SaveChangesAsync(cancellationToken);

        return new StudentGuardianDto(
            guardian.Id,
            guardian.FullNameEn,
            guardian.FullNameAr,
            guardian.Phone,
            link.Relationship,
            link.IsPrimary,
            link.IsEmergencyContact,
            link.IsAuthorizedPickup,
            link.CustodyNotes);
    }

    internal static async Task DemoteOtherPrimariesAsync(
        IAppDbContext db,
        Guid studentId,
        Guid? exceptGuardianId,
        CancellationToken cancellationToken)
    {
        var primaries = await db.StudentGuardians
            .Where(link => link.StudentId == studentId && link.IsPrimary && link.GuardianId != exceptGuardianId)
            .ToListAsync(cancellationToken);

        foreach (var primary in primaries)
        {
            primary.IsPrimary = false;
        }
    }
}

public sealed record UpdateGuardianLinkCommand(
    Guid StudentId,
    Guid GuardianId,
    string Relationship,
    bool IsPrimary,
    bool IsEmergencyContact,
    bool IsAuthorizedPickup,
    string? CustodyNotes) : IRequest<StudentGuardianDto>;

public sealed class UpdateGuardianLinkValidator : AbstractValidator<UpdateGuardianLinkCommand>
{
    public UpdateGuardianLinkValidator()
    {
        RuleFor(x => x.Relationship).Must(value => value is "Father" or "Mother" or "Guardian" or "Other")
            .WithMessage("Relationship must be Father, Mother, Guardian or Other.");
        RuleFor(x => x.CustodyNotes).MaximumLength(500);
    }
}

public sealed class UpdateGuardianLinkHandler(IAppDbContext db)
    : IRequestHandler<UpdateGuardianLinkCommand, StudentGuardianDto>
{
    public async Task<StudentGuardianDto> Handle(UpdateGuardianLinkCommand request, CancellationToken cancellationToken)
    {
        var link = await db.StudentGuardians
            .SingleOrDefaultAsync(
                candidate => candidate.StudentId == request.StudentId && candidate.GuardianId == request.GuardianId,
                cancellationToken)
            ?? throw new NotFoundException("StudentGuardian", $"{request.StudentId}/{request.GuardianId}");

        var guardian = await db.Guardians.SingleOrDefaultAsync(g => g.Id == request.GuardianId, cancellationToken)
            ?? throw new NotFoundException("Guardian", request.GuardianId);

        if (request.IsPrimary)
        {
            await LinkGuardianHandler.DemoteOtherPrimariesAsync(db, request.StudentId, request.GuardianId, cancellationToken);
        }

        link.Relationship = request.Relationship;
        link.IsPrimary = request.IsPrimary;
        link.IsEmergencyContact = request.IsEmergencyContact;
        link.IsAuthorizedPickup = request.IsAuthorizedPickup;
        link.CustodyNotes = request.CustodyNotes;

        await db.SaveChangesAsync(cancellationToken);

        return new StudentGuardianDto(
            guardian.Id,
            guardian.FullNameEn,
            guardian.FullNameAr,
            guardian.Phone,
            link.Relationship,
            link.IsPrimary,
            link.IsEmergencyContact,
            link.IsAuthorizedPickup,
            link.CustodyNotes);
    }
}

public sealed record UnlinkGuardianCommand(Guid StudentId, Guid GuardianId) : IRequest<Unit>;

public sealed class UnlinkGuardianHandler(IAppDbContext db)
    : IRequestHandler<UnlinkGuardianCommand, Unit>
{
    public async Task<Unit> Handle(UnlinkGuardianCommand request, CancellationToken cancellationToken)
    {
        var link = await db.StudentGuardians
            .SingleOrDefaultAsync(
                candidate => candidate.StudentId == request.StudentId && candidate.GuardianId == request.GuardianId,
                cancellationToken)
            ?? throw new NotFoundException("StudentGuardian", $"{request.StudentId}/{request.GuardianId}");

        db.StudentGuardians.Remove(link);
        await db.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
