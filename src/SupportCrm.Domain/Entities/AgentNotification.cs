namespace SupportCrm.Domain.Entities;

public class AgentNotification
{
    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public string Kind { get; private set; } = default!; // "TaskDue" | "Mention" (Agent Dashboard AD-5)
    public string Message { get; private set; } = default!;
    public Guid? RelatedTicketId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private AgentNotification() { } // EF Core

    public AgentNotification(Guid agentId, string kind, string message, Guid? relatedTicketId, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Notification message is required.", nameof(message));

        Id = Guid.NewGuid();
        AgentId = agentId;
        Kind = kind;
        Message = message;
        RelatedTicketId = relatedTicketId;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkRead() => IsRead = true;
}
