using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Structure;

namespace SchoolOS.Api.Controllers;

[ApiController]
[Route("api/school-settings")]
[Authorize]
public sealed class SchoolSettingsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "perm:structure.view")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new GetSchoolSettingsQuery(), cancellationToken));

    [HttpPut]
    [Authorize(Policy = "perm:structure.manage")]
    public async Task<IActionResult> Update(UpdateSchoolSettingsCommand command, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(command, cancellationToken));
}
