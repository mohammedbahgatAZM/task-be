namespace SupportCrm.Domain.Entities;

public class ChatMessage
{
    public Guid Id { get; private set; }
    public Guid ChatSessionId { get; private set; }
    public string Body { get; private set; } = default!;
    public bool IsFromCustomer { get; private set; }
    public DateTimeOffset SentAtUtc { get; private set; }

    private ChatMessage() { } // EF Core

    public ChatMessage(Guid chatSessionId, string body, bool isFromCustomer, DateTimeOffset sentAtUtc)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Message body is required.", nameof(body));

        Id = Guid.NewGuid();
        ChatSessionId = chatSessionId;
        Body = body;
        IsFromCustomer = isFromCustomer;
        SentAtUtc = sentAtUtc;
    }
}
