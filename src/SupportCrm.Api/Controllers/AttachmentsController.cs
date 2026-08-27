namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Customers;

[ApiController]
[Route("api/customers/{customerId:guid}/attachments")]
public class AttachmentsController(NoteAndAttachmentService noteAndAttachmentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentDto>>> GetAll(Guid customerId, CancellationToken ct) =>
        Ok(await noteAndAttachmentService.GetAttachmentsAsync(customerId, ct));

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<AttachmentDto>> Upload(Guid customerId, IFormFile file, [FromQuery] string? uploadedByName, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest("A file is required.");

        try
        {
            await using var stream = file.OpenReadStream();
            var dto = await noteAndAttachmentService.AddAttachmentAsync(
                customerId, file.FileName, file.ContentType, file.Length, stream, uploadedByName ?? "unknown", ct);
            return dto;
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
        catch (AttachmentTooLargeException ex)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, ex.Message);
        }
    }

    [HttpGet("{attachmentId:guid}/download")]
    public async Task<IActionResult> Download(Guid attachmentId, CancellationToken ct)
    {
        try
        {
            var (content, attachment) = await noteAndAttachmentService.OpenAttachmentAsync(attachmentId, ct);
            return File(content, attachment.ContentType, attachment.FileName);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
