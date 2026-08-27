namespace SupportCrm.Domain.Entities;

// One row per ticket (upserted on regenerate) — the AC asks for "the summary," current and
// singular, not a version history (unlike Knowledge Base's ContentVersionEntry).
public class TicketAiSummary
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string SummaryText { get; private set; } = default!;
    public int SourceMessageCount { get; private set; }
    public DateTimeOffset GeneratedAtUtc { get; private set; }

    private TicketAiSummary() { } // EF Core

    public TicketAiSummary(Guid ticketId, string summaryText, int sourceMessageCount, DateTimeOffset generatedAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        SummaryText = summaryText;
        SourceMessageCount = sourceMessageCount;
        GeneratedAtUtc = generatedAtUtc;
    }

    public void Regenerate(string summaryText, int sourceMessageCount, DateTimeOffset generatedAtUtc)
    {
        SummaryText = summaryText;
        SourceMessageCount = sourceMessageCount;
        GeneratedAtUtc = generatedAtUtc;
    }
}
