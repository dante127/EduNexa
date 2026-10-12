using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Structure;

namespace SchoolOS.Application.Structure;

public sealed record SubjectDto(Guid Id, string Code, string NameEn, string NameAr, bool IsActive);

internal static class SubjectMapping
{
    public static SubjectDto ToDto(this Subject subject) =>
        new(subject.Id, subject.Code, subject.NameEn, subject.NameAr, subject.IsActive);
}

public sealed record ListSubjectsQuery : IRequest<List<SubjectDto>>;

public sealed class ListSubjectsHandler(IAppDbContext db)
    : IRequestHandler<ListSubjectsQuery, List<SubjectDto>>
{
    public async Task<List<SubjectDto>> Handle(ListSubjectsQuery request, CancellationToken cancellationToken)
    {
        var subjects = await db.Subjects
            .AsNoTracking()
            .OrderBy(subject => subject.Code)
            .ToListAsync(cancellationToken);

        return subjects.Select(subject => subject.ToDto()).ToList();
    }
}

public sealed record CreateSubjectCommand(string Code, string NameEn, string NameAr) : IRequest<SubjectDto>;

public sealed class CreateSubjectValidator : AbstractValidator<CreateSubjectCommand>
{
    public CreateSubjectValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(150);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(150);
    }
}

public sealed class CreateSubjectHandler(IAppDbContext db)
    : IRequestHandler<CreateSubjectCommand, SubjectDto>
{
    public async Task<SubjectDto> Handle(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        var duplicate = await db.Subjects.AnyAsync(subject => subject.Code == request.Code, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("duplicate_subject_code", $"A subject with code '{request.Code}' already exists.");
        }

        var entity = new Subject
        {
            Id = Guid.NewGuid(),
            Code = request.Code,
            NameEn = request.NameEn,
            NameAr = request.NameAr
        };

        db.Subjects.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record UpdateSubjectCommand(Guid Id, string Code, string NameEn, string NameAr, bool IsActive) : IRequest<SubjectDto>;

public sealed class UpdateSubjectValidator : AbstractValidator<UpdateSubjectCommand>
{
    public UpdateSubjectValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(150);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(150);
    }
}

public sealed class UpdateSubjectHandler(IAppDbContext db)
    : IRequestHandler<UpdateSubjectCommand, SubjectDto>
{
    public async Task<SubjectDto> Handle(UpdateSubjectCommand request, CancellationToken cancellationToken)
    {
        var subject = await db.Subjects.SingleOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Subject", request.Id);

        var duplicate = await db.Subjects
            .AnyAsync(s => s.Code == request.Code && s.Id != request.Id, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("duplicate_subject_code", $"A subject with code '{request.Code}' already exists.");
        }

        subject.Code = request.Code;
        subject.NameEn = request.NameEn;
        subject.NameAr = request.NameAr;
        subject.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
        return subject.ToDto();
    }
}
