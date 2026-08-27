namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.KnowledgeBase;

[ApiController]
[Route("api/kb/search")]
public class KbSearchController(KbSearchService searchService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<KbSearchResponseDto>> Search([FromQuery] string q, [FromQuery] int take, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q)) return BadRequest("A query is required.");
        return Ok(await searchService.SearchAsync(q, take <= 0 ? 20 : take, ct));
    }
}
