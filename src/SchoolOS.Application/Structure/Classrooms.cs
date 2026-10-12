using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SchoolOS.Application.Common;
using SchoolOS.Domain.Structure;

namespace SchoolOS.Application.Structure;

public sealed record ClassroomDto(Guid Id, string Name, int Capacity, bool IsActive);

internal static class ClassroomMapping
{
    public static ClassroomDto ToDto(this Classroom classroom) =>
        new(classroom.Id, classroom.Name, classroom.Capacity, classroom.IsActive);
}

public sealed record ListClassroomsQuery : IRequest<List<ClassroomDto>>;

public sealed class ListClassroomsHandler(IAppDbContext db)
    : IRequestHandler<ListClassroomsQuery, List<ClassroomDto>>
{
    public async Task<List<ClassroomDto>> Handle(ListClassroomsQuery request, CancellationToken cancellationToken)
    {
        var classrooms = await db.Classrooms
            .AsNoTracking()
            .OrderBy(room => room.Name)
            .ToListAsync(cancellationToken);

        return classrooms.Select(room => room.ToDto()).ToList();
    }
}

public sealed record CreateClassroomCommand(string Name, int Capacity) : IRequest<ClassroomDto>;

public sealed class CreateClassroomValidator : AbstractValidator<CreateClassroomCommand>
{
    public CreateClassroomValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0);
    }
}

public sealed class CreateClassroomHandler(IAppDbContext db)
    : IRequestHandler<CreateClassroomCommand, ClassroomDto>
{
    public async Task<ClassroomDto> Handle(CreateClassroomCommand request, CancellationToken cancellationToken)
    {
        var duplicate = await db.Classrooms.AnyAsync(room => room.Name == request.Name, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("duplicate_classroom_name", $"A classroom named '{request.Name}' already exists.");
        }

        var entity = new Classroom
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Capacity = request.Capacity
        };

        db.Classrooms.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity.ToDto();
    }
}

public sealed record UpdateClassroomCommand(Guid Id, string Name, int Capacity, bool IsActive) : IRequest<ClassroomDto>;

public sealed class UpdateClassroomValidator : AbstractValidator<UpdateClassroomCommand>
{
    public UpdateClassroomValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Capacity).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateClassroomHandler(IAppDbContext db)
    : IRequestHandler<UpdateClassroomCommand, ClassroomDto>
{
    public async Task<ClassroomDto> Handle(UpdateClassroomCommand request, CancellationToken cancellationToken)
    {
        var classroom = await db.Classrooms.SingleOrDefaultAsync(room => room.Id == request.Id, cancellationToken)
            ?? throw new NotFoundException("Classroom", request.Id);

        var duplicate = await db.Classrooms
            .AnyAsync(room => room.Name == request.Name && room.Id != request.Id, cancellationToken);
        if (duplicate)
        {
            throw new ConflictException("duplicate_classroom_name", $"A classroom named '{request.Name}' already exists.");
        }

        classroom.Name = request.Name;
        classroom.Capacity = request.Capacity;
        classroom.IsActive = request.IsActive;

        await db.SaveChangesAsync(cancellationToken);
        return classroom.ToDto();
    }
}
