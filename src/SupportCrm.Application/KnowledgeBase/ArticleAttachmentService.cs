namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ArticleAttachmentService(
    IArticleRepository articleRepository,
    IArticleAttachmentRepository attachmentRepository,
    IArticleAttachmentStorage storage,
    TimeProvider timeProvider)
{
    public async Task<ArticleAttachmentDto> AddAsync(Guid articleId, string fileName, string contentType, long sizeBytes, Stream content, string uploadedByName, CancellationToken ct)
    {
        _ = await articleRepository.GetByIdAsync(articleId, ct) ?? throw new ArticleNotFoundException(articleId.ToString());

        var attachmentId = Guid.NewGuid();
        var storageKey = await storage.SaveAsync(articleId, attachmentId, fileName, content, ct);

        var attachment = new ArticleAttachment(articleId, fileName, contentType, sizeBytes, storageKey, uploadedByName, timeProvider.GetUtcNow());
        await attachmentRepository.AddAsync(attachment, ct);
        await attachmentRepository.SaveChangesAsync(ct);
        return ToDto(attachment);
    }

    public async Task<IReadOnlyList<ArticleAttachmentDto>> GetForArticleAsync(Guid articleId, CancellationToken ct) =>
        (await attachmentRepository.GetByArticleAsync(articleId, ct)).Select(ToDto).ToList();

    public async Task<(Stream Content, ArticleAttachment Attachment)> OpenAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await attachmentRepository.GetByIdAsync(attachmentId, ct) ?? throw new KeyNotFoundException($"Attachment '{attachmentId}' was not found.");
        var stream = await storage.OpenReadAsync(attachment.StorageKey, ct);
        return (stream, attachment);
    }

    private static ArticleAttachmentDto ToDto(ArticleAttachment a) => new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByName, a.UploadedAtUtc);
}
