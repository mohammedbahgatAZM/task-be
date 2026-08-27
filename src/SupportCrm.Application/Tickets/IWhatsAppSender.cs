namespace SupportCrm.Application.Tickets;

public interface IWhatsAppSender
{
    Task<string> SendAsync(string toPhoneNumber, string body, IReadOnlyList<TicketAttachmentDto> attachments, bool isTemplate, CancellationToken ct);
}
