namespace SupportCrm.Domain.Entities;

// One row per FAQ shown as a suggestion during one ticket-draft attempt. DraftSessionId is a
// client-generated, unauthenticated correlation id — not a real session/auth concept. Flipping
// LedToTicketSubmission is the only mutation; nothing else about an impression ever changes.
public class FaqPortalImpression
{
    public Guid Id { get; private set; }
    public Guid FaqId { get; private set; }
    public string DraftSessionId { get; private set; } = default!;
    public bool LedToTicketSubmission { get; private set; }
    public DateTimeOffset ShownAtUtc { get; private set; }

    private FaqPortalImpression() { } // EF Core

    public FaqPortalImpression(Guid faqId, string draftSessionId, DateTimeOffset shownAtUtc)
    {
        if (string.IsNullOrWhiteSpace(draftSessionId))
            throw new ArgumentException("Draft session id is required.", nameof(draftSessionId));

        Id = Guid.NewGuid();
        FaqId = faqId;
        DraftSessionId = draftSessionId;
        ShownAtUtc = shownAtUtc;
    }

    public void MarkLedToTicketSubmission() => LedToTicketSubmission = true;
}
