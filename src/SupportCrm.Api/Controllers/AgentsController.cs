namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/agents")]
public class AgentsController(AgentService agentService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentDto>>> GetAll(CancellationToken ct) =>
        Ok(await agentService.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<AgentDto>> Create([FromBody] CreateAgentRequest request, CancellationToken ct) =>
        await agentService.CreateAsync(request, ct);

    [HttpPut("{id:guid}/availability")]
    public async Task<IActionResult> SetAvailability(Guid id, [FromBody] SetAgentAvailabilityRequest request, CancellationToken ct)
    {
        try { await agentService.SetAvailabilityAsync(id, request.IsAvailable, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/sensitive-data-access")]
    public async Task<IActionResult> SetSensitiveDataAccess(Guid id, [FromBody] SetAgentSensitiveDataAccessRequest request, CancellationToken ct)
    {
        try { await agentService.SetSensitiveDataAccessAsync(id, request.CanViewSensitiveData, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/skills")]
    public async Task<IActionResult> AddSkill(Guid id, [FromBody] AddAgentSkillRequest request, CancellationToken ct)
    {
        await agentService.AddSkillAsync(id, request.Skill, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/skills")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetSkills(Guid id, CancellationToken ct) =>
        Ok(await agentService.GetSkillsAsync(id, ct));

    [HttpPost("{id:guid}/languages")]
    public async Task<IActionResult> AddLanguage(Guid id, [FromBody] AddAgentLanguageRequest request, CancellationToken ct)
    {
        await agentService.AddLanguageAsync(id, request.Language, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/languages")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetLanguages(Guid id, CancellationToken ct) =>
        Ok(await agentService.GetLanguagesAsync(id, ct));

    [HttpPut("{id:guid}/supervisor")]
    public async Task<IActionResult> SetSupervisor(Guid id, [FromBody] SetAgentSupervisorRequest request, CancellationToken ct)
    {
        try { await agentService.SetSupervisorAsync(id, request.IsSupervisor, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/kb-editor")]
    public async Task<IActionResult> SetKnowledgeBaseEditor(Guid id, [FromBody] SetAgentKnowledgeBaseEditorRequest request, CancellationToken ct)
    {
        try { await agentService.SetKnowledgeBaseEditorAsync(id, request.IsKnowledgeBaseEditor, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/language")]
    public async Task<IActionResult> SetLanguage(Guid id, [FromBody] SetAgentLanguageRequest request, CancellationToken ct)
    {
        try { await agentService.SetPreferredLanguageAsync(id, request.Language, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/department")]
    public async Task<IActionResult> SetDepartment(Guid id, [FromBody] SupportCrm.Application.Platform.SetDepartmentIdRequest request, CancellationToken ct)
    {
        try { await agentService.SetDepartmentAsync(id, request.DepartmentId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/branch")]
    public async Task<IActionResult> SetBranch(Guid id, [FromBody] SupportCrm.Application.Platform.SetBranchIdRequest request, CancellationToken ct)
    {
        try { await agentService.SetBranchAsync(id, request.BranchId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
