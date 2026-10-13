using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Students;

namespace SchoolOS.Application.Students;

public sealed record AdmissionApplicationDto(
    Guid Id,
    string ApplicantFirstNameEn,
    string ApplicantLastNameEn,
    string ApplicantFirstNameAr,
    string ApplicantLastNameAr,
    DateOnly DateOfBirth,
    string Gender,
    string? Nationality,
    Guid AcademicYearId,
    Guid DesiredGradeLevelId,
    string GuardianName,
    string? GuardianPhone,
    string? GuardianEmail,
    string Status,
    DateTimeOffset SubmittedAt,
    Guid? ReviewedByUserId,
    DateTimeOffset? ReviewedAt,
    string? DecisionNotes,
    Guid? ConvertedStudentId);

internal static class AdmissionApplicationMapping
{
    public static AdmissionApplicationDto ToDto(this AdmissionApplication application) =>
        new(
            application.Id,
            application.ApplicantFirstNameEn,
            application.ApplicantLastNameEn,
            application.ApplicantFirstNameAr,
            application.ApplicantLastNameAr,
            application.DateOfBirth,
            application.Gender,
            application.Nationality,
            application.AcademicYearId,
            application.DesiredGradeLevelId,
            application.GuardianName,
            application.GuardianPhone,
            application.GuardianEmail,
            application.Status.ToString(),
            application.SubmittedAt,
            application.ReviewedByUserId,
            application.ReviewedAt,
            application.DecisionNotes,
            application.ConvertedStudentId);
}

public sealed record ListAdmissionApplicationsQuery(string? Status, Guid? AcademicYearId)
    : IRequest<List<AdmissionApplicationDto>>;

public sealed class ListAdmissionApplicationsHandler(IAppDbContext db)
    : IRequestHandler<ListAdmissionApplicationsQuery, List<AdmissionApplicationDto>>
{
    public async Task<List<AdmissionApplicationDto>> Handle(ListAdmissionApplicationsQuery request, CancellationToken cancellationToken)
    {
        var query = db.AdmissionApplications.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<AdmissionApplicationStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(application => application.Status == status);
        }

        if (request.AcademicYearId is not null)
        {
            query = query.Where(application => application.AcademicYearId == request.AcademicYearId);
        }

        var applications = await query
            .OrderByDescending(application => application.SubmittedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        return applications.Select(application => application.ToDto()).ToList();
    }
}

public sealed record GetAdmissionApplicationQuery(Guid Id) : IRequest<AdmissionApplicationDto>;

public sealed class GetAdmissionApplicationHandler(IAppDbContext db)
    : IRequestHandler<GetAdmissionApplicationQuery, AdmissionApplicationDto>
{
    public async Task<AdmissionApplicationDto> Handle(GetAdmissionApplicationQuery request, CancellationToken cancellationToken)
    {
        var application = await db.AdmissionApplications.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AdmissionApplication", request.Id);

        return application.ToDto();
    }
}

public interface IAdmissionApplicationInput
{
    string ApplicantFirstNameEn { get; }

    string ApplicantLastNameEn { get; }

    string ApplicantFirstNameAr { get; }

    string ApplicantLastNameAr { get; }

    DateOnly DateOfBirth { get; }

    string Gender { get; }

    string? Nationality { get; }

    Guid AcademicYearId { get; }

    Guid DesiredGradeLevelId { get; }

    string GuardianName { get; }

    string? GuardianPhone { get; }

    string? GuardianEmail { get; }
}

public class AdmissionApplicationInputValidator<T> : AbstractValidator<T>
    where T : IAdmissionApplicationInput
{
    public AdmissionApplicationInputValidator()
    {
        RuleFor(x => x.ApplicantFirstNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ApplicantLastNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ApplicantFirstNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.ApplicantLastNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DateOfBirth).LessThan(DateOnly.FromDateTime(DateTime.UtcNow))
            .WithMessage("Date of birth must be in the past.");
        RuleFor(x => x.Gender).Must(gender => gender is "Male" or "Female").WithMessage("Gender must be 'Male' or 'Female'.");
        RuleFor(x => x.Nationality).MaximumLength(100);
        RuleFor(x => x.GuardianName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.GuardianPhone).MaximumLength(30);
        RuleFor(x => x.GuardianEmail).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.GuardianEmail));
    }
}

public sealed record CreateAdmissionApplicationCommand(
    string ApplicantFirstNameEn,
    string ApplicantLastNameEn,
    string ApplicantFirstNameAr,
    string ApplicantLastNameAr,
    DateOnly DateOfBirth,
    string Gender,
    string? Nationality,
    Guid AcademicYearId,
    Guid DesiredGradeLevelId,
    string GuardianName,
    string? GuardianPhone,
    string? GuardianEmail) : IRequest<AdmissionApplicationDto>, IAdmissionApplicationInput;

public sealed class CreateAdmissionApplicationValidator : AdmissionApplicationInputValidator<CreateAdmissionApplicationCommand>
{
}

public sealed class CreateAdmissionApplicationHandler(IAppDbContext db)
    : IRequestHandler<CreateAdmissionApplicationCommand, AdmissionApplicationDto>
{
    public async Task<AdmissionApplicationDto> Handle(CreateAdmissionApplicationCommand request, CancellationToken cancellationToken)
    {
        await EnsureReferencesAsync(db, request.AcademicYearId, request.DesiredGradeLevelId, cancellationToken);

        var application = new AdmissionApplication
        {
            Id = Guid.NewGuid(),
            ApplicantFirstNameEn = request.ApplicantFirstNameEn,
            ApplicantLastNameEn = request.ApplicantLastNameEn,
            ApplicantFirstNameAr = request.ApplicantFirstNameAr,
            ApplicantLastNameAr = request.ApplicantLastNameAr,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Nationality = request.Nationality,
            AcademicYearId = request.AcademicYearId,
            DesiredGradeLevelId = request.DesiredGradeLevelId,
            GuardianName = request.GuardianName,
            GuardianPhone = request.GuardianPhone,
            GuardianEmail = request.GuardianEmail,
            Status = AdmissionApplicationStatus.Submitted,
            SubmittedAt = DateTimeOffset.UtcNow
        };

        db.AdmissionApplications.Add(application);
        await db.SaveChangesAsync(cancellationToken);
        return application.ToDto();
    }

    internal static async Task EnsureReferencesAsync(
        IAppDbContext db,
        Guid academicYearId,
        Guid gradeLevelId,
        CancellationToken cancellationToken)
    {
        var yearExists = await db.AcademicYears.AnyAsync(year => year.Id == academicYearId, cancellationToken);
        if (!yearExists)
        {
            throw new NotFoundException("AcademicYear", academicYearId);
        }

        var gradeLevelExists = await db.GradeLevels.AnyAsync(level => level.Id == gradeLevelId, cancellationToken);
        if (!gradeLevelExists)
        {
            throw new NotFoundException("GradeLevel", gradeLevelId);
        }
    }
}

public sealed record UpdateAdmissionApplicationCommand(
    Guid Id,
    string ApplicantFirstNameEn,
    string ApplicantLastNameEn,
    string ApplicantFirstNameAr,
    string ApplicantLastNameAr,
    DateOnly DateOfBirth,
    string Gender,
    string? Nationality,
    Guid AcademicYearId,
    Guid DesiredGradeLevelId,
    string GuardianName,
    string? GuardianPhone,
    string? GuardianEmail) : IRequest<AdmissionApplicationDto>, IAdmissionApplicationInput;

public sealed class UpdateAdmissionApplicationValidator : AdmissionApplicationInputValidator<UpdateAdmissionApplicationCommand>
{
}

public sealed class UpdateAdmissionApplicationHandler(IAppDbContext db)
    : IRequestHandler<UpdateAdmissionApplicationCommand, AdmissionApplicationDto>
{
    public async Task<AdmissionApplicationDto> Handle(UpdateAdmissionApplicationCommand request, CancellationToken cancellationToken)
    {
        var application = await db.AdmissionApplications
            .SingleOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AdmissionApplication", request.Id);

        if (application.Status is not (AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.UnderReview))
        {
            throw new ConflictException("application_locked", "Only applications that are submitted or under review can be edited.");
        }

        await CreateAdmissionApplicationHandler.EnsureReferencesAsync(
            db,
            request.AcademicYearId,
            request.DesiredGradeLevelId,
            cancellationToken);

        application.ApplicantFirstNameEn = request.ApplicantFirstNameEn;
        application.ApplicantLastNameEn = request.ApplicantLastNameEn;
        application.ApplicantFirstNameAr = request.ApplicantFirstNameAr;
        application.ApplicantLastNameAr = request.ApplicantLastNameAr;
        application.DateOfBirth = request.DateOfBirth;
        application.Gender = request.Gender;
        application.Nationality = request.Nationality;
        application.AcademicYearId = request.AcademicYearId;
        application.DesiredGradeLevelId = request.DesiredGradeLevelId;
        application.GuardianName = request.GuardianName;
        application.GuardianPhone = request.GuardianPhone;
        application.GuardianEmail = request.GuardianEmail;

        await db.SaveChangesAsync(cancellationToken);
        return application.ToDto();
    }
}

public sealed record ReviewAdmissionApplicationCommand(Guid Id, Guid ReviewedByUserId) : IRequest<AdmissionApplicationDto>;

public sealed class ReviewAdmissionApplicationHandler(IAppDbContext db)
    : IRequestHandler<ReviewAdmissionApplicationCommand, AdmissionApplicationDto>
{
    public async Task<AdmissionApplicationDto> Handle(ReviewAdmissionApplicationCommand request, CancellationToken cancellationToken)
    {
        var application = await db.AdmissionApplications
            .SingleOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AdmissionApplication", request.Id);

        if (application.Status != AdmissionApplicationStatus.Submitted)
        {
            throw new ConflictException("application_not_submitted", "Only a submitted application can be moved to review.");
        }

        application.Status = AdmissionApplicationStatus.UnderReview;
        application.ReviewedByUserId = request.ReviewedByUserId;
        application.ReviewedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return application.ToDto();
    }
}

public sealed record ApproveAdmissionApplicationCommand(Guid Id, Guid ReviewedByUserId, string? Notes) : IRequest<AdmissionApplicationDto>;

public sealed class ApproveAdmissionApplicationHandler(IAppDbContext db)
    : IRequestHandler<ApproveAdmissionApplicationCommand, AdmissionApplicationDto>
{
    public async Task<AdmissionApplicationDto> Handle(ApproveAdmissionApplicationCommand request, CancellationToken cancellationToken)
    {
        var application = await db.AdmissionApplications
            .SingleOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AdmissionApplication", request.Id);

        if (application.Status is not (AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.UnderReview))
        {
            throw new ConflictException("application_not_reviewable", "Only a submitted or under-review application can be approved.");
        }

        application.Status = AdmissionApplicationStatus.Approved;
        application.ReviewedByUserId = request.ReviewedByUserId;
        application.ReviewedAt = DateTimeOffset.UtcNow;
        application.DecisionNotes = request.Notes;

        await db.SaveChangesAsync(cancellationToken);
        return application.ToDto();
    }
}

public sealed record RejectAdmissionApplicationCommand(Guid Id, Guid ReviewedByUserId, string Notes) : IRequest<AdmissionApplicationDto>;

public sealed class RejectAdmissionApplicationValidator : AbstractValidator<RejectAdmissionApplicationCommand>
{
    public RejectAdmissionApplicationValidator()
    {
        RuleFor(x => x.Notes).NotEmpty().MaximumLength(500).WithMessage("A rejection reason is required.");
    }
}

public sealed class RejectAdmissionApplicationHandler(IAppDbContext db)
    : IRequestHandler<RejectAdmissionApplicationCommand, AdmissionApplicationDto>
{
    public async Task<AdmissionApplicationDto> Handle(RejectAdmissionApplicationCommand request, CancellationToken cancellationToken)
    {
        var application = await db.AdmissionApplications
            .SingleOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AdmissionApplication", request.Id);

        if (application.Status is not (AdmissionApplicationStatus.Submitted or AdmissionApplicationStatus.UnderReview))
        {
            throw new ConflictException("application_not_reviewable", "Only a submitted or under-review application can be rejected.");
        }

        application.Status = AdmissionApplicationStatus.Rejected;
        application.ReviewedByUserId = request.ReviewedByUserId;
        application.ReviewedAt = DateTimeOffset.UtcNow;
        application.DecisionNotes = request.Notes;

        await db.SaveChangesAsync(cancellationToken);
        return application.ToDto();
    }
}

public sealed record AdmissionEnrollmentResultDto(StudentDto Student, EnrollmentDto Enrollment, AdmissionApplicationDto Application);

public sealed record EnrollAdmissionApplicationCommand(
    Guid Id,
    Guid ClassSectionId,
    DateOnly StartDate,
    string? Notes) : IRequest<AdmissionEnrollmentResultDto>;

public sealed class EnrollAdmissionApplicationValidator : AbstractValidator<EnrollAdmissionApplicationCommand>
{
    public EnrollAdmissionApplicationValidator()
    {
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class EnrollAdmissionApplicationHandler(IAppDbContext db)
    : IRequestHandler<EnrollAdmissionApplicationCommand, AdmissionEnrollmentResultDto>
{
    public async Task<AdmissionEnrollmentResultDto> Handle(
        EnrollAdmissionApplicationCommand request,
        CancellationToken cancellationToken)
    {
        var application = await db.AdmissionApplications
            .SingleOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("AdmissionApplication", request.Id);

        if (application.ConvertedStudentId is not null || application.Status == AdmissionApplicationStatus.Enrolled)
        {
            throw new ConflictException("application_already_enrolled", "This application has already been enrolled.");
        }

        if (application.Status != AdmissionApplicationStatus.Approved)
        {
            throw new ConflictException("application_not_approved", "The application must be approved before enrollment.");
        }

        var year = await db.AcademicYears
            .SingleOrDefaultAsync(candidate => candidate.Id == application.AcademicYearId, cancellationToken)
            ?? throw new NotFoundException("AcademicYear", application.AcademicYearId);

        var section = await db.ClassSections
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ClassSectionId, cancellationToken)
            ?? throw new NotFoundException("ClassSection", request.ClassSectionId);

        if (section.AcademicYearId != application.AcademicYearId
            || section.GradeLevelId != application.DesiredGradeLevelId)
        {
            throw new ConflictException("section_mismatch", "The section does not belong to the application's academic year and grade level.");
        }

        var studentNumber = await AllocateStudentNumberAsync(db, year.StartDate.Year, cancellationToken);

        var student = new Student
        {
            Id = Guid.NewGuid(),
            StudentNumber = studentNumber,
            FirstNameEn = application.ApplicantFirstNameEn,
            LastNameEn = application.ApplicantLastNameEn,
            FirstNameAr = application.ApplicantFirstNameAr,
            LastNameAr = application.ApplicantLastNameAr,
            DateOfBirth = application.DateOfBirth,
            Gender = application.Gender,
            Nationality = application.Nationality,
            Status = StudentStatus.Active
        };
        db.Students.Add(student);

        if (!string.IsNullOrWhiteSpace(application.GuardianName))
        {
            var guardian = new Guardian
            {
                Id = Guid.NewGuid(),
                FullNameEn = application.GuardianName,
                FullNameAr = application.GuardianName,
                Phone = application.GuardianPhone,
                Email = application.GuardianEmail
            };
            db.Guardians.Add(guardian);

            db.StudentGuardians.Add(new StudentGuardian
            {
                StudentId = student.Id,
                GuardianId = guardian.Id,
                Relationship = "Guardian",
                IsPrimary = true,
                IsEmergencyContact = true,
                IsAuthorizedPickup = true
            });
        }

        var enrollment = new Enrollment
        {
            Id = Guid.NewGuid(),
            StudentId = student.Id,
            AcademicYearId = application.AcademicYearId,
            GradeLevelId = application.DesiredGradeLevelId,
            ClassSectionId = request.ClassSectionId,
            Status = EnrollmentStatus.Enrolled,
            StartDate = request.StartDate,
            Notes = request.Notes
        };
        db.Enrollments.Add(enrollment);

        application.Status = AdmissionApplicationStatus.Enrolled;
        application.ConvertedStudentId = student.Id;

        await db.SaveChangesAsync(cancellationToken);

        return new AdmissionEnrollmentResultDto(student.ToDto(), enrollment.ToDto(), application.ToDto());
    }

    internal static async Task<string> AllocateStudentNumberAsync(
        IAppDbContext db,
        int year,
        CancellationToken cancellationToken)
    {
        // The counter row is locked inside the persistence layer (SELECT ... FOR UPDATE)
        // so concurrent enrollments cannot allocate the same number.
        var number = await db.NextStudentNumberAsync(year, cancellationToken);
        return $"{year}-{number:D4}";
    }
}
