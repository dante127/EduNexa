using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Structure;

namespace SchoolOS.Application.Structure;

public sealed record AcademicTermDto(
    Guid Id,
    Guid AcademicYearId,
    string Name,
    int Sequence,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status);

internal static class AcademicTermMapping
{
    public static AcademicTermDto ToDto(this AcademicTerm term) =>
        new(term.Id, term.AcademicYearId, term.Name, term.Sequence, term.StartDate, term.EndDate, term.Status.ToString());
}

public sealed record AcademicTermInput(string Name, int Sequence, DateOnly StartDate, DateOnly EndDate);

public sealed record ListAcademicTermsQuery(Guid AcademicYearId) : IRequest<List<AcademicTermDto>>;

public sealed class ListAcademicTermsHandler(IAppDbContext db)
    : IRequestHandler<ListAcademicTermsQuery, List<AcademicTermDto>>
{
    public async Task<List<AcademicTermDto>> Handle(ListAcademicTermsQuery request, CancellationToken cancellationToken)
    {
        var terms = await db.AcademicTerms
            .AsNoTracking()
            .Where(term => term.AcademicYearId == request.AcademicYearId)
            .OrderBy(term => term.Sequence)
            .ToListAsync(cancellationToken);

        return terms.Select(term => term.ToDto()).ToList();
    }
}

public sealed record CreateAcademicTermCommand(
    Guid AcademicYearId,
    string Name,
    int Sequence,
    DateOnly StartDate,
    DateOnly EndDate) : IRequest<AcademicTermDto>;

public sealed class CreateAcademicTermValidator : AbstractValidator<CreateAcademicTermCommand>
{
    public CreateAcademicTermValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Sequence).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("EndDate must be after StartDate.");
    }
}

public sealed class CreateAcademicTermHandler(IAppDbContext db)
    : IRequestHandler<CreateAcademicTermCommand, AcademicTermDto>
{
    public async Task<AcademicTermDto> Handle(CreateAcademicTermCommand request, CancellationToken cancellationToken)
    {
        var year = await db.AcademicYears
            .SingleOrDefaultAsync(y => y.Id == request.AcademicYearId, cancellationToken)
            ?? throw new NotFoundException("AcademicYear", request.AcademicYearId);

        EnsureWithinYear(year, request.StartDate, request.EndDate);

        var duplicate = await db.AcademicTerms
            .AnyAsync(t => t.AcademicYearId == request.AcademicYearId && t.Sequence == request.Sequence, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException(
                "duplicate_term_sequence",
                $"Sequence {request.Sequence} is already used by another term in this academic year.");
        }

        var entity = new AcademicTerm
        {
            Id = Guid.NewGuid(),
            AcademicYearId = request.AcademicYearId,
            Name = request.Name,
            Sequence = request.Sequence,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        db.AcademicTerms.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }

    internal static void EnsureWithinYear(AcademicYear year, DateOnly startDate, DateOnly endDate)
    {
        if (startDate < year.StartDate || endDate > year.EndDate)
        {
            throw new ConflictException(
                "term_outside_year_bounds",
                $"Term dates must fall within the academic year ({year.StartDate:yyyy-MM-dd} .. {year.EndDate:yyyy-MM-dd}).");
        }
    }
}

public sealed record UpdateAcademicTermCommand(
    Guid Id,
    string Name,
    int Sequence,
    DateOnly StartDate,
    DateOnly EndDate) : IRequest<AcademicTermDto>;

public sealed class UpdateAcademicTermValidator : AbstractValidator<UpdateAcademicTermCommand>
{
    public UpdateAcademicTermValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Sequence).GreaterThan(0);
        RuleFor(x => x.EndDate).GreaterThan(x => x.StartDate).WithMessage("EndDate must be after StartDate.");
    }
}

public sealed class UpdateAcademicTermHandler(IAppDbContext db)
    : IRequestHandler<UpdateAcademicTermCommand, AcademicTermDto>
{
    public async Task<AcademicTermDto> Handle(UpdateAcademicTermCommand request, CancellationToken cancellationToken)
    {
        var term = await db.AcademicTerms.SingleOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AcademicTerm", request.Id);

        var year = await db.AcademicYears
            .SingleOrDefaultAsync(y => y.Id == term.AcademicYearId, cancellationToken)
            ?? throw new NotFoundException("AcademicYear", term.AcademicYearId);

        CreateAcademicTermHandler.EnsureWithinYear(year, request.StartDate, request.EndDate);

        var duplicate = await db.AcademicTerms
            .AnyAsync(t => t.AcademicYearId == term.AcademicYearId && t.Sequence == request.Sequence && t.Id != term.Id, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException(
                "duplicate_term_sequence",
                $"Sequence {request.Sequence} is already used by another term in this academic year.");
        }

        term.Name = request.Name;
        term.Sequence = request.Sequence;
        term.StartDate = request.StartDate;
        term.EndDate = request.EndDate;

        await db.SaveChangesAsync(cancellationToken);
        return term.ToDto();
    }
}

public sealed record ActivateAcademicTermCommand(Guid Id) : IRequest<AcademicTermDto>;

public sealed class ActivateAcademicTermHandler(IAppDbContext db)
    : IRequestHandler<ActivateAcademicTermCommand, AcademicTermDto>
{
    public async Task<AcademicTermDto> Handle(ActivateAcademicTermCommand request, CancellationToken cancellationToken)
    {
        var term = await db.AcademicTerms.SingleOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AcademicTerm", request.Id);

        if (term.Status == AcademicPeriodStatus.Active)
        {
            return term.ToDto();
        }

        var year = await db.AcademicYears
            .SingleOrDefaultAsync(y => y.Id == term.AcademicYearId, cancellationToken)
            ?? throw new NotFoundException("AcademicYear", term.AcademicYearId);

        if (year.Status != AcademicPeriodStatus.Active)
        {
            throw new ConflictException("year_not_active", "The academic year must be active before its terms can be activated.");
        }

        var otherActive = await db.AcademicTerms
            .AnyAsync(t => t.AcademicYearId == term.AcademicYearId && t.Status == AcademicPeriodStatus.Active && t.Id != term.Id, cancellationToken);
        if (otherActive)
        {
            throw new ConflictException("another_term_active", "Another term of this academic year is already active.");
        }

        term.Status = AcademicPeriodStatus.Active;
        await db.SaveChangesAsync(cancellationToken);
        return term.ToDto();
    }
}

public sealed record CloseAcademicTermCommand(Guid Id) : IRequest<AcademicTermDto>;

public sealed class CloseAcademicTermHandler(IAppDbContext db)
    : IRequestHandler<CloseAcademicTermCommand, AcademicTermDto>
{
    public async Task<AcademicTermDto> Handle(CloseAcademicTermCommand request, CancellationToken cancellationToken)
    {
        var term = await db.AcademicTerms.SingleOrDefaultAsync(t => t.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AcademicTerm", request.Id);

        if (term.Status == AcademicPeriodStatus.Closed)
        {
            return term.ToDto();
        }

        if (term.Status != AcademicPeriodStatus.Active)
        {
            throw new ConflictException("term_not_active", "Only an active term can be closed.");
        }

        term.Status = AcademicPeriodStatus.Closed;
        await db.SaveChangesAsync(cancellationToken);
        return term.ToDto();
    }
}
