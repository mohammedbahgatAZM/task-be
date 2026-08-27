namespace SupportCrm.Application.Tickets;

/// <summary>
/// Sends an outbound email reply. No real mailbox/SMTP provider exists in this codebase —
/// register <see cref="MockEmailSender"/> until one does. Returns a fake provider message id.
/// </summary>
public interface IEmailSender
{
    Task<string> SendReplyAsync(string toAddress, string subject, string body, IReadOnlyList<TicketAttachmentDto> attachments, CancellationToken ct);
}
