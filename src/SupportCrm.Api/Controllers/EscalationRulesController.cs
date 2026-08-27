namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/escalation-rules")]
public class EscalationRulesController(EscalationRuleService ruleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EscalationRuleDto>>> GetAll(CancellationToken ct) =>
        Ok(await ruleService.GetActiveOrderedAsync(ct));

    [HttpPost]
    public async Task<ActionResult<EscalationRuleDto>> Create([FromBody] CreateEscalationRuleRequest request, CancellationToken ct)
    {
        try { return await ruleService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:guid}/tiers")]
    public async Task<ActionResult<IReadOnlyList<EscalationTierDto>>> GetTiers(Guid id, CancellationToken ct) =>
        Ok(await ruleService.GetTiersAsync(id, ct));

    [HttpPost("{id:guid}/tiers")]
    public async Task<ActionResult<EscalationTierDto>> AddTier(Guid id, [FromBody] CreateEscalationTierRequest request, CancellationToken ct)
    {
        try { return await ruleService.AddTierAsync(id, request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}
