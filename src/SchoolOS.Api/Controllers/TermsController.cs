using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Structure;

namespace SchoolOS.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class TermsController(IMediator mediator) : ControllerBase
{
    [HttpGet("academic-years/{yearId:guid}/terms")]
    [Authorize(Policy = "perm:structure.view")]
    public async Task<IActionResult> List(Guid yearId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListAcademicTermsQuery(yearId), cancellationToken));

    [HttpPost("academic-years/{yearId:guid}/terms")]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Create(Guid yearId, AcademicTermInput input, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(
            new CreateAcademicTermCommand(yearId, input.Name, input.Sequence, input.StartDate, input.EndDate),
            cancellationToken));

    [HttpPut("terms/{id:guid}")]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Update(Guid id, AcademicTermInput input, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(
            new UpdateAcademicTermCommand(id, input.Name, input.Sequence, input.StartDate, input.EndDate),
            cancellationToken));

    [HttpPost("terms/{id:guid}/activate")]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ActivateAcademicTermCommand(id), cancellationToken));

    [HttpPost("terms/{id:guid}/close")]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Close(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new CloseAcademicTermCommand(id), cancellationToken));
}
