namespace SupportCrm.Domain.Entities;

public class EscalationLogEntry
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid EscalationRuleId { get; private set; }
    public int TierNumber { get; private set; }
    public string ActionSummary { get; private set; } = default!;
    public DateTimeOffset TriggeredAtUtc { get; private set; }

    private EscalationLogEntry() { } // EF Core

    public EscalationLogEntry(Guid ticketId, Guid escalationRuleId, int tierNumber, string actionSummary, DateTimeOffset triggeredAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        EscalationRuleId = escalationRuleId;
        TierNumber = tierNumber;
        ActionSummary = actionSummary;
        TriggeredAtUtc = triggeredAtUtc;
    }
}
