using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Structure;

namespace SchoolOS.Api.Controllers;

[ApiController]
[Route("api/academic-years")]
[Authorize]
public sealed class AcademicYearsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "perm:structure.view")]
    public async Task<IActionResult> List(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListAcademicYearsQuery(), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:structure.view")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetAcademicYearQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Create(CreateAcademicYearCommand command, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Update(Guid id, UpdateAcademicYearCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }

        return Ok(await mediator.Send(command, cancellationToken));
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ActivateAcademicYearCommand(id), cancellationToken));

    [HttpPost("{id:guid}/close")]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new CloseAcademicYearCommand(id), cancellationToken));
}
