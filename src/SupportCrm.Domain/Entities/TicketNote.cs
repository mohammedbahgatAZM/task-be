namespace SupportCrm.Domain.Entities;

public class TicketNote
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string Text { get; private set; } = default!;
    public string AuthorName { get; private set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private TicketNote() { } // EF Core

    public TicketNote(Guid ticketId, string text, string authorName, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Note text is required.", nameof(text));

        Id = Guid.NewGuid();
        TicketId = ticketId;
        Text = text;
        AuthorName = string.IsNullOrWhiteSpace(authorName) ? "unknown" : authorName;
        CreatedAtUtc = createdAtUtc;
    }
}
