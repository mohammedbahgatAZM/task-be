namespace SupportCrm.Domain.Entities;

// One row per ticket, written at creation time regardless of whether the suggestion was
// applied — the record itself is what powers the pending-review list and accuracy report.
public class TicketCategorizationSuggestion
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid? SuggestedCategoryId { get; private set; }
    public TicketPriority SuggestedPriority { get; private set; }
    public int ConfidencePercentage { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private TicketCategorizationSuggestion() { } // EF Core

    public TicketCategorizationSuggestion(Guid ticketId, Guid? suggestedCategoryId, TicketPriority suggestedPriority, int confidencePercentage, DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        SuggestedCategoryId = suggestedCategoryId;
        SuggestedPriority = suggestedPriority;
        ConfidencePercentage = confidencePercentage;
        CreatedAtUtc = createdAtUtc;
    }
}
