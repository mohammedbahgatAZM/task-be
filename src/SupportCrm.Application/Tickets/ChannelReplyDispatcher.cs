namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

// IsTemplate is only consulted for a WhatsApp-routed reply (see ReplyAsync below) — it closes the
// gap flagged when the unified endpoint replaced CC-2's dedicated WhatsApp compose card, which had
// its own "send as template" toggle for replying outside the 24-hour messaging window.
public record DispatchReplyRequest(string Body, string ChangedBy, bool IsTemplate = false);

/// <summary>
/// Picks the right outbound channel for a reply based on the ticket's most recent
/// customer-authored message's channel, so an agent replying from the unified thread
/// doesn't have to pick a channel-specific endpoint by hand. Chat and WebForm have no
/// "reply back through the same channel" concept once their originating session/submission
/// is over (there is no live chat connection or web-form response channel to reply
/// through) — for those, and for tickets with no channel history at all, the reply is
/// recorded as a plain internal `TicketMessage` with no outbound send, which is the
/// correct behavior, not a missing feature.
/// </summary>
public class ChannelReplyDispatcher(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    EmailChannelService emailChannelService,
    WhatsAppChannelService whatsAppChannelService,
    SmsChannelService smsChannelService,
    TimeProvider timeProvider)
{
    public async Task<TicketMessageDto> ReplyAsync(Guid ticketId, DispatchReplyRequest request, CancellationToken ct)
    {
        _ = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());

        var messages = await messageRepository.GetMessagesAsync(ticketId, ct);
        var lastInboundChannel = messages
            .Where(m => m.AuthorKind == "Customer")
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => m.Channel)
            .FirstOrDefault();

        return lastInboundChannel switch
        {
            TicketChannel.Email => await emailChannelService.SendReplyAsync(ticketId, new SendEmailReplyRequest(request.Body, request.ChangedBy, null), ct),
            TicketChannel.WhatsApp => await whatsAppChannelService.SendAsync(ticketId, new SendWhatsAppMessageRequest(request.Body, request.ChangedBy, null, request.IsTemplate), ct),
            TicketChannel.Sms => await smsChannelService.SendAsync(ticketId, new SendSmsRequest(request.Body, request.ChangedBy), ct),
            _ => await RecordPlainReplyAsync(ticketId, request, ct) // Chat, WebForm, Manual, or no channel history yet
        };
    }

    private async Task<TicketMessageDto> RecordPlainReplyAsync(Guid ticketId, DispatchReplyRequest request, CancellationToken ct)
    {
        var message = new TicketMessage(ticketId, request.Body, request.ChangedBy, "Agent", timeProvider.GetUtcNow());
        await messageRepository.AddMessageAsync(message, ct);
        await messageRepository.SaveChangesAsync(ct);
        return new TicketMessageDto(message.Id, message.Body, message.AuthorName, message.AuthorKind, message.CreatedAtUtc);
    }
}
