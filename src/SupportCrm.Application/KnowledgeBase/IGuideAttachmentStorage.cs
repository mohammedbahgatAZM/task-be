namespace SupportCrm.Application.KnowledgeBase;

public interface IGuideAttachmentStorage
{
    Task<string> SaveAsync(Guid guideId, Guid attachmentId, string fileName, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
}
