namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/teams")]
public class TeamsController(TeamService teamService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TeamDto>>> GetAll(CancellationToken ct) =>
        Ok(await teamService.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<TeamDto>> Create([FromBody] CreateTeamRequest request, CancellationToken ct) =>
        await teamService.CreateAsync(request, ct);

    [HttpPut("{id:guid}/department")]
    public async Task<IActionResult> SetDepartment(Guid id, [FromBody] SupportCrm.Application.Platform.SetDepartmentIdRequest request, CancellationToken ct)
    {
        try { await teamService.SetDepartmentAsync(id, request.DepartmentId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
