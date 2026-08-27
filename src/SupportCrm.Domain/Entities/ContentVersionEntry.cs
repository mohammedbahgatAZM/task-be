namespace SupportCrm.Domain.Entities;

// Immutable snapshot row — one per edit to content that has ever been Published. ContentType
// is "Article" | "Guide"; ContentId is that content's own Id. Never-published Drafts are not
// snapshotted (see ContentWorkflowService) — nothing public changed yet.
public class ContentVersionEntry
{
    public Guid Id { get; private set; }
    public string ContentType { get; private set; } = default!;
    public Guid ContentId { get; private set; }
    public int VersionNumber { get; private set; }
    public string? TitleEnSnapshot { get; private set; }
    public string? TitleArSnapshot { get; private set; }
    public string? BodyEnSnapshot { get; private set; }
    public string? BodyArSnapshot { get; private set; }
    public string ChangedBy { get; private set; } = default!;
    public DateTimeOffset ChangedAtUtc { get; private set; }

    private ContentVersionEntry() { } // EF Core

    public ContentVersionEntry(string contentType, Guid contentId, int versionNumber, string? titleEnSnapshot, string? titleArSnapshot, string? bodyEnSnapshot, string? bodyArSnapshot, string changedBy, DateTimeOffset changedAtUtc)
    {
        Id = Guid.NewGuid();
        ContentType = contentType;
        ContentId = contentId;
        VersionNumber = versionNumber;
        TitleEnSnapshot = titleEnSnapshot;
        TitleArSnapshot = titleArSnapshot;
        BodyEnSnapshot = bodyEnSnapshot;
        BodyArSnapshot = bodyArSnapshot;
        ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        ChangedAtUtc = changedAtUtc;
    }
}
