namespace SupportCrm.Domain.Entities;

public class TicketFieldChangeEntry
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string FieldName { get; private set; } = default!; // "Category" | "Priority"
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public string ChangedBy { get; private set; } = default!;
    public DateTimeOffset ChangedAtUtc { get; private set; }

    private TicketFieldChangeEntry() { } // EF Core

    public TicketFieldChangeEntry(Guid ticketId, string fieldName, string? oldValue, string? newValue, string changedBy, DateTimeOffset changedAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        FieldName = fieldName;
        OldValue = oldValue;
        NewValue = newValue;
        ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        ChangedAtUtc = changedAtUtc;
    }
}
