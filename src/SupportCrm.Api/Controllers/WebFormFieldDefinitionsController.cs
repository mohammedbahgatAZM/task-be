namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/web-form-fields")]
public class WebFormFieldDefinitionsController(WebFormFieldDefinitionService fieldDefinitionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<WebFormFieldDefinitionDto>>> GetByCategory([FromQuery] Guid categoryId, CancellationToken ct) =>
        Ok(await fieldDefinitionService.GetByCategoryAsync(categoryId, ct));

    [HttpPost]
    public async Task<ActionResult<WebFormFieldDefinitionDto>> Create([FromBody] CreateWebFormFieldDefinitionRequest request, CancellationToken ct) =>
        await fieldDefinitionService.CreateAsync(request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await fieldDefinitionService.DeleteAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
