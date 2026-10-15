using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Staff;

namespace SchoolOS.Application.Staff;

internal static class StaffRules
{
    public static bool IsValidContractType(string? value) =>
        Enum.TryParse<ContractType>(value, ignoreCase: true, out _);

    public static bool IsValidStatus(string? value) =>
        Enum.TryParse<StaffStatus>(value, ignoreCase: true, out _);
}

public sealed record StaffDto(
    Guid Id,
    Guid? UserId,
    string EmployeeNumber,
    string FirstNameEn,
    string LastNameEn,
    string FirstNameAr,
    string LastNameAr,
    string Position,
    string? Department,
    DateOnly HireDate,
    string ContractType,
    string Status,
    string? Phone,
    string? Email,
    string? EmergencyContact,
    string? Notes);

internal static class StaffMapping
{
    public static StaffDto ToDto(this StaffMember staff) =>
        new(
            staff.Id,
            staff.UserId,
            staff.EmployeeNumber,
            staff.FirstNameEn,
            staff.LastNameEn,
            staff.FirstNameAr,
            staff.LastNameAr,
            staff.Position,
            staff.Department,
            staff.HireDate,
            staff.ContractType.ToString(),
            staff.Status.ToString(),
            staff.Phone,
            staff.Email,
            staff.EmergencyContact,
            staff.Notes);
}

public sealed record ListStaffQuery(string? Search, string? Status, int Page = 1, int PageSize = 20)
    : IRequest<PagedResult<StaffDto>>;

public sealed class ListStaffHandler(IAppDbContext db)
    : IRequestHandler<ListStaffQuery, PagedResult<StaffDto>>
{
    public async Task<PagedResult<StaffDto>> Handle(ListStaffQuery request, CancellationToken cancellationToken)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var query = db.StaffMembers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            var lowered = term.ToLowerInvariant();
            query = query.Where(staff =>
                staff.FirstNameEn.ToLower().Contains(lowered)
                || staff.LastNameEn.ToLower().Contains(lowered)
                || staff.FirstNameAr.ToLower().Contains(lowered)
                || staff.LastNameAr.ToLower().Contains(lowered)
                || staff.EmployeeNumber.ToLower().Contains(lowered)
                || (staff.Phone != null && staff.Phone.ToLower().Contains(lowered))
                || (staff.Email != null && staff.Email.ToLower().Contains(lowered)));
        }

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<StaffStatus>(request.Status, ignoreCase: true, out var status))
        {
            query = query.Where(staff => staff.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(staff => staff.EmployeeNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<StaffDto>(items.Select(staff => staff.ToDto()).ToList(), page, pageSize, total);
    }
}

public sealed record GetStaffQuery(Guid Id) : IRequest<StaffDto>;

public sealed class GetStaffHandler(IAppDbContext db) : IRequestHandler<GetStaffQuery, StaffDto>
{
    public async Task<StaffDto> Handle(GetStaffQuery request, CancellationToken cancellationToken)
    {
        var staff = await db.StaffMembers.AsNoTracking()
            .SingleOrDefaultAsync(member => member.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("StaffMember", request.Id);

        return staff.ToDto();
    }
}

public sealed record CreateStaffCommand(
    string? EmployeeNumber,
    string FirstNameEn,
    string LastNameEn,
    string FirstNameAr,
    string LastNameAr,
    string Position,
    string? Department,
    DateOnly HireDate,
    string ContractType,
    string? Phone,
    string? Email,
    string? EmergencyContact,
    string? Notes) : IRequest<StaffDto>;

public sealed class CreateStaffValidator : AbstractValidator<CreateStaffCommand>
{
    public CreateStaffValidator()
    {
        RuleFor(x => x.EmployeeNumber).MaximumLength(30);
        RuleFor(x => x.FirstNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FirstNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Position).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Department).MaximumLength(100);
        RuleFor(x => x.ContractType)
            .Must(StaffRules.IsValidContractType)
            .WithMessage("Contract type is not valid.");
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.EmergencyContact).MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class CreateStaffHandler(IAppDbContext db) : IRequestHandler<CreateStaffCommand, StaffDto>
{
    public async Task<StaffDto> Handle(CreateStaffCommand request, CancellationToken cancellationToken)
    {
        string employeeNumber;
        if (!string.IsNullOrWhiteSpace(request.EmployeeNumber))
        {
            employeeNumber = request.EmployeeNumber.Trim();
            var duplicate = await db.StaffMembers.AnyAsync(
                staff => staff.EmployeeNumber == employeeNumber, cancellationToken);
            if (duplicate)
            {
                throw new ConflictException(
                    "duplicate_employee_number",
                    "A staff member with this employee number already exists.");
            }
        }
        else
        {
            var sequence = await db.NextEmployeeNumberAsync(cancellationToken);
            employeeNumber = $"EMP-{sequence:D4}";
        }

        var staffMember = new StaffMember
        {
            Id = Guid.NewGuid(),
            EmployeeNumber = employeeNumber,
            FirstNameEn = request.FirstNameEn.Trim(),
            LastNameEn = request.LastNameEn.Trim(),
            FirstNameAr = request.FirstNameAr.Trim(),
            LastNameAr = request.LastNameAr.Trim(),
            Position = request.Position.Trim(),
            Department = request.Department,
            HireDate = request.HireDate,
            ContractType = Enum.Parse<ContractType>(request.ContractType, ignoreCase: true),
            Status = StaffStatus.Active,
            Phone = request.Phone,
            Email = request.Email,
            EmergencyContact = request.EmergencyContact,
            Notes = request.Notes
        };

        db.StaffMembers.Add(staffMember);
        await db.SaveChangesAsync(cancellationToken);
        return staffMember.ToDto();
    }
}

public sealed record UpdateStaffCommand(
    Guid Id,
    string FirstNameEn,
    string LastNameEn,
    string FirstNameAr,
    string LastNameAr,
    string Position,
    string? Department,
    DateOnly HireDate,
    string ContractType,
    string Status,
    string? Phone,
    string? Email,
    string? EmergencyContact,
    string? Notes) : IRequest<StaffDto>;

public sealed class UpdateStaffValidator : AbstractValidator<UpdateStaffCommand>
{
    public UpdateStaffValidator()
    {
        RuleFor(x => x.FirstNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastNameEn).NotEmpty().MaximumLength(100);
        RuleFor(x => x.FirstNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastNameAr).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Position).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Department).MaximumLength(100);
        RuleFor(x => x.ContractType)
            .Must(StaffRules.IsValidContractType)
            .WithMessage("Contract type is not valid.");
        RuleFor(x => x.Status)
            .Must(StaffRules.IsValidStatus)
            .WithMessage("Status is not valid.");
        RuleFor(x => x.Phone).MaximumLength(30);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.EmergencyContact).MaximumLength(200);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class UpdateStaffHandler(IAppDbContext db) : IRequestHandler<UpdateStaffCommand, StaffDto>
{
    public async Task<StaffDto> Handle(UpdateStaffCommand request, CancellationToken cancellationToken)
    {
        var staff = await db.StaffMembers.SingleOrDefaultAsync(member => member.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("StaffMember", request.Id);

        staff.FirstNameEn = request.FirstNameEn.Trim();
        staff.LastNameEn = request.LastNameEn.Trim();
        staff.FirstNameAr = request.FirstNameAr.Trim();
        staff.LastNameAr = request.LastNameAr.Trim();
        staff.Position = request.Position.Trim();
        staff.Department = request.Department;
        staff.HireDate = request.HireDate;
        staff.ContractType = Enum.Parse<ContractType>(request.ContractType, ignoreCase: true);
        staff.Status = Enum.Parse<StaffStatus>(request.Status, ignoreCase: true);
        staff.Phone = request.Phone;
        staff.Email = request.Email;
        staff.EmergencyContact = request.EmergencyContact;
        staff.Notes = request.Notes;

        await db.SaveChangesAsync(cancellationToken);
        return staff.ToDto();
    }
}

public sealed record LinkStaffUserCommand(Guid StaffId, Guid UserId) : IRequest<StaffDto>;

public sealed class LinkStaffUserHandler(IAppDbContext db) : IRequestHandler<LinkStaffUserCommand, StaffDto>
{
    public async Task<StaffDto> Handle(LinkStaffUserCommand request, CancellationToken cancellationToken)
    {
        var staff = await db.StaffMembers.SingleOrDefaultAsync(member => member.Id == request.StaffId, cancellationToken)
            ?? throw new NotFoundException("StaffMember", request.StaffId);

        var userExists = await db.UserExistsAsync(request.UserId, cancellationToken);
        if (!userExists)
        {
            throw new NotFoundException("User", request.UserId);
        }

        var alreadyLinked = await db.StaffMembers.AnyAsync(
            member => member.UserId == request.UserId && member.Id != request.StaffId, cancellationToken);
        if (alreadyLinked)
        {
            throw new ConflictException(
                "user_already_linked",
                "This user account is already linked to another staff member.");
        }

        staff.UserId = request.UserId;
        await db.SaveChangesAsync(cancellationToken);
        return staff.ToDto();
    }
}

public sealed record UnlinkStaffUserCommand(Guid StaffId) : IRequest<StaffDto>;

public sealed class UnlinkStaffUserHandler(IAppDbContext db) : IRequestHandler<UnlinkStaffUserCommand, StaffDto>
{
    public async Task<StaffDto> Handle(UnlinkStaffUserCommand request, CancellationToken cancellationToken)
    {
        var staff = await db.StaffMembers.SingleOrDefaultAsync(member => member.Id == request.StaffId, cancellationToken)
            ?? throw new NotFoundException("StaffMember", request.StaffId);

        staff.UserId = null;
        await db.SaveChangesAsync(cancellationToken);
        return staff.ToDto();
    }
}
