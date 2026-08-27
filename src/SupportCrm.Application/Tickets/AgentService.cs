namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AgentService(IAgentRepository repository)
{
    public async Task<AgentDto> CreateAsync(CreateAgentRequest request, CancellationToken ct)
    {
        var agent = new Agent(request.Name.Trim());
        await repository.AddAsync(agent, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(agent);
    }

    public async Task<IReadOnlyList<AgentDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task SetAvailabilityAsync(Guid agentId, bool isAvailable, CancellationToken ct)
    {
        var agent = await repository.GetByIdAsync(agentId, ct) ?? throw new KeyNotFoundException($"Agent '{agentId}' was not found.");
        agent.SetAvailability(isAvailable);
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetSensitiveDataAccessAsync(Guid agentId, bool canView, CancellationToken ct)
    {
        var agent = await repository.GetByIdAsync(agentId, ct) ?? throw new KeyNotFoundException($"Agent '{agentId}' was not found.");
        agent.SetSensitiveDataAccess(canView);
        await repository.SaveChangesAsync(ct);
    }

    public Task AddSkillAsync(Guid agentId, string skill, CancellationToken ct) => repository.AddSkillAsync(agentId, skill.Trim(), ct);

    public Task<IReadOnlyList<string>> GetSkillsAsync(Guid agentId, CancellationToken ct) => repository.GetSkillsAsync(agentId, ct);

    public Task AddLanguageAsync(Guid agentId, string language, CancellationToken ct) => repository.AddLanguageAsync(agentId, language.Trim(), ct);

    public Task<IReadOnlyList<string>> GetLanguagesAsync(Guid agentId, CancellationToken ct) => repository.GetLanguagesAsync(agentId, ct);

    public async Task SetSupervisorAsync(Guid agentId, bool isSupervisor, CancellationToken ct)
    {
        var agent = await repository.GetByIdAsync(agentId, ct) ?? throw new KeyNotFoundException($"Agent '{agentId}' was not found.");
        agent.SetSupervisor(isSupervisor);
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetKnowledgeBaseEditorAsync(Guid agentId, bool isEditor, CancellationToken ct)
    {
        var agent = await repository.GetByIdAsync(agentId, ct) ?? throw new KeyNotFoundException($"Agent '{agentId}' was not found.");
        agent.SetKnowledgeBaseEditor(isEditor);
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetPreferredLanguageAsync(Guid agentId, string language, CancellationToken ct)
    {
        var agent = await repository.GetByIdAsync(agentId, ct) ?? throw new KeyNotFoundException($"Agent '{agentId}' was not found.");
        agent.SetPreferredLanguage(language);
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetDepartmentAsync(Guid agentId, Guid? departmentId, CancellationToken ct)
    {
        var agent = await repository.GetByIdAsync(agentId, ct) ?? throw new KeyNotFoundException($"Agent '{agentId}' was not found.");
        agent.SetDepartment(departmentId);
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetBranchAsync(Guid agentId, Guid? branchId, CancellationToken ct)
    {
        var agent = await repository.GetByIdAsync(agentId, ct) ?? throw new KeyNotFoundException($"Agent '{agentId}' was not found.");
        agent.SetBranch(branchId);
        await repository.SaveChangesAsync(ct);
    }

    private static AgentDto ToDto(Agent a) => new(a.Id, a.Name, a.IsAvailable, a.CanViewSensitiveData, a.IsSupervisor, a.IsKnowledgeBaseEditor, a.PreferredLanguage, a.DepartmentId, a.BranchId);
}
