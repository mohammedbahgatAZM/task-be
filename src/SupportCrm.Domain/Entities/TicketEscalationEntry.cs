namespace SupportCrm.Domain.Entities;

public class TicketEscalationEntry
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid? EscalatedToAgentId { get; private set; }
    public Guid? EscalatedToTeamId { get; private set; }
    public string Reason { get; private set; } = default!;
    public string EscalatedBy { get; private set; } = default!;
    public DateTimeOffset EscalatedAtUtc { get; private set; }

    private TicketEscalationEntry() { } // EF Core

    public TicketEscalationEntry(Guid ticketId, Guid? escalatedToAgentId, Guid? escalatedToTeamId, string reason, string escalatedBy, DateTimeOffset escalatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to escalate a ticket.", nameof(reason));
        if (escalatedToAgentId is null && escalatedToTeamId is null)
            throw new ArgumentException("Escalation must target an agent or a team.", nameof(escalatedToAgentId));

        Id = Guid.NewGuid();
        TicketId = ticketId;
        EscalatedToAgentId = escalatedToAgentId;
        EscalatedToTeamId = escalatedToTeamId;
        Reason = reason;
        EscalatedBy = string.IsNullOrWhiteSpace(escalatedBy) ? "unknown" : escalatedBy;
        EscalatedAtUtc = escalatedAtUtc;
    }
}
