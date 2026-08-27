namespace SupportCrm.Domain.Entities;

public class TicketCollaborator
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AgentId { get; private set; }
    public DateTimeOffset AddedAtUtc { get; private set; }

    private TicketCollaborator() { } // EF Core

    public TicketCollaborator(Guid ticketId, Guid agentId, DateTimeOffset addedAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        AgentId = agentId;
        AddedAtUtc = addedAtUtc;
    }
}
