using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Staff;

namespace SchoolOS.Application.Staff;

public sealed record TeacherAssignmentDto(
    Guid Id,
    Guid AcademicYearId,
    Guid StaffMemberId,
    Guid SubjectId,
    Guid ClassSectionId);

public sealed record ListTeacherAssignmentsQuery(
    Guid? StaffMemberId,
    Guid? AcademicYearId,
    Guid? ClassSectionId,
    Guid? SubjectId) : IRequest<List<TeacherAssignmentDto>>;

public sealed class ListTeacherAssignmentsHandler(IAppDbContext db)
    : IRequestHandler<ListTeacherAssignmentsQuery, List<TeacherAssignmentDto>>
{
    public async Task<List<TeacherAssignmentDto>> Handle(
        ListTeacherAssignmentsQuery request,
        CancellationToken cancellationToken)
    {
        var query = db.TeacherAssignments.AsNoTracking();

        if (request.StaffMemberId is not null)
        {
            query = query.Where(assignment => assignment.StaffMemberId == request.StaffMemberId);
        }

        if (request.AcademicYearId is not null)
        {
            query = query.Where(assignment => assignment.AcademicYearId == request.AcademicYearId);
        }

        if (request.ClassSectionId is not null)
        {
            query = query.Where(assignment => assignment.ClassSectionId == request.ClassSectionId);
        }

        if (request.SubjectId is not null)
        {
            query = query.Where(assignment => assignment.SubjectId == request.SubjectId);
        }

        var assignments = await query
            .OrderBy(assignment => assignment.AcademicYearId)
            .ThenBy(assignment => assignment.StaffMemberId)
            .ToListAsync(cancellationToken);

        return assignments
            .Select(assignment => new TeacherAssignmentDto(
                assignment.Id,
                assignment.AcademicYearId,
                assignment.StaffMemberId,
                assignment.SubjectId,
                assignment.ClassSectionId))
            .ToList();
    }
}

public sealed record CreateTeacherAssignmentCommand(
    Guid AcademicYearId,
    Guid StaffMemberId,
    Guid SubjectId,
    Guid ClassSectionId) : IRequest<TeacherAssignmentDto>;

public sealed class CreateTeacherAssignmentHandler(IAppDbContext db)
    : IRequestHandler<CreateTeacherAssignmentCommand, TeacherAssignmentDto>
{
    public async Task<TeacherAssignmentDto> Handle(
        CreateTeacherAssignmentCommand request,
        CancellationToken cancellationToken)
    {
        var staffExists = await db.StaffMembers.AnyAsync(
            staff => staff.Id == request.StaffMemberId, cancellationToken);
        if (!staffExists)
        {
            throw new NotFoundException("StaffMember", request.StaffMemberId);
        }

        var subjectExists = await db.Subjects.AnyAsync(
            subject => subject.Id == request.SubjectId, cancellationToken);
        if (!subjectExists)
        {
            throw new NotFoundException("Subject", request.SubjectId);
        }

        var yearExists = await db.AcademicYears.AnyAsync(
            year => year.Id == request.AcademicYearId, cancellationToken);
        if (!yearExists)
        {
            throw new NotFoundException("AcademicYear", request.AcademicYearId);
        }

        var section = await db.ClassSections
            .SingleOrDefaultAsync(candidate => candidate.Id == request.ClassSectionId, cancellationToken)
            ?? throw new NotFoundException("ClassSection", request.ClassSectionId);

        if (section.AcademicYearId != request.AcademicYearId)
        {
            throw new ConflictException(
                "section_year_mismatch",
                "The section does not belong to the requested academic year.");
        }

        var duplicate = await db.TeacherAssignments.AnyAsync(
            assignment => assignment.AcademicYearId == request.AcademicYearId
                && assignment.StaffMemberId == request.StaffMemberId
                && assignment.SubjectId == request.SubjectId
                && assignment.ClassSectionId == request.ClassSectionId,
            cancellationToken);
        if (duplicate)
        {
            throw new ConflictException(
                "duplicate_assignment",
                "This teacher is already assigned to this subject and section for the academic year.");
        }

        var entity = new TeacherAssignment
        {
            Id = Guid.NewGuid(),
            AcademicYearId = request.AcademicYearId,
            StaffMemberId = request.StaffMemberId,
            SubjectId = request.SubjectId,
            ClassSectionId = request.ClassSectionId
        };

        db.TeacherAssignments.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return new TeacherAssignmentDto(
            entity.Id,
            entity.AcademicYearId,
            entity.StaffMemberId,
            entity.SubjectId,
            entity.ClassSectionId);
    }
}

public sealed record DeleteTeacherAssignmentCommand(Guid Id) : IRequest;

public sealed class DeleteTeacherAssignmentHandler(IAppDbContext db)
    : IRequestHandler<DeleteTeacherAssignmentCommand>
{
    public async Task Handle(DeleteTeacherAssignmentCommand request, CancellationToken cancellationToken)
    {
        var assignment = await db.TeacherAssignments
            .SingleOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("TeacherAssignment", request.Id);

        db.TeacherAssignments.Remove(assignment);
        await db.SaveChangesAsync(cancellationToken);
    }
}
