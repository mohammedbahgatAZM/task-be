namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/quick-reply-templates")]
public class QuickReplyTemplatesController(QuickReplyTemplateService templateService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuickReplyTemplateDto>>> GetAll(CancellationToken ct) =>
        Ok(await templateService.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<QuickReplyTemplateDto>> Create([FromBody] CreateQuickReplyTemplateRequest request, CancellationToken ct) =>
        await templateService.CreateAsync(request, ct);

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<QuickReplyTemplateDto>> Update(Guid id, [FromBody] UpdateQuickReplyTemplateRequest request, CancellationToken ct)
    {
        try { return await templateService.UpdateAsync(id, request, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/retire")]
    public async Task<IActionResult> Retire(Guid id, CancellationToken ct)
    {
        try { await templateService.RetireAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/render")]
    public async Task<ActionResult<RenderedQuickReplyDto>> Render(Guid id, [FromBody] RenderQuickReplyTemplateRequest request, CancellationToken ct)
    {
        try { return await templateService.RenderAsync(id, request.TicketId, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (TicketNotFoundException) { return NotFound(); }
    }
}
