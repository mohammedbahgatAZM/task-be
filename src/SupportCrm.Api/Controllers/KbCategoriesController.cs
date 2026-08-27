namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.KnowledgeBase;

[ApiController]
[Route("api/kb/categories")]
public class KbCategoriesController(KbCategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KbCategoryDto>>> GetAll(CancellationToken ct) =>
        Ok(await categoryService.GetActiveAsync(ct));

    [HttpPost]
    public async Task<ActionResult<KbCategoryDto>> Create([FromBody] CreateKbCategoryRequest request, CancellationToken ct)
    {
        try { return await categoryService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}
