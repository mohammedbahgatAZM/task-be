namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Platform;

[ApiController]
[Route("api/departments")]
public class DepartmentsController(DepartmentService departmentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAll(CancellationToken ct) =>
        Ok(await departmentService.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create([FromBody] CreateDepartmentRequest request, CancellationToken ct) =>
        await departmentService.CreateAsync(request, ct);

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        try { await departmentService.SetActiveAsync(id, true, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        try { await departmentService.SetActiveAsync(id, false, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/default-channel")]
    public async Task<IActionResult> SetDefaultChannel(Guid id, [FromBody] SetDepartmentChannelRequest request, CancellationToken ct)
    {
        try { await departmentService.SetDefaultChannelAsync(id, request, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
