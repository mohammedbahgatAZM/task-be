namespace SupportCrm.Domain.Entities;

public class ContactDetailChangeLogEntry
{
    public Guid Id { get; private set; }
    public Guid ContactDetailId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string ChangeType { get; private set; } = default!; // "Created" | "ValueChanged" | "PrimaryChanged"
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public string ChangedBy { get; private set; } = default!;
    public DateTimeOffset ChangedAtUtc { get; private set; }

    private ContactDetailChangeLogEntry() { } // EF Core

    public ContactDetailChangeLogEntry(Guid contactDetailId, Guid customerId, string changeType, string? oldValue, string? newValue, string changedBy, DateTimeOffset changedAtUtc)
    {
        Id = Guid.NewGuid();
        ContactDetailId = contactDetailId;
        CustomerId = customerId;
        ChangeType = changeType;
        OldValue = oldValue;
        NewValue = newValue;
        ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        ChangedAtUtc = changedAtUtc;
    }
}
