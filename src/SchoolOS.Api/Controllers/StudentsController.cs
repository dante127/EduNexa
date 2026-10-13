using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Students;

namespace SchoolOS.Api.Controllers;

public sealed record CreateEnrollmentRequest(
    Guid AcademicYearId,
    Guid GradeLevelId,
    Guid ClassSectionId,
    DateOnly StartDate,
    string? Notes);

public sealed record LinkGuardianRequest(
    Guid GuardianId,
    string Relationship,
    bool IsPrimary,
    bool IsEmergencyContact,
    bool IsAuthorizedPickup,
    string? CustodyNotes);

public sealed record UpdateGuardianLinkRequest(
    string Relationship,
    bool IsPrimary,
    bool IsEmergencyContact,
    bool IsAuthorizedPickup,
    string? CustodyNotes);

[ApiController]
[Route("api/students")]
[Authorize]
public sealed class StudentsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "perm:students.view")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new ListStudentsQuery(search, status, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:students.view")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetStudentQuery(id), cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Update(Guid id, UpdateStudentCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }

        return Ok(await mediator.Send(command, cancellationToken));
    }

    [HttpGet("{id:guid}/enrollments")]
    [Authorize(Policy = "perm:students.view")]
    public async Task<IActionResult> ListEnrollments(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListStudentEnrollmentsQuery(id), cancellationToken));

    [HttpPost("{id:guid}/enrollments")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> CreateEnrollment(Guid id, CreateEnrollmentRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(
            new CreateEnrollmentCommand(
                id,
                request.AcademicYearId,
                request.GradeLevelId,
                request.ClassSectionId,
                request.StartDate,
                request.Notes),
            cancellationToken));

    [HttpGet("{id:guid}/guardians")]
    [Authorize(Policy = "perm:students.view")]
    public async Task<IActionResult> ListGuardians(Guid id, CancellationToken cancellationToken)
    {
        var detail = await mediator.Send(new GetStudentQuery(id), cancellationToken);
        return Ok(detail.Guardians);
    }

    [HttpPost("{id:guid}/guardians")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> LinkGuardian(Guid id, LinkGuardianRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(
            new LinkGuardianCommand(
                id,
                request.GuardianId,
                request.Relationship,
                request.IsPrimary,
                request.IsEmergencyContact,
                request.IsAuthorizedPickup,
                request.CustodyNotes),
            cancellationToken));

    [HttpPut("{id:guid}/guardians/{guardianId:guid}")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> UpdateGuardianLink(
        Guid id,
        Guid guardianId,
        UpdateGuardianLinkRequest request,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(
            new UpdateGuardianLinkCommand(
                id,
                guardianId,
                request.Relationship,
                request.IsPrimary,
                request.IsEmergencyContact,
                request.IsAuthorizedPickup,
                request.CustodyNotes),
            cancellationToken));

    [HttpDelete("{id:guid}/guardians/{guardianId:guid}")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> UnlinkGuardian(Guid id, Guid guardianId, CancellationToken cancellationToken)
    {
        await mediator.Send(new UnlinkGuardianCommand(id, guardianId), cancellationToken);
        return NoContent();
    }
}
