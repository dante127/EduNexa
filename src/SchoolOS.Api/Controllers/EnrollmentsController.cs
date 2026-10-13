using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Students;

namespace SchoolOS.Api.Controllers;

public sealed record ChangeSectionRequest(Guid ClassSectionId);

public sealed record CloseEnrollmentRequest(DateOnly EndDate, string? Notes);

[ApiController]
[Route("api/enrollments")]
[Authorize]
public sealed class EnrollmentsController(IMediator mediator) : ControllerBase
{
    [HttpPost("{id:guid}/change-section")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> ChangeSection(Guid id, ChangeSectionRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ChangeSectionCommand(id, request.ClassSectionId), cancellationToken));

    [HttpPost("{id:guid}/transfer")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Transfer(Guid id, CloseEnrollmentRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new TransferEnrollmentCommand(id, request.EndDate, request.Notes), cancellationToken));

    [HttpPost("{id:guid}/withdraw")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Withdraw(Guid id, CloseEnrollmentRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new WithdrawEnrollmentCommand(id, request.EndDate, request.Notes), cancellationToken));

    [HttpPost("{id:guid}/graduate")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Graduate(Guid id, CloseEnrollmentRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GraduateEnrollmentCommand(id, request.EndDate, request.Notes), cancellationToken));
}
