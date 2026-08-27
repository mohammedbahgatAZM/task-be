namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.KnowledgeBase;

[ApiController]
[Route("api/kb/articles")]
public class ArticlesController(ArticleService articleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ArticleDto>>> GetAll([FromQuery] Guid? categoryId, [FromQuery] bool includeUnpublished, CancellationToken ct) =>
        Ok(categoryId is null
            ? await articleService.GetAllAsync(includeUnpublished, ct)
            : await articleService.GetByCategoryAsync(categoryId.Value, includeUnpublished, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ArticleDto>> GetById(Guid id, CancellationToken ct)
    {
        try { return await articleService.GetByIdAndTrackViewAsync(id, ct); }
        catch (ArticleNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    public async Task<ActionResult<ArticleDto>> Create([FromBody] CreateArticleRequest request, CancellationToken ct)
    {
        try { return await articleService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ArticleDto>> Update(Guid id, [FromBody] UpdateArticleRequest request, CancellationToken ct)
    {
        try { return await articleService.UpdateAsync(id, request, ct); }
        catch (ArticleNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/helpful")]
    public async Task<IActionResult> MarkHelpful(Guid id, CancellationToken ct)
    {
        try { await articleService.MarkHelpfulAsync(id, ct); return NoContent(); }
        catch (ArticleNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/not-helpful")]
    public async Task<IActionResult> MarkNotHelpful(Guid id, CancellationToken ct)
    {
        try { await articleService.MarkNotHelpfulAsync(id, ct); return NoContent(); }
        catch (ArticleNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ArticleAttachmentDto>> UploadAttachment(Guid id, IFormFile file, [FromQuery] string? uploadedByName, [FromServices] ArticleAttachmentService attachmentService, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("A file is required.");
        try
        {
            await using var stream = file.OpenReadStream();
            return await attachmentService.AddAsync(id, file.FileName, file.ContentType, file.Length, stream, uploadedByName ?? "unknown", ct);
        }
        catch (ArticleNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IReadOnlyList<ArticleAttachmentDto>>> GetAttachments(Guid id, [FromServices] ArticleAttachmentService attachmentService, CancellationToken ct) =>
        Ok(await attachmentService.GetForArticleAsync(id, ct));

    [HttpGet("attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, [FromServices] ArticleAttachmentService attachmentService, CancellationToken ct)
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
        try { await workflowService.SubmitForReviewAsync("Article", id, request, ct); return NoContent(); }
        catch (ArticleNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, [FromBody] PublishContentRequest request, [FromServices] ContentWorkflowService workflowService, CancellationToken ct)
    {
        try { await workflowService.PublishAsync("Article", id, request, ct); return NoContent(); }
        catch (ArticleNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, [FromBody] TransitionContentRequest request, [FromServices] ContentWorkflowService workflowService, CancellationToken ct)
    {
        try { await workflowService.UnpublishAsync("Article", id, request, ct); return NoContent(); }
        catch (ArticleNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, [FromBody] TransitionContentRequest request, [FromServices] ContentWorkflowService workflowService, CancellationToken ct)
    {
        try { await workflowService.ArchiveAsync("Article", id, request, ct); return NoContent(); }
        catch (ArticleNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<ContentVersionDto>>> GetVersions(Guid id, [FromServices] ContentWorkflowService workflowService, CancellationToken ct) =>
        Ok(await workflowService.GetVersionHistoryAsync("Article", id, ct));
}
