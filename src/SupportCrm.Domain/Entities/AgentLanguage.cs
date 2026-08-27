namespace SupportCrm.Domain.Entities;

public class AgentLanguage
{
    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public string Language { get; private set; } = default!;

    private AgentLanguage() { } // EF Core

    public AgentLanguage(Guid agentId, string language)
    {
        if (string.IsNullOrWhiteSpace(language))
            throw new ArgumentException("Language is required.", nameof(language));
        Id = Guid.NewGuid();
        AgentId = agentId;
        Language = language;
    }
}
