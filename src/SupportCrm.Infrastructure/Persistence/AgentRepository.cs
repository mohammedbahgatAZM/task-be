namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AgentRepository(SupportCrmDbContext dbContext) : IAgentRepository
{
    public Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Agents.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Agent>> GetAllAsync(CancellationToken ct) =>
        await dbContext.Agents.ToListAsync(ct);

    public Task AddAsync(Agent agent, CancellationToken ct)
    {
        dbContext.Agents.Add(agent);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Agent>> GetBySkillAsync(string skill, CancellationToken ct) =>
        await dbContext.Agents
            .Where(a => dbContext.AgentSkills.Any(s => s.AgentId == a.Id && s.Skill == skill))
            .ToListAsync(ct);

    public Task AddSkillAsync(Guid agentId, string skill, CancellationToken ct)
    {
        dbContext.AgentSkills.Add(new AgentSkill(agentId, skill));
        return dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetSkillsAsync(Guid agentId, CancellationToken ct) =>
        await dbContext.AgentSkills.Where(s => s.AgentId == agentId).Select(s => s.Skill).ToListAsync(ct);

    public Task AddLanguageAsync(Guid agentId, string language, CancellationToken ct)
    {
        dbContext.AgentLanguages.Add(new AgentLanguage(agentId, language));
        return dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetLanguagesAsync(Guid agentId, CancellationToken ct) =>
        await dbContext.AgentLanguages.Where(l => l.AgentId == agentId).Select(l => l.Language).ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
