namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.KnowledgeBase;
using SupportCrm.Application.CustomerPortal;

[ApiController]
[Route("api/kb/faqs")]
public class FaqsController(FaqService faqService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FaqDto>>> GetAll([FromQuery] Guid? categoryId, CancellationToken ct) =>
        Ok(categoryId is null ? await faqService.GetAllAsync(ct) : await faqService.GetByCategoryAsync(categoryId.Value, ct));

    [HttpPost]
    public async Task<ActionResult<FaqDto>> Create([FromBody] CreateFaqRequest request, CancellationToken ct)
    {
        try { return await faqService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:guid}/helpful")]
    public async Task<IActionResult> MarkHelpful(Guid id, CancellationToken ct)
    {
        try { await faqService.MarkHelpfulAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/not-helpful")]
    public async Task<IActionResult> MarkNotHelpful(Guid id, CancellationToken ct)
    {
        try { await faqService.MarkNotHelpfulAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // Manager review view — no RBAC exists yet, so this is unrestricted like everything else,
    // but conceptually the "unhelpful ratings visible to knowledge base managers" AC.
    [HttpGet("most-unhelpful")]
    public async Task<ActionResult<IReadOnlyList<FaqDto>>> GetMostUnhelpful([FromQuery] int take, CancellationToken ct) =>
        Ok(await faqService.GetMostUnhelpfulAsync(take <= 0 ? 20 : take, ct));

    // CP-4 — portal impression/deflection analytics, layered on top of the existing Faq entity
    [HttpPost("{id:guid}/impression")]
    public async Task<IActionResult> LogImpression(Guid id, [FromBody] LogFaqImpressionRequest request, [FromServices] FaqPortalAnalyticsService analyticsService, CancellationToken ct)
    {
        try { await analyticsService.LogImpressionAsync(id, request.DraftSessionId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("deflection/mark-converted")]
    public async Task<IActionResult> MarkConverted([FromBody] MarkDraftSessionConvertedRequest request, [FromServices] FaqPortalAnalyticsService analyticsService, CancellationToken ct)
    {
        await analyticsService.MarkSessionConvertedAsync(request.DraftSessionId, ct);
        return NoContent();
    }

    [HttpGet("deflection-report")]
    public async Task<ActionResult<IReadOnlyList<FaqDeflectionReportItemDto>>> GetDeflectionReport([FromServices] FaqPortalAnalyticsService analyticsService, CancellationToken ct) =>
        Ok(await analyticsService.GetDeflectionReportAsync(ct));
}
