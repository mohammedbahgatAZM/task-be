namespace SupportCrm.Domain.Entities;

// One row per (ticket, kind) ever sent — the dedupe guard so a ticket's Warning/Breach alert
// fires exactly once each, not once per SlaEscalationHostedService tick.
public class SlaAlertLog
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string Kind { get; private set; } = default!; // "Warning" | "Breach"
    public DateTimeOffset SentAtUtc { get; private set; }

    private SlaAlertLog() { } // EF Core

    public SlaAlertLog(Guid ticketId, string kind, DateTimeOffset sentAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        Kind = kind;
        SentAtUtc = sentAtUtc;
    }
}
