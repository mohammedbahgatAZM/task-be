namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class EmailChannelService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ITicketAttachmentRepository attachmentRepository,
    IEmailSender emailSender,
    TimeProvider timeProvider)
{
    public async Task<TicketMessageDto> SendReplyAsync(Guid ticketId, SendEmailReplyRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        if (string.IsNullOrWhiteSpace(ticket.RequesterContactValue))
            throw new InvalidOperationException("This ticket has no requester contact value to email.");

        var attachments = request.AttachmentIds is { Count: > 0 }
            ? (await attachmentRepository.GetByTicketAsync(ticketId, ct))
                .Where(a => request.AttachmentIds.Contains(a.Id))
                .Select(a => new TicketAttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByName, a.UploadedAtUtc))
                .ToList()
            : new List<TicketAttachmentDto>();

        await emailSender.SendReplyAsync(ticket.RequesterContactValue, ticket.Subject, request.Body, attachments, ct);

        var now = timeProvider.GetUtcNow();
        var message = new TicketMessage(ticketId, request.Body, request.ChangedBy, "Agent", now);
        message.SetChannel(TicketChannel.Email);
        await messageRepository.AddMessageAsync(message, ct);
        await messageRepository.AddDeliveryStatusAsync(new TicketMessageDeliveryStatus(message.Id, "Sent", null, now), ct);
        await messageRepository.SaveChangesAsync(ct);

        return new TicketMessageDto(message.Id, message.Body, message.AuthorName, message.AuthorKind, message.CreatedAtUtc);
    }

    public async Task RecordBounceAsync(RecordEmailBounceRequest request, CancellationToken ct)
    {
        var message = await messageRepository.GetMessageByIdAsync(request.TicketMessageId, ct)
            ?? throw new KeyNotFoundException($"Ticket message '{request.TicketMessageId}' was not found.");
        await messageRepository.AddDeliveryStatusAsync(
            new TicketMessageDeliveryStatus(message.Id, "Bounced", request.Reason, timeProvider.GetUtcNow()), ct);
        await messageRepository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TicketMessageDeliveryStatusDto>> GetDeliveryStatusesAsync(Guid ticketId, CancellationToken ct) =>
        (await messageRepository.GetDeliveryStatusesAsync(ticketId, ct))
            .Select(s => new TicketMessageDeliveryStatusDto(s.Id, s.TicketMessageId, s.Status, s.Detail, s.OccurredAtUtc))
            .ToList();
}
