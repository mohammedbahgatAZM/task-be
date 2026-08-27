namespace SupportCrm.Domain.Entities;

public class TicketTask
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string Note { get; private set; } = default!;
    public DateTimeOffset DueAtUtc { get; private set; }
    public Guid AssignedAgentId { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTimeOffset? NotifiedAtUtc { get; private set; } // set once a "task due" notification has fired — prevents re-notifying on every poll
    public string CreatedBy { get; private set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private TicketTask() { } // EF Core

    public TicketTask(Guid ticketId, string note, DateTimeOffset dueAtUtc, Guid assignedAgentId, string createdBy, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("Task note is required.", nameof(note));

        Id = Guid.NewGuid();
        TicketId = ticketId;
        Note = note;
        DueAtUtc = dueAtUtc;
        AssignedAgentId = assignedAgentId;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
    }

    public void Complete() => IsCompleted = true;

    public void Reassign(Guid newAgentId) => AssignedAgentId = newAgentId;

    public void MarkNotified(DateTimeOffset atUtc) => NotifiedAtUtc = atUtc;
}
