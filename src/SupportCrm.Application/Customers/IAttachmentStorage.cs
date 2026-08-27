namespace SupportCrm.Application.Customers;

/// <summary>
/// Persists attachment bytes. The default registration (<c>LocalDiskAttachmentStorage</c>,
/// in SupportCrm.Infrastructure) writes to local disk — swap the DI registration for a blob-storage
/// implementation later without touching <see cref="NoteAndAttachmentService"/> or its controller.
/// </summary>
public interface IAttachmentStorage
{
    Task<string> SaveAsync(Guid customerId, Guid attachmentId, string fileName, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
}
