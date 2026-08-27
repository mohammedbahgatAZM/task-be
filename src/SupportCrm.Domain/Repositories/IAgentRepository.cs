namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Agent>> GetAllAsync(CancellationToken ct);
    Task AddAsync(Agent agent, CancellationToken ct);
    Task<IReadOnlyList<Agent>> GetBySkillAsync(string skill, CancellationToken ct);
    Task AddSkillAsync(Guid agentId, string skill, CancellationToken ct);
    Task<IReadOnlyList<string>> GetSkillsAsync(Guid agentId, CancellationToken ct);
    Task AddLanguageAsync(Guid agentId, string language, CancellationToken ct);
    Task<IReadOnlyList<string>> GetLanguagesAsync(Guid agentId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
