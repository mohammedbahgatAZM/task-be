namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.KnowledgeBase;

[ApiController]
[Route("api/kb/guides")]
public class GuidesController(GuideService guideService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GuideDto>>> GetAll([FromQuery] Guid? ticketCategoryId, [FromQuery] bool includeUnpublished, CancellationToken ct) =>
        Ok(ticketCategoryId is null
            ? await guideService.GetAllAsync(includeUnpublished, ct)
            : await guideService.GetByTicketCategoryAsync(ticketCategoryId.Value, includeUnpublished, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GuideDto>> GetById(Guid id, CancellationToken ct)
    {
        try { return await guideService.GetByIdAsync(id, ct); }
        catch (GuideNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    public async Task<ActionResult<GuideDto>> Create([FromBody] CreateGuideRequest request, CancellationToken ct)
    {
        try { return await guideService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GuideDto>> Update(Guid id, [FromBody] UpdateGuideRequest request, CancellationToken ct)
    {
        try { return await guideService.UpdateAsync(id, request, ct); }
        catch (GuideNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/flag-outdated")]
    public async Task<IActionResult> FlagOutdated(Guid id, [FromBody] FlagGuideOutdatedRequest request, CancellationToken ct)
    {
        try { await guideService.FlagOutdatedAsync(id, request, ct); return NoContent(); }
        catch (GuideNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:guid}/categories")]
    public async Task<ActionResult<IReadOnlyList<Guid>>> GetLinkedCategories(Guid id, CancellationToken ct) =>
        Ok(await guideService.GetLinkedCategoriesAsync(id, ct));

    [HttpPost("{id:guid}/categories/{ticketCategoryId:guid}")]
    public async Task<IActionResult> LinkCategory(Guid id, Guid ticketCategoryId, [FromQuery] Guid editorAgentId, CancellationToken ct)
    {
        try { await guideService.LinkCategoryAsync(id, ticketCategoryId, editorAgentId, ct); return NoContent(); }
        catch (GuideNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpDelete("{id:guid}/categories/{ticketCategoryId:guid}")]
    public async Task<IActionResult> UnlinkCategory(Guid id, Guid ticketCategoryId, [FromQuery] Guid editorAgentId, CancellationToken ct)
    {
        try { await guideService.UnlinkCategoryAsync(id, ticketCategoryId, editorAgentId, ct); return NoContent(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<GuideAttachmentDto>> UploadAttachment(Guid id, IFormFile file, [FromQuery] string? uploadedByName, [FromServices] GuideAttachmentService attachmentService, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("A file is required.");
        try
        {
            await using var stream = file.OpenReadStream();
            return await attachmentService.AddAsync(id, file.FileName, file.ContentType, file.Length, stream, uploadedByName ?? "unknown", ct);
        }
        catch (GuideNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IReadOnlyList<GuideAttachmentDto>>> GetAttachments(Guid id, [FromServices] GuideAttachmentService attachmentService, CancellationToken ct) =>
        Ok(await attachmentService.GetForArticleAsync(id, ct));

    [HttpGet("attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, [FromServices] GuideAttachmentService attachmentService, CancellationToken ct)
    {
        try
        {
            var (content, attachment) = await attachmentService.OpenAsync(attachmentId, ct);
            return File(content, attachment.ContentType, attachment.FileName);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/submit-for-review")]
    public async Task<IActionResult> SubmitForReview(Guid id, [FromBody] TransitionContentRequest request, [FromServices] ContentWorkflowService workflowService, CancellationToken ct)
    {
        try { await workflowService.SubmitForReviewAsync("Guide", id, request, ct); return NoContent(); }
        catch (GuideNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, [FromBody] PublishContentRequest request, [FromServices] ContentWorkflowService workflowService, CancellationToken ct)
    {
        try { await workflowService.PublishAsync("Guide", id, request, ct); return NoContent(); }
        catch (GuideNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, [FromBody] TransitionContentRequest request, [FromServices] ContentWorkflowService workflowService, CancellationToken ct)
    {
        try { await workflowService.UnpublishAsync("Guide", id, request, ct); return NoContent(); }
        catch (GuideNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, [FromBody] TransitionContentRequest request, [FromServices] ContentWorkflowService workflowService, CancellationToken ct)
    {
        try { await workflowService.ArchiveAsync("Guide", id, request, ct); return NoContent(); }
        catch (GuideNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<ContentVersionDto>>> GetVersions(Guid id, [FromServices] ContentWorkflowService workflowService, CancellationToken ct) =>
        Ok(await workflowService.GetVersionHistoryAsync("Guide", id, ct));
}
