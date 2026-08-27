namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/web-form-submissions")]
public class WebFormSubmissionsController(WebFormSubmissionService submissionService, TicketAttachmentService attachmentService) : ControllerBase
{
    private static readonly HashSet<string> AllowedFileContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "application/pdf", "text/plain"
    };
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    [HttpPost]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<WebFormSubmissionResultDto>> Submit(
        [FromForm] Guid categoryId, [FromForm] string requesterName, [FromForm] string requesterContactValue,
        [FromForm] Dictionary<string, string> fieldValues, IFormFileCollection? files, CancellationToken ct)
    {
        WebFormSubmissionResultDto result;
        try
        {
            result = await submissionService.SubmitAsync(new SubmitWebFormRequest(categoryId, requesterName, requesterContactValue, fieldValues), ct);
        }
        catch (WebFormValidationException ex)
        {
            return BadRequest(new { errors = ex.Errors });
        }

        if (files is { Count: > 0 })
        {
            foreach (var file in files)
            {
                // Disallowed files are silently skipped rather than failing the whole
                // submission after the ticket already exists — the ticket itself is still
                // valid without them, only the attachment is dropped.
                if (file.Length == 0 || file.Length > MaxFileSizeBytes || !AllowedFileContentTypes.Contains(file.ContentType))
                    continue;

                await using var stream = file.OpenReadStream();
                await attachmentService.AddAsync(result.TicketId, file.FileName, file.ContentType, file.Length, stream, requesterName, ct);
            }
        }

        return result;
    }
}
