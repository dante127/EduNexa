using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Students;

namespace SchoolOS.Api.Controllers;

public sealed record ReviewNotesRequest(string? Notes);

public sealed record RejectRequest(string Notes);

public sealed record EnrollmentRequest(Guid ClassSectionId, DateOnly StartDate, string? Notes);

[ApiController]
[Route("api/admission-applications")]
[Authorize]
public sealed class AdmissionApplicationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "perm:students.view")]
    public async Task<IActionResult> List(
        [FromQuery] string? status,
        [FromQuery] Guid? academicYearId,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListAdmissionApplicationsQuery(status, academicYearId), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:students.view")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAdmissionApplicationQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Create(CreateAdmissionApplicationCommand command, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Update(Guid id, UpdateAdmissionApplicationCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }

        return Ok(await mediator.Send(command, cancellationToken));
    }

    [HttpPost("{id:guid}/review")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Review(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ReviewAdmissionApplicationCommand(id, CurrentUserId()), cancellationToken));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Approve(Guid id, ReviewNotesRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ApproveAdmissionApplicationCommand(id, CurrentUserId(), request.Notes), cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Reject(Guid id, RejectRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new RejectAdmissionApplicationCommand(id, CurrentUserId(), request.Notes), cancellationToken));

    [HttpPost("{id:guid}/enroll")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Enroll(Guid id, EnrollmentRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(
            new EnrollAdmissionApplicationCommand(id, request.ClassSectionId, request.StartDate, request.Notes),
            cancellationToken));

    private Guid CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
