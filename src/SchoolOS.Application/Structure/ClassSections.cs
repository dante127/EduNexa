using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Structure;

namespace SchoolOS.Application.Structure;

public sealed record ClassSectionDto(Guid Id, Guid AcademicYearId, Guid GradeLevelId, string Name, int Capacity);

internal static class ClassSectionMapping
{
    public static ClassSectionDto ToDto(this ClassSection section) =>
        new(section.Id, section.AcademicYearId, section.GradeLevelId, section.Name, section.Capacity);
}

public sealed record ListClassSectionsQuery(Guid? AcademicYearId, Guid? GradeLevelId) : IRequest<List<ClassSectionDto>>;

public sealed class ListClassSectionsHandler(IAppDbContext db)
    : IRequestHandler<ListClassSectionsQuery, List<ClassSectionDto>>
{
    public async Task<List<ClassSectionDto>> Handle(ListClassSectionsQuery request, CancellationToken cancellationToken)
    {
        var query = db.ClassSections.AsNoTracking();

        if (request.AcademicYearId is not null)
        {
            query = query.Where(section => section.AcademicYearId == request.AcademicYearId);
        }

        if (request.GradeLevelId is not null)
        {
            query = query.Where(section => section.GradeLevelId == request.GradeLevelId);
        }

        var sections = await query
            .OrderBy(section => section.Name)
            .ToListAsync(cancellationToken);

        return sections.Select(section => section.ToDto()).ToList();
    }
}

public sealed record CreateClassSectionCommand(Guid AcademicYearId, Guid GradeLevelId, string Name, int Capacity)
    : IRequest<ClassSectionDto>;

public sealed class CreateClassSectionValidator : AbstractValidator<CreateClassSectionCommand>
{
    public CreateClassSectionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Capacity).GreaterThan(0);
    }
}

public sealed class CreateClassSectionHandler(IAppDbContext db)
    : IRequestHandler<CreateClassSectionCommand, ClassSectionDto>
{
    public async Task<ClassSectionDto> Handle(CreateClassSectionCommand request, CancellationToken cancellationToken)
    {
        var yearExists = await db.AcademicYears.AnyAsync(year => year.Id == request.AcademicYearId, cancellationToken);
        if (!yearExists)
        {
            throw new NotFoundException("AcademicYear", request.AcademicYearId);
        }

        var gradeLevelExists = await db.GradeLevels.AnyAsync(level => level.Id == request.GradeLevelId, cancellationToken);
        if (!gradeLevelExists)
        {
            throw new NotFoundException("GradeLevel", request.GradeLevelId);
        }

        var duplicate = await db.ClassSections.AnyAsync(section =>
            section.AcademicYearId == request.AcademicYearId
            && section.GradeLevelId == request.GradeLevelId
            && section.Name == request.Name, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException(
                "duplicate_section",
                $"A section named '{request.Name}' already exists for this grade level in this academic year.");
        }

        var entity = new ClassSection
        {
            Id = Guid.NewGuid(),
            AcademicYearId = request.AcademicYearId,
            GradeLevelId = request.GradeLevelId,
            Name = request.Name,
            Capacity = request.Capacity
        };

        db.ClassSections.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record UpdateClassSectionCommand(Guid Id, string Name, int Capacity) : IRequest<ClassSectionDto>;

public sealed class UpdateClassSectionValidator : AbstractValidator<UpdateClassSectionCommand>
{
    public UpdateClassSectionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Capacity).GreaterThan(0);
    }
}

public sealed class UpdateClassSectionHandler(IAppDbContext db)
    : IRequestHandler<UpdateClassSectionCommand, ClassSectionDto>
{
    public async Task<ClassSectionDto> Handle(UpdateClassSectionCommand request, CancellationToken cancellationToken)
    {
        var section = await db.ClassSections.SingleOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("ClassSection", request.Id);

        var duplicate = await db.ClassSections.AnyAsync(s =>
            s.AcademicYearId == section.AcademicYearId
            && s.GradeLevelId == section.GradeLevelId
            && s.Name == request.Name
            && s.Id != section.Id, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException(
                "duplicate_section",
                $"A section named '{request.Name}' already exists for this grade level in this academic year.");
        }

        section.Name = request.Name;
        section.Capacity = request.Capacity;

        await db.SaveChangesAsync(cancellationToken);
        return section.ToDto();
    }
}
