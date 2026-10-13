using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Students;

namespace SchoolOS.Api.Controllers;

[ApiController]
[Route("api/guardians")]
[Authorize]
public sealed class GuardiansController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "perm:students.view")]
    public async Task<IActionResult> List([FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListGuardiansQuery(search), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Create(CreateGuardianCommand command, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Update(Guid id, UpdateGuardianCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }

        return Ok(await mediator.Send(command, cancellationToken));
    }
}
