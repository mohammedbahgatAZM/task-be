namespace SupportCrm.Domain.Entities;

public class TicketStatusChangeEntry
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public TicketStatus? OldStatus { get; private set; } // null for the initial "Created" entry
    public TicketStatus NewStatus { get; private set; }
    public string ChangedBy { get; private set; } = default!;
    public string ChangedByKind { get; private set; } = default!; // "Agent" | "System"
    public string? Reason { get; private set; }
    public DateTimeOffset ChangedAtUtc { get; private set; }

    private TicketStatusChangeEntry() { } // EF Core

    public TicketStatusChangeEntry(Guid ticketId, TicketStatus? oldStatus, TicketStatus newStatus,
        string changedBy, string changedByKind, string? reason, DateTimeOffset changedAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        ChangedByKind = changedByKind is "Agent" or "System" ? changedByKind : "Agent";
        Reason = reason;
        ChangedAtUtc = changedAtUtc;
    }
}
