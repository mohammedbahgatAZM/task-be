namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/ticket-categories")]
public class TicketCategoriesController(TicketCategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketCategoryDto>>> GetAll(CancellationToken ct) =>
        Ok(await categoryService.GetActiveAsync(ct));

    [HttpPost]
    public async Task<ActionResult<TicketCategoryDto>> Create([FromBody] CreateTicketCategoryRequest request, CancellationToken ct) =>
        await categoryService.CreateAsync(request, ct);

    [HttpPut("{id:guid}/department")]
    public async Task<IActionResult> SetDepartment(Guid id, [FromBody] SupportCrm.Application.Platform.SetDepartmentIdRequest request, CancellationToken ct)
    {
        try { await categoryService.SetDepartmentAsync(id, request.DepartmentId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
