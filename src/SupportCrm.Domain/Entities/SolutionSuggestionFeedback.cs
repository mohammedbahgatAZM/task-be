namespace SupportCrm.Domain.Entities;

// Logged only — does not yet feed back into KbSearchService's ranking. A stand-in for a
// future relevance-tuning pass, flagged explicitly rather than silently doing nothing useful.
public class SolutionSuggestionFeedback
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string ContentType { get; private set; } = default!; // "Article" | "Guide"
    public Guid ContentId { get; private set; }
    public string FlaggedByName { get; private set; } = default!;
    public DateTimeOffset FlaggedAtUtc { get; private set; }

    private SolutionSuggestionFeedback() { } // EF Core

    public SolutionSuggestionFeedback(Guid ticketId, string contentType, Guid contentId, string flaggedByName, DateTimeOffset flaggedAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        ContentType = contentType;
        ContentId = contentId;
        FlaggedByName = string.IsNullOrWhiteSpace(flaggedByName) ? "unknown" : flaggedByName;
        FlaggedAtUtc = flaggedAtUtc;
    }
}
