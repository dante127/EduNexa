using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Structure;

namespace SchoolOS.Application.Structure;

public sealed record GradeLevelDto(Guid Id, int Sequence, string NameEn, string NameAr);

internal static class GradeLevelMapping
{
    public static GradeLevelDto ToDto(this GradeLevel gradeLevel) =>
        new(gradeLevel.Id, gradeLevel.Sequence, gradeLevel.NameEn, gradeLevel.NameAr);
}

public sealed record ListGradeLevelsQuery : IRequest<List<GradeLevelDto>>;

public sealed class ListGradeLevelsHandler(IAppDbContext db)
    : IRequestHandler<ListGradeLevelsQuery, List<GradeLevelDto>>
{
    public async Task<List<GradeLevelDto>> Handle(ListGradeLevelsQuery request, CancellationToken cancellationToken)
    {
        var gradeLevels = await db.GradeLevels
            .AsNoTracking()
            .OrderBy(level => level.Sequence)
            .ToListAsync(cancellationToken);

        return gradeLevels.Select(level => level.ToDto()).ToList();
    }
}

public sealed record CreateGradeLevelCommand(int Sequence, string NameEn, string NameAr) : IRequest<GradeLevelDto>;

public sealed class CreateGradeLevelValidator : AbstractValidator<CreateGradeLevelCommand>
{
    public CreateGradeLevelValidator()
    {
        RuleFor(x => x.Sequence).GreaterThan(0);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateGradeLevelHandler(IAppDbContext db)
    : IRequestHandler<CreateGradeLevelCommand, GradeLevelDto>
{
    public async Task<GradeLevelDto> Handle(CreateGradeLevelCommand request, CancellationToken cancellationToken)
    {
        var duplicate = await db.GradeLevels.AnyAsync(level => level.Sequence == request.Sequence, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("duplicate_grade_level_sequence", $"Grade level sequence {request.Sequence} already exists.");
        }

        var entity = new GradeLevel
        {
            Id = Guid.NewGuid(),
            Sequence = request.Sequence,
            NameEn = request.NameEn,
            NameAr = request.NameAr
        };

        db.GradeLevels.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record UpdateGradeLevelCommand(Guid Id, int Sequence, string NameEn, string NameAr) : IRequest<GradeLevelDto>;

public sealed class UpdateGradeLevelValidator : AbstractValidator<UpdateGradeLevelCommand>
{
    public UpdateGradeLevelValidator()
    {
        RuleFor(x => x.Sequence).GreaterThan(0);
        RuleFor(x => x.NameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.NameAr).NotEmpty().MaximumLength(100);
    }
}

public sealed class UpdateGradeLevelHandler(IAppDbContext db)
    : IRequestHandler<UpdateGradeLevelCommand, GradeLevelDto>
{
    public async Task<GradeLevelDto> Handle(UpdateGradeLevelCommand request, CancellationToken cancellationToken)
    {
        var gradeLevel = await db.GradeLevels.SingleOrDefaultAsync(level => level.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("GradeLevel", request.Id);

        var duplicate = await db.GradeLevels
            .AnyAsync(level => level.Sequence == request.Sequence && level.Id != request.Id, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("duplicate_grade_level_sequence", $"Grade level sequence {request.Sequence} already exists.");
        }

        gradeLevel.Sequence = request.Sequence;
        gradeLevel.NameEn = request.NameEn;
        gradeLevel.NameAr = request.NameAr;

        await db.SaveChangesAsync(cancellationToken);
        return gradeLevel.ToDto();
    }
}
