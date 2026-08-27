namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class WhatsAppChannelService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ITicketAttachmentRepository attachmentRepository,
    IWhatsAppSender whatsAppSender,
    TimeProvider timeProvider)
{
    public async Task<TicketMessageDto> SendAsync(Guid ticketId, SendWhatsAppMessageRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        if (string.IsNullOrWhiteSpace(ticket.RequesterContactValue))
            throw new InvalidOperationException("This ticket has no requester phone number to message.");

        if (!request.IsTemplate)
        {
            var messages = await messageRepository.GetMessagesAsync(ticketId, ct);
            var lastInbound = messages.Where(m => m.AuthorKind == "Customer").Select(m => (DateTimeOffset?)m.CreatedAtUtc).Max();
            if (!WhatsAppMessagingWindow.IsOpen(lastInbound, timeProvider.GetUtcNow()))
                throw new InvalidOperationException(
                    "Outside the 24-hour messaging window — send a template message instead of a free-form reply.");
        }

        var attachments = request.AttachmentIds is { Count: > 0 }
            ? (await attachmentRepository.GetByTicketAsync(ticketId, ct))
                .Where(a => request.AttachmentIds.Contains(a.Id))
                .Select(a => new TicketAttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByName, a.UploadedAtUtc))
                .ToList()
            : new List<TicketAttachmentDto>();

        await whatsAppSender.SendAsync(ticket.RequesterContactValue, request.Body, attachments, request.IsTemplate, ct);

        var now = timeProvider.GetUtcNow();
        var message = new TicketMessage(ticketId, request.Body, request.ChangedBy, "Agent", now);
        message.SetChannel(TicketChannel.WhatsApp);
        await messageRepository.AddMessageAsync(message, ct);
        await messageRepository.AddDeliveryStatusAsync(new TicketMessageDeliveryStatus(message.Id, "Sent", null, now), ct);
        await messageRepository.SaveChangesAsync(ct);

        return new TicketMessageDto(message.Id, message.Body, message.AuthorName, message.AuthorKind, message.CreatedAtUtc);
    }

    public async Task RecordStatusAsync(RecordWhatsAppStatusRequest request, CancellationToken ct)
    {
        _ = await messageRepository.GetMessageByIdAsync(request.TicketMessageId, ct)
            ?? throw new KeyNotFoundException($"Ticket message '{request.TicketMessageId}' was not found.");
        await messageRepository.AddDeliveryStatusAsync(
            new TicketMessageDeliveryStatus(request.TicketMessageId, request.Status, request.Detail, timeProvider.GetUtcNow()), ct);
        await messageRepository.SaveChangesAsync(ct);
    }
}
