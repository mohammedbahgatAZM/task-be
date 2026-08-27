namespace SupportCrm.Domain.Entities;

public class DigestLogEntry
{
    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public DateTimeOffset SentAtUtc { get; private set; }

    private DigestLogEntry() { } // EF Core

    public DigestLogEntry(Guid agentId, DateTimeOffset sentAtUtc)
    {
        Id = Guid.NewGuid();
        AgentId = agentId;
        SentAtUtc = sentAtUtc;
    }
}
