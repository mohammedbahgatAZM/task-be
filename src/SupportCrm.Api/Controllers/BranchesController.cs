namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Platform;

[ApiController]
[Route("api/branches")]
public class BranchesController(BranchService branchService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetAll(CancellationToken ct) =>
        Ok(await branchService.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<BranchDto>> Create([FromBody] CreateBranchRequest request, CancellationToken ct) =>
        await branchService.CreateAsync(request, ct);

    [HttpPut("{id:guid}/settings")]
    public async Task<IActionResult> UpdateSettings(Guid id, [FromBody] UpdateBranchSettingsRequest request, CancellationToken ct)
    {
        try { await branchService.UpdateSettingsAsync(id, request, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/activate")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        try { await branchService.SetActiveAsync(id, true, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/deactivate")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        try { await branchService.SetActiveAsync(id, false, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
