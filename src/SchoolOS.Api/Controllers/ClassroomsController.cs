using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Structure;

namespace SchoolOS.Api.Controllers;

[ApiController]
[Route("api/classrooms")]
[Authorize]
public sealed class ClassroomsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "perm:structure.view")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListClassroomsQuery(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Create(CreateClassroomCommand command, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Update(Guid id, UpdateClassroomCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }

        return Ok(await mediator.Send(command, cancellationToken));
    }
}
