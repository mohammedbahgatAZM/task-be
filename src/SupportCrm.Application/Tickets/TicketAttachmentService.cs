namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketAttachmentService(
    ITicketRepository ticketRepository,
    ITicketAttachmentRepository attachmentRepository,
    ITicketAttachmentStorage storage,
    TimeProvider timeProvider)
{
    public async Task<TicketAttachmentDto> AddAsync(Guid ticketId, string fileName, string contentType, long sizeBytes, Stream content, string uploadedByName, CancellationToken ct)
    {
        _ = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());

        var attachmentId = Guid.NewGuid();
        var storageKey = await storage.SaveAsync(ticketId, attachmentId, fileName, content, ct);

        var attachment = new TicketAttachment(ticketId, fileName, contentType, sizeBytes, storageKey, uploadedByName, timeProvider.GetUtcNow());
        await attachmentRepository.AddAsync(attachment, ct);
        await attachmentRepository.SaveChangesAsync(ct);
        return ToDto(attachment);
    }

    public async Task<IReadOnlyList<TicketAttachmentDto>> GetForTicketAsync(Guid ticketId, CancellationToken ct) =>
        (await attachmentRepository.GetByTicketAsync(ticketId, ct)).Select(ToDto).ToList();

    public async Task<(Stream Content, TicketAttachment Attachment)> OpenAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await attachmentRepository.GetByIdAsync(attachmentId, ct) ?? throw new KeyNotFoundException($"Attachment '{attachmentId}' was not found.");
        var stream = await storage.OpenReadAsync(attachment.StorageKey, ct);
        return (stream, attachment);
    }

    private static TicketAttachmentDto ToDto(TicketAttachment a) => new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByName, a.UploadedAtUtc);
}
