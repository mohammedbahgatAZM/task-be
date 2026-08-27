namespace SupportCrm.Domain.Entities;

public class Guide
{
    public Guid Id { get; private set; }
    public string? TitleEn { get; private set; }
    public string? TitleAr { get; private set; }
    public string? BodyEn { get; private set; }
    public string? BodyAr { get; private set; }
    public string? VideoUrl { get; private set; }
    public KbContentStatus Status { get; private set; } = KbContentStatus.Draft;
    public string AuthorName { get; private set; } = default!;
    public string LastUpdatedByName { get; private set; } = default!;
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }
    public bool IsFlaggedOutdated { get; private set; }
    public string? FlaggedReason { get; private set; }
    public DateTimeOffset? FlaggedAtUtc { get; private set; }
    public bool HasBeenPublished { get; private set; }
    public DateTimeOffset? ReviewDueAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Guide() { } // EF Core

    public Guide(string? titleEn, string? titleAr, string? bodyEn, string? bodyAr, string? videoUrl, string authorName, DateTimeOffset createdAtUtc)
    {
        var hasEnglish = !string.IsNullOrWhiteSpace(titleEn) && !string.IsNullOrWhiteSpace(bodyEn);
        var hasArabic = !string.IsNullOrWhiteSpace(titleAr) && !string.IsNullOrWhiteSpace(bodyAr);
        if (!hasEnglish && !hasArabic)
            throw new ArgumentException("A title+body pair is required in at least one language.", nameof(titleEn));
        if (string.IsNullOrWhiteSpace(authorName))
            throw new ArgumentException("Author name is required.", nameof(authorName));

        Id = Guid.NewGuid();
        TitleEn = titleEn;
        TitleAr = titleAr;
        BodyEn = bodyEn;
        BodyAr = bodyAr;
        VideoUrl = videoUrl;
        AuthorName = authorName;
        LastUpdatedByName = authorName;
        LastUpdatedAtUtc = createdAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public void RecordUpdate(string? titleEn, string? titleAr, string? bodyEn, string? bodyAr, string? videoUrl, string changedBy, DateTimeOffset atUtc)
    {
        TitleEn = titleEn;
        TitleAr = titleAr;
        BodyEn = bodyEn;
        BodyAr = bodyAr;
        VideoUrl = videoUrl;
        LastUpdatedByName = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        LastUpdatedAtUtc = atUtc;
    }

    public void FlagOutdated(string reason, DateTimeOffset atUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to flag a guide as outdated.", nameof(reason));
        IsFlaggedOutdated = true;
        FlaggedReason = reason;
        FlaggedAtUtc = atUtc;
    }

    public void ClearOutdatedFlag()
    {
        IsFlaggedOutdated = false;
        FlaggedReason = null;
        FlaggedAtUtc = null;
    }

    public void SubmitForReview()
    {
        if (Status is not (KbContentStatus.Draft or KbContentStatus.UnderReview))
            throw new InvalidOperationException($"Cannot submit for review from status '{Status}'.");
        Status = KbContentStatus.UnderReview;
    }

    public void Publish(DateTimeOffset? reviewDueAtUtc)
    {
        Status = KbContentStatus.Published;
        HasBeenPublished = true;
        ReviewDueAtUtc = reviewDueAtUtc;
    }

    public void Unpublish() => Status = KbContentStatus.Draft;

    public void Archive() => Status = KbContentStatus.Archived;
}
