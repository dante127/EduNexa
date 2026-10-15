using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Staff;

namespace SchoolOS.Api.Controllers;

public sealed record LinkUserRequest(Guid UserId);

[ApiController]
[Route("api/staff")]
[Authorize]
public sealed class StaffController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "perm:staff.view")]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await mediator.Send(new ListStaffQuery(search, status, page, pageSize), cancellationToken));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "perm:staff.view")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetStaffQuery(id), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "perm:staff.manage")]
    public async Task<IActionResult> Create(CreateStaffCommand command, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(command, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "perm:staff.manage")]
    public async Task<IActionResult> Update(Guid id, UpdateStaffCommand command, CancellationToken cancellationToken)
    {
        if (id != command.Id)
        {
            return BadRequest();
        }

        return Ok(await mediator.Send(command, cancellationToken));
    }

    [HttpPut("{id:guid}/user")]
    [Authorize(Policy = "perm:staff.manage")]
    public async Task<IActionResult> LinkUser(Guid id, LinkUserRequest request, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new LinkStaffUserCommand(id, request.UserId), cancellationToken));

    [HttpDelete("{id:guid}/user")]
    [Authorize(Policy = "perm:staff.manage")]
    public async Task<IActionResult> UnlinkUser(Guid id, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new UnlinkStaffUserCommand(id), cancellationToken));
}
