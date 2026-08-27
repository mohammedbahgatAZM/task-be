namespace SupportCrm.Domain.Entities;

public class Article
{
    public Guid Id { get; private set; }
    public Guid? KbCategoryId { get; private set; }
    public string? TitleEn { get; private set; }
    public string? TitleAr { get; private set; }
    public string? BodyEn { get; private set; }
    public string? BodyAr { get; private set; }
    public KbContentStatus Status { get; private set; } = KbContentStatus.Draft;
    public string AuthorName { get; private set; } = default!;
    public string LastUpdatedByName { get; private set; } = default!;
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }
    public int ViewCount { get; private set; }
    public int HelpfulCount { get; private set; }
    public int NotHelpfulCount { get; private set; }
    public bool HasBeenPublished { get; private set; }
    public DateTimeOffset? ReviewDueAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Article() { } // EF Core

    public Article(Guid? kbCategoryId, string? titleEn, string? titleAr, string? bodyEn, string? bodyAr, string authorName, DateTimeOffset createdAtUtc)
    {
        var hasEnglish = !string.IsNullOrWhiteSpace(titleEn) && !string.IsNullOrWhiteSpace(bodyEn);
        var hasArabic = !string.IsNullOrWhiteSpace(titleAr) && !string.IsNullOrWhiteSpace(bodyAr);
        if (!hasEnglish && !hasArabic)
            throw new ArgumentException("A title+body pair is required in at least one language.", nameof(titleEn));
        if (string.IsNullOrWhiteSpace(authorName))
            throw new ArgumentException("Author name is required.", nameof(authorName));

        Id = Guid.NewGuid();
        KbCategoryId = kbCategoryId;
        TitleEn = titleEn;
        TitleAr = titleAr;
        BodyEn = bodyEn;
        BodyAr = bodyAr;
        AuthorName = authorName;
        LastUpdatedByName = authorName;
        LastUpdatedAtUtc = createdAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public void RecordUpdate(string? titleEn, string? titleAr, string? bodyEn, string? bodyAr, string changedBy, DateTimeOffset atUtc)
    {
        TitleEn = titleEn;
        TitleAr = titleAr;
        BodyEn = bodyEn;
        BodyAr = bodyAr;
        LastUpdatedByName = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        LastUpdatedAtUtc = atUtc;
    }

    public void IncrementViewCount() => ViewCount++;
    public void MarkHelpful() => HelpfulCount++;
    public void MarkNotHelpful() => NotHelpfulCount++;

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
