namespace SupportCrm.Domain.Entities;

// Always customer-visible by definition — this is the AC's "message" concept,
// distinct from TicketNote (always internal-only).
public class TicketMessage
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string Body { get; private set; } = default!;
    public string AuthorName { get; private set; } = default!;
    public string AuthorKind { get; private set; } = default!; // "Customer" | "Agent" | "System"
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public TicketChannel? Channel { get; private set; }

    private TicketMessage() { } // EF Core

    public TicketMessage(Guid ticketId, string body, string authorName, string authorKind, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Message body is required.", nameof(body));

        Id = Guid.NewGuid();
        TicketId = ticketId;
        Body = body;
        AuthorName = string.IsNullOrWhiteSpace(authorName) ? "unknown" : authorName;
        AuthorKind = authorKind is "Customer" or "Agent" or "System" ? authorKind : "Agent";
        CreatedAtUtc = createdAtUtc;
    }

    public void SetChannel(TicketChannel? channel) => Channel = channel;
}
