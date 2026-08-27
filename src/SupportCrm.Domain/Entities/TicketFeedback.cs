namespace SupportCrm.Domain.Entities;

// One row per ticket, write-once (enforced at the service layer, not here) — a customer can't
// silently erase a low rating by resubmitting.
public class TicketFeedback
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset SubmittedAtUtc { get; private set; }

    private TicketFeedback() { } // EF Core

    public TicketFeedback(Guid ticketId, int rating, string? comment, DateTimeOffset submittedAtUtc)
    {
        if (rating is < 1 or > 5)
            throw new ArgumentException("Rating must be between 1 and 5.", nameof(rating));

        Id = Guid.NewGuid();
        TicketId = ticketId;
        Rating = rating;
        Comment = comment;
        SubmittedAtUtc = submittedAtUtc;
    }
}
