namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SmsChannelService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ISmsSender smsSender,
    TimeProvider timeProvider)
{
    public async Task<TicketMessageDto> SendAsync(Guid ticketId, SendSmsRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ArgumentException("Body is required.", nameof(request));

        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        if (string.IsNullOrWhiteSpace(ticket.RequesterContactValue))
            throw new InvalidOperationException("This ticket has no requester phone number to text.");

        var segments = SmsSegmenter.Split(request.Body);
        foreach (var segment in segments)
            await smsSender.SendAsync(ticket.RequesterContactValue, segment, ct);

        var now = timeProvider.GetUtcNow();
        var message = new TicketMessage(ticketId, request.Body, request.ChangedBy, "Agent", now);
        message.SetChannel(TicketChannel.Sms);
        await messageRepository.AddMessageAsync(message, ct);
        await messageRepository.AddDeliveryStatusAsync(new TicketMessageDeliveryStatus(message.Id, "Sent", $"{segments.Count} segment(s)", now), ct);
        await messageRepository.SaveChangesAsync(ct);

        return new TicketMessageDto(message.Id, message.Body, message.AuthorName, message.AuthorKind, message.CreatedAtUtc);
    }

    public async Task RecordDeliveryFailureAsync(RecordSmsDeliveryFailureRequest request, CancellationToken ct)
    {
        _ = await messageRepository.GetMessageByIdAsync(request.TicketMessageId, ct)
            ?? throw new KeyNotFoundException($"Ticket message '{request.TicketMessageId}' was not found.");
        await messageRepository.AddDeliveryStatusAsync(
            new TicketMessageDeliveryStatus(request.TicketMessageId, "Failed", request.Reason, timeProvider.GetUtcNow()), ct);
        await messageRepository.SaveChangesAsync(ct);
    }
}
