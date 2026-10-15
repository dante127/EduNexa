using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolOS.Application.Students;

namespace SchoolOS.Api.Controllers;

public sealed record VerifyDocumentRequest(string Status, string? Notes);

[ApiController]
[Route("api/students/{studentId:guid}/documents")]
[Authorize]
public sealed class StudentDocumentsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "perm:students.view")]
    public async Task<IActionResult> List(Guid studentId, CancellationToken cancellationToken) =>
        Ok(await mediator.Send(new ListStudentDocumentsQuery(studentId), cancellationToken));

    [HttpPost]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Upload(
        Guid studentId,
        IFormFile file,
        [FromForm] string documentType,
        [FromForm] string? notes,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        return Ok(await mediator.Send(
            new UploadStudentDocumentCommand(
                studentId,
                documentType,
                file.FileName,
                file.ContentType,
                file.Length,
                content,
                CurrentUserId(),
                notes),
            cancellationToken));
    }

    [HttpGet("{documentId:guid}/download")]
    [Authorize(Policy = "perm:students.view")]
    public async Task<IActionResult> Download(Guid studentId, Guid documentId, CancellationToken cancellationToken)
    {
        var download = await mediator.Send(new GetStudentDocumentDownloadQuery(studentId, documentId), cancellationToken);
        return File(download.Content, download.MimeType, download.FileName);
    }

    [HttpPost("{documentId:guid}/verify")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Verify(
        Guid studentId,
        Guid documentId,
        VerifyDocumentRequest request,
        CancellationToken cancellationToken) =>
        Ok(await mediator.Send(
            new VerifyStudentDocumentCommand(studentId, documentId, request.Status, request.Notes, CurrentUserId()),
            cancellationToken));

    [HttpDelete("{documentId:guid}")]
    [Authorize(Policy = "perm:students.manage")]
    public async Task<IActionResult> Delete(Guid studentId, Guid documentId, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteStudentDocumentCommand(studentId, documentId), cancellationToken);
        return NoContent();
    }

    private Guid CurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(value, out var id) ? id : Guid.Empty;
    }
}
