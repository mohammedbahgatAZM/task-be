namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;

public record CreateArticleRequest(Guid? KbCategoryId, string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string AuthorName);
public record UpdateArticleRequest(string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string ChangedBy);
public record ArticleDto(Guid Id, Guid? KbCategoryId, string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, KbContentStatus Status, string AuthorName, string LastUpdatedByName, DateTimeOffset LastUpdatedAtUtc, int ViewCount, int HelpfulCount, int NotHelpfulCount);
public record ArticleAttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string UploadedByName, DateTimeOffset UploadedAtUtc);

public class ArticleNotFoundException(string id) : Exception($"Article '{id}' was not found.");
