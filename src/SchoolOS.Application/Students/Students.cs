using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Students;

namespace SchoolOS.Application.Students;

public sealed record StudentDto(
    Guid Id,
    string StudentNumber,
    string FirstNameEn,
    string LastNameEn,
    string FirstNameAr,
    string LastNameAr,
    DateOnly DateOfBirth,
    string Gender,
    string? Nationality,
    string Status,
    string? Phone,
    string? Email,
    string? Address,
    string? PhotoPath);

internal static class StudentMapping
{
    public static StudentDto ToDto(this Student student) =>
        new(
            student.Id,
            student.StudentNumber,
            student.FirstNameEn,
            student.LastNameEn,
            student.FirstNameAr,
            student.LastNameAr,
            student.DateOfBirth,
            student.Gender,
            student.Nationality,
            student.Status.ToString(),
            student.Phone,
            student.Email,
            student.Address,
            student.PhotoPath);
}

public sealed record ListStudentsQuery(string? Search, string? Status, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<StudentDto>>;

public sealed class ListStudentsHandler(IAppDbContext db)
    : IRequestHandler<ListStudentsQuery, PagedResult<StudentDto>>
{
    public async Task<PagedResult<StudentDto>> Handle(ListStudentsQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = db.Students.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            var lowered = term.ToLowerInvariant();
            query = query.Where(student =>
                student.FirstNameEn.ToLower().Contains(lowered)
                || student.LastNameEn.ToLower().Contains(lowered)
                || student.FirstNameAr.ToLower().Contains(lowered)
                || student.LastNameAr.ToLower().Contains(lowered)
                || student.StudentNumber.ToLower().Contains(lowered));
        }

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<StudentStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(student => student.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(student => student.StudentNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StudentDto>(items.Select(student => student.ToDto()).ToList(), page, pageSize, total);
    }
}

public sealed record StudentGuardianDto(
    Guid GuardianId,
    string FullNameEn,
    string FullNameAr,
    string? Phone,
    string Relationship,
    bool IsPrimary,
    bool IsEmergencyContact,
    bool IsAuthorizedPickup,
    string? CustodyNotes);

public sealed record StudentDetailDto(StudentDto Student, List<StudentGuardianDto> Guardians, List<EnrollmentDto> Enrollments);

public sealed record GetStudentQuery(Guid Id) : IRequest<StudentDetailDto>;

public sealed class GetStudentHandler(IAppDbContext db)
    : IRequestHandler<GetStudentQuery, StudentDetailDto>
{
    public async Task<StudentDetailDto> Handle(GetStudentQuery request, CancellationToken cancellationToken)
    {
        var student = await db.Students.AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Student", request.Id);

        var guardians = await (
                from link in db.StudentGuardians.AsNoTracking()
                join guardian in db.Guardians.AsNoTracking() on link.GuardianId equals guardian.Id
                where link.StudentId == request.Id
                orderby link.IsPrimary descending, guardian.FullNameEn
                select new StudentGuardianDto(
                    guardian.Id,
                    guardian.FullNameEn,
                    guardian.FullNameAr,
                    guardian.Phone,
                    link.Relationship,
                    link.IsPrimary,
                    link.IsEmergencyContact,
                    link.IsAuthorizedPickup,
                    link.CustodyNotes))
            .ToListAsync(cancellationToken);

        var enrollments = await db.Enrollments.AsNoTracking()
            .Where(enrollment => enrollment.StudentId == request.Id)
            .OrderByDescending(enrollment => enrollment.StartDate)
            .ToListAsync(cancellationToken);

        return new StudentDetailDto(
            student.ToDto(),
            guardians,
            enrollments.Select(EnrollmentMapping.ToDto).ToList());
    }
}

public sealed record UpdateStudentCommand(
    Guid Id,
    string FirstNameEn,
    string LastNameEn,
    string FirstNameAr,
    string LastNameAr,
    DateOnly DateOfBirth,
    string Gender,
    string? Nationality,
    string? Phone,
    string? Email,
    string? Address,
    string? PhotoPath) : IRequest<StudentDto>;

public sealed class UpdateStudentValidator : AbstractValidator<UpdateStudentCommand>
{
    public UpdateStudentValidator()
    {
        RuleFor(x => x.FirstNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FirstNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Gender).Must(gender => gender is "Male" or "Female").WithMessage("Gender must be 'Male' or 'Female'.");
        RuleFor(x => x.Nationality).MaximumLength(100);
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Address).MaximumLength(300);
        RuleFor(x => x.PhotoPath).MaximumLength(300);
    }
}

public sealed class UpdateStudentHandler(IAppDbContext db)
    : IRequestHandler<UpdateStudentCommand, StudentDto>
{
    public async Task<StudentDto> Handle(UpdateStudentCommand request, CancellationToken cancellationToken)
    {
        var student = await db.Students.SingleOrDefaultAsync(s => s.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Student", request.Id);

        student.FirstNameEn = request.FirstNameEn;
        student.LastNameEn = request.LastNameEn;
        student.FirstNameAr = request.FirstNameAr;
        student.LastNameAr = request.LastNameAr;
        student.DateOfBirth = request.DateOfBirth;
        student.Gender = request.Gender;
        student.Nationality = request.Nationality;
        student.Phone = request.Phone;
        student.Email = request.Email;
        student.Address = request.Address;
        student.PhotoPath = request.PhotoPath;

        await db.SaveChangesAsync(cancellationToken);
        return student.ToDto();
    }
}
