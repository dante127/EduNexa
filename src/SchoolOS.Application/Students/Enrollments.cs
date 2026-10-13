using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Students;

namespace SchoolOS.Application.Students;

public sealed record EnrollmentDto(
    Guid Id,
    Guid StudentId,
    Guid AcademicYearId,
    Guid GradeLevelId,
    Guid ClassSectionId,
    string Status,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Notes);

internal static class EnrollmentMapping
{
    public static EnrollmentDto ToDto(this Enrollment enrollment) =>
        new(
            enrollment.Id,
            enrollment.StudentId,
            enrollment.AcademicYearId,
            enrollment.GradeLevelId,
            enrollment.ClassSectionId,
            enrollment.Status.ToString(),
            enrollment.StartDate,
            enrollment.EndDate,
            enrollment.Notes);
}

public sealed record ListStudentEnrollmentsQuery(Guid StudentId) : IRequest<List<EnrollmentDto>>;

public sealed class ListStudentEnrollmentsHandler(IAppDbContext db)
    : IRequestHandler<ListStudentEnrollmentsQuery, List<EnrollmentDto>>
{
    public async Task<List<EnrollmentDto>> Handle(ListStudentEnrollmentsQuery request, CancellationToken cancellationToken)
    {
        var enrollments = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.StudentId == request.StudentId)
            .OrderByDescending(enrollment => enrollment.StartDate)
            .ToListAsync(cancellationToken);

        return enrollments.Select(EnrollmentMapping.ToDto).ToList();
    }
}

public sealed record CreateEnrollmentCommand(
    Guid StudentId,
    Guid AcademicYearId,
    Guid GradeLevelId,
    Guid ClassSectionId,
    DateOnly StartDate,
    string? Notes) : IRequest<EnrollmentDto>;

public sealed class CreateEnrollmentValidator : AbstractValidator<CreateEnrollmentCommand>
{
    public CreateEnrollmentValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class CreateEnrollmentHandler(IAppDbContext db)
    : IRequestHandler<CreateEnrollmentCommand, EnrollmentDto>
{
    public async Task<EnrollmentDto> Handle(CreateEnrollmentCommand request, CancellationToken cancellationToken)
    {
        var student = await db.Students.SingleOrDefaultAsync(s => s.Id == request.StudentId, cancellationToken)
            ?? throw new NotFoundException("Student", request.StudentId);

        if (student.Status == StudentStatus.Graduated)
        {
            throw new ConflictException("student_graduated", "A graduated student cannot be enrolled again.");
        }

        var yearExists = await db.AcademicYears.AnyAsync(year => year.Id == request.AcademicYearId, cancellationToken);
        if (!yearExists)
        {
            throw new NotFoundException("AcademicYear", request.AcademicYearId);
        }

        var section = await db.ClassSections
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ClassSectionId, cancellationToken)
            ?? throw new NotFoundException("ClassSection", request.ClassSectionId);

        if (section.AcademicYearId != request.AcademicYearId || section.GradeLevelId != request.GradeLevelId)
        {
            throw new ConflictException("section_mismatch", "The section does not belong to the requested academic year and grade level.");
        }

        var alreadyOpen = await db.Enrollments.AnyAsync(
            enrollment => enrollment.StudentId == request.StudentId
                && enrollment.AcademicYearId == request.AcademicYearId
                && enrollment.Status == EnrollmentStatus.Enrolled,
            cancellationToken);
        if (alreadyOpen)
        {
            throw new ConflictException("already_enrolled", "The student already has an open enrollment for this academic year.");
        }

        var entity = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = request.StudentId,
            AcademicYearId = request.AcademicYearId,
            GradeLevelId = request.GradeLevelId,
            ClassSectionId = request.ClassSectionId,
            Status = EnrollmentStatus.Enrolled,
            StartDate = request.StartDate,
            Notes = request.Notes
        };

        db.Enrollments.Add(entity);

        if (student.Status != StudentStatus.Active)
        {
            student.Status = StudentStatus.Active;
        }

        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record ChangeSectionCommand(Guid EnrollmentId, Guid ClassSectionId) : IRequest<EnrollmentDto>;

public sealed class ChangeSectionHandler(IAppDbContext db)
    : IRequestHandler<ChangeSectionCommand, EnrollmentDto>
{
    public async Task<EnrollmentDto> Handle(ChangeSectionCommand request, CancellationToken cancellationToken)
    {
        var enrollment = await db.Enrollments
            .SingleOrDefaultAsync(candidate => candidate.Id == request.EnrollmentId, cancellationToken)
            ?? throw new NotFoundException("Enrollment", request.EnrollmentId);

        if (enrollment.Status != EnrollmentStatus.Enrolled)
        {
            throw new ConflictException("enrollment_not_open", "Only an open enrollment can change sections.");
        }

        var section = await db.ClassSections
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ClassSectionId, cancellationToken)
            ?? throw new NotFoundException("ClassSection", request.ClassSectionId);

        if (section.AcademicYearId != enrollment.AcademicYearId || section.GradeLevelId != enrollment.GradeLevelId)
        {
            throw new ConflictException("section_mismatch", "The section does not belong to the enrollment's academic year and grade level.");
        }

        enrollment.ClassSectionId = request.ClassSectionId;

        await db.SaveChangesAsync(cancellationToken);
        return enrollment.ToDto();
    }
}

public sealed record TransferEnrollmentCommand(Guid EnrollmentId, DateOnly EndDate, string? Notes) : IRequest<EnrollmentDto>;

public sealed class TransferEnrollmentHandler(IAppDbContext db)
    : IRequestHandler<TransferEnrollmentCommand, EnrollmentDto>
{
    public Task<EnrollmentDto> Handle(TransferEnrollmentCommand request, CancellationToken cancellationToken) =>
        EnrollmentClosing.CloseAsync(
            db,
            request.EnrollmentId,
            EnrollmentStatus.Transferred,
            StudentStatus.Transferred,
            request.EndDate,
            request.Notes,
            cancellationToken);
}

public sealed record WithdrawEnrollmentCommand(Guid EnrollmentId, DateOnly EndDate, string? Notes) : IRequest<EnrollmentDto>;

public sealed class WithdrawEnrollmentHandler(IAppDbContext db)
    : IRequestHandler<WithdrawEnrollmentCommand, EnrollmentDto>
{
    public Task<EnrollmentDto> Handle(WithdrawEnrollmentCommand request, CancellationToken cancellationToken) =>
        EnrollmentClosing.CloseAsync(
            db,
            request.EnrollmentId,
            EnrollmentStatus.Withdrawn,
            StudentStatus.Withdrawn,
            request.EndDate,
            request.Notes,
            cancellationToken);
}

public sealed record GraduateEnrollmentCommand(Guid EnrollmentId, DateOnly EndDate, string? Notes) : IRequest<EnrollmentDto>;

public sealed class GraduateEnrollmentHandler(IAppDbContext db)
    : IRequestHandler<GraduateEnrollmentCommand, EnrollmentDto>
{
    public Task<EnrollmentDto> Handle(GraduateEnrollmentCommand request, CancellationToken cancellationToken) =>
        EnrollmentClosing.CloseAsync(
            db,
            request.EnrollmentId,
            EnrollmentStatus.Graduated,
            StudentStatus.Graduated,
            request.EndDate,
            request.Notes,
            cancellationToken);
}

internal static class EnrollmentClosing
{
    public static async Task<EnrollmentDto> CloseAsync(
        IAppDbContext db,
        Guid enrollmentId,
        EnrollmentStatus closedStatus,
        StudentStatus studentStatus,
        DateOnly endDate,
        string? notes,
        CancellationToken cancellationToken)
    {
        var enrollment = await db.Enrollments
            .SingleOrDefaultAsync(candidate => candidate.Id == enrollmentId, cancellationToken)
            ?? throw new NotFoundException("Enrollment", enrollmentId);

        if (enrollment.Status != EnrollmentStatus.Enrolled)
        {
            throw new ConflictException("enrollment_not_open", "Only an open enrollment can be closed.");
        }

        if (endDate < enrollment.StartDate)
        {
            throw new ConflictException("invalid_enrollment_end_date", "The end date cannot be before the enrollment start date.");
        }

        enrollment.Status = closedStatus;
        enrollment.EndDate = endDate;
        if (!string.IsNullOrWhiteSpace(notes))
        {
            enrollment.Notes = notes;
        }

        var student = await db.Students
            .SingleOrDefaultAsync(candidate => candidate.Id == enrollment.StudentId, cancellationToken)
            ?? throw new NotFoundException("Student", enrollment.StudentId);

        student.Status = studentStatus;

        await db.SaveChangesAsync(cancellationToken);
        return enrollment.ToDto();
    }
}
