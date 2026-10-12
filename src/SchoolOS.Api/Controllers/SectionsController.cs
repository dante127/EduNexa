using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Structure;

namespace SchoolOS.Api.Controllers;

[ApiController]
[Route("api/sections")]
[Authorize]
public sealed class SectionsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "perm:structure.view")]
    public async Task<IActionResult> List(
        [FromQuery] Guid? academicYearId,
        [FromQuery] Guid? gradeLevelId,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListClassSectionsQuery(academicYearId, gradeLevelId), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Create(CreateClassSectionCommand command, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Update(Guid id, UpdateClassSectionCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }

        return Ok(await mediator.Send(command, cancellationToken));
    }
}
