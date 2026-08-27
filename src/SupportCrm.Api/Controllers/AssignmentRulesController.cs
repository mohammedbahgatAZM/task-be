namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/assignment-rules")]
public class AssignmentRulesController(AssignmentRuleService ruleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AssignmentRuleDto>>> GetAll(CancellationToken ct) =>
        Ok(await ruleService.GetActiveOrderedAsync(ct));

    [HttpPost]
    public async Task<ActionResult<AssignmentRuleDto>> Create([FromBody] CreateAssignmentRuleRequest request, CancellationToken ct)
    {
        try { return await ruleService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}
