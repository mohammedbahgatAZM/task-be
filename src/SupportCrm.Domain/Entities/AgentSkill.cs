namespace SupportCrm.Domain.Entities;

public class AgentSkill
{
    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public string Skill { get; private set; } = default!;

    private AgentSkill() { } // EF Core

    public AgentSkill(Guid agentId, string skill)
    {
        if (string.IsNullOrWhiteSpace(skill))
            throw new ArgumentException("Skill is required.", nameof(skill));
        Id = Guid.NewGuid();
        AgentId = agentId;
        Skill = skill;
    }
}
