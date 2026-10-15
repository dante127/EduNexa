using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Staff;

namespace SchoolOS.Api.Controllers;

[ApiController]
[Route("api/teacher-assignments")]
[Authorize]
public sealed class TeacherAssignmentsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "perm:staff.view")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? staffMemberId,
        [FromQuery] Guid? academicYearId,
        [FromQuery] Guid? classSectionId,
        [FromQuery] Guid? subjectId,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(
            new ListTeacherAssignmentsQuery(staffMemberId, academicYearId, classSectionId, subjectId),
            cancellationToken));

    [HttpPost]
    [Authorize(Policy = "perm:staff.manage")]
    public async Task<IActionResult> Create(
        CreateTeacherAssignmentCommand command,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(command, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "perm:staff.manage")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteTeacherAssignmentCommand(id), cancellationToken);
        return NoContent();
    }
}
