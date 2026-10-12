using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Structure;

namespace SchoolOS.Application.Structure;

public sealed record AcademicYearDto(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate, string Status);

internal static class AcademicYearMapping
{
    public static AcademicYearDto ToDto(this AcademicYear year) =>
        new(year.Id, year.Name, year.StartDate, year.EndDate, year.Status.ToString());
}

public sealed record ListAcademicYearsQuery : IRequest<List<AcademicYearDto>>;

public sealed class ListAcademicYearsHandler(IAppDbContext db)
    : IRequestHandler<ListAcademicYearsQuery, List<AcademicYearDto>>
{
    public async Task<List<AcademicYearDto>> Handle(ListAcademicYearsQuery request, CancellationToken cancellationToken)
    {
        var years = await db.AcademicYears
            .AsNoTracking()
            .OrderByDescending(year => year.StartDate)
            .ToListAsync(cancellationToken);

        return years.Select(year => year.ToDto()).ToList();
    }
}

public sealed record GetAcademicYearQuery(Guid Id) : IRequest<AcademicYearDto>;

public sealed class GetAcademicYearHandler(IAppDbContext db)
    : IRequestHandler<GetAcademicYearQuery, AcademicYearDto>
{
    public async Task<AcademicYearDto> Handle(GetAcademicYearQuery request, CancellationToken cancellationToken)
    {
        var year = await db.AcademicYears.AsNoTracking()
            .SingleOrDefaultAsync(y => y.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AcademicYear", request.Id);

        return year.ToDto();
    }
}

public sealed record CreateAcademicYearCommand(string Name, DateOnly StartDate, DateOnly EndDate) : IRequest<AcademicYearDto>;

public sealed class CreateAcademicYearValidator : AbstractValidator<CreateAcademicYearCommand>
{
    public CreateAcademicYearValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(20);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("EndDate must be after StartDate.");
    }
}

public sealed class CreateAcademicYearHandler(IAppDbContext db)
    : IRequestHandler<CreateAcademicYearCommand, AcademicYearDto>
{
    public async Task<AcademicYearDto> Handle(CreateAcademicYearCommand request, CancellationToken cancellationToken)
    {
        var nameTaken = await db.AcademicYears.AnyAsync(year => year.Name == request.Name, cancellationToken);
        if (nameTaken)
        {
            throw new ConflictException("duplicate_year_name", $"An academic year named '{request.Name}' already exists.");
        }

        var entity = new AcademicYear
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        db.AcademicYears.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record UpdateAcademicYearCommand(Guid Id, string Name, DateOnly StartDate, DateOnly EndDate) : IRequest<AcademicYearDto>;

public sealed class UpdateAcademicYearValidator : AbstractValidator<UpdateAcademicYearCommand>
{
    public UpdateAcademicYearValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(20);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("EndDate must be after StartDate.");
    }
}

public sealed class UpdateAcademicYearHandler(IAppDbContext db)
    : IRequestHandler<UpdateAcademicYearCommand, AcademicYearDto>
{
    public async Task<AcademicYearDto> Handle(UpdateAcademicYearCommand request, CancellationToken cancellationToken)
    {
        var year = await db.AcademicYears.SingleOrDefaultAsync(y => y.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AcademicYear", request.Id);

        var nameTaken = await db.AcademicYears
            .AnyAsync(y => y.Name == request.Name && y.Id != request.Id, cancellationToken);
        if (nameTaken)
        {
            throw new ConflictException("duplicate_year_name", $"An academic year named '{request.Name}' already exists.");
        }

        year.Name = request.Name;
        year.StartDate = request.StartDate;
        year.EndDate = request.EndDate;

        await db.SaveChangesAsync(cancellationToken);
        return year.ToDto();
    }
}

public sealed record ActivateAcademicYearCommand(Guid Id) : IRequest<AcademicYearDto>;

public sealed class ActivateAcademicYearHandler(IAppDbContext db)
    : IRequestHandler<ActivateAcademicYearCommand, AcademicYearDto>
{
    public async Task<AcademicYearDto> Handle(ActivateAcademicYearCommand request, CancellationToken cancellationToken)
    {
        var year = await db.AcademicYears.SingleOrDefaultAsync(y => y.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AcademicYear", request.Id);

        if (year.Status == AcademicPeriodStatus.Active)
        {
            return year.ToDto();
        }

        var otherActive = await db.AcademicYears
            .AnyAsync(y => y.Status == AcademicPeriodStatus.Active && y.Id != request.Id, cancellationToken);
        if (otherActive)
        {
            throw new ConflictException(
                "another_year_active",
                "Another academic year is already active. Close it before activating this one.");
        }

        year.Status = AcademicPeriodStatus.Active;
        await db.SaveChangesAsync(cancellationToken);
        return year.ToDto();
    }
}

public sealed record CloseAcademicYearCommand(Guid Id) : IRequest<AcademicYearDto>;

public sealed class CloseAcademicYearHandler(IAppDbContext db)
    : IRequestHandler<CloseAcademicYearCommand, AcademicYearDto>
{
    public async Task<AcademicYearDto> Handle(CloseAcademicYearCommand request, CancellationToken cancellationToken)
    {
        var year = await db.AcademicYears.SingleOrDefaultAsync(y => y.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AcademicYear", request.Id);

        if (year.Status == AcademicPeriodStatus.Closed)
        {
            return year.ToDto();
        }

        if (year.Status != AcademicPeriodStatus.Active)
        {
            throw new ConflictException("year_not_active", "Only an active academic year can be closed.");
        }

        year.Status = AcademicPeriodStatus.Closed;
        await db.SaveChangesAsync(cancellationToken);
        return year.ToDto();
    }
}
