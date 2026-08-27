namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;

public record CreateGuideRequest(string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string? VideoUrl, string AuthorName, Guid EditorAgentId);
public record UpdateGuideRequest(string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string? VideoUrl, string ChangedBy, Guid EditorAgentId);
public record FlagGuideOutdatedRequest(string Reason);
public record GuideDto(Guid Id, string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string? VideoUrl, KbContentStatus Status, string AuthorName, string LastUpdatedByName, DateTimeOffset LastUpdatedAtUtc, bool IsFlaggedOutdated, string? FlaggedReason);
public record GuideAttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string UploadedByName, DateTimeOffset UploadedAtUtc);

public class GuideNotFoundException(string id) : Exception($"Guide '{id}' was not found.");
public class KbEditorRequiredException(Guid agentId) : Exception($"Agent '{agentId}' is not an authorized knowledge base editor.");
