namespace SupportCrm.Application.KnowledgeBase;

public interface IArticleAttachmentStorage
{
    Task<string> SaveAsync(Guid articleId, Guid attachmentId, string fileName, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
}
