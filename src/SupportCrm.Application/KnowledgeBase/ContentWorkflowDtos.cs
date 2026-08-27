namespace SupportCrm.Application.KnowledgeBase;

public record PublishContentRequest(Guid EditorAgentId, DateTimeOffset? ReviewDueAtUtc);
public record TransitionContentRequest(Guid EditorAgentId);
public record ContentVersionDto(int VersionNumber, string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string ChangedBy, DateTimeOffset ChangedAtUtc);
public record DueForReviewItemDto(string ContentType, Guid ContentId, string? TitleEn, string? TitleAr, DateTimeOffset ReviewDueAtUtc);
