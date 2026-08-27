namespace SupportCrm.Application.Tickets;

public interface ITicketAttachmentStorage
{
    Task<string> SaveAsync(Guid ticketId, Guid attachmentId, string fileName, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
}
