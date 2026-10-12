using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Structure;

namespace SchoolOS.Api.Controllers;

[ApiController]
[Route("api/subjects")]
[Authorize]
public sealed class SubjectsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "perm:structure.view")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListSubjectsQuery(), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Create(CreateSubjectCommand command, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Update(Guid id, UpdateSubjectCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }

        return Ok(await mediator.Send(command, cancellationToken));
    }
}
