namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/sla/alerts")]
public class SlaAlertsController(SlaAlertService alertService) : ControllerBase
{
    [HttpGet("preferences/{agentId:guid}")]
    public async Task<ActionResult<AlertPreferenceDto>> GetPreference(Guid agentId, CancellationToken ct) =>
        await alertService.GetPreferenceAsync(agentId, ct);

    [HttpPut("preferences/{agentId:guid}")]
    public async Task<ActionResult<AlertPreferenceDto>> SetPreference(Guid agentId, [FromBody] SetAlertPreferenceRequest request, CancellationToken ct)
    {
        try { return await alertService.SetPreferenceAsync(agentId, request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    // "Available to managers" per the AC is a product/UI convention, not a backend role check —
    // no RBAC exists yet in this codebase.
    [HttpGet("at-risk")]
    public async Task<ActionResult<IReadOnlyList<AtRiskTicketDto>>> GetAtRisk(CancellationToken ct) =>
        Ok(await alertService.GetAtRiskTicketsAsync(ct));
}
