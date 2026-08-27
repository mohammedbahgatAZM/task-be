namespace SupportCrm.Application.Tickets;

using Microsoft.Extensions.Logging;

public class MockWhatsAppSender(ILogger<MockWhatsAppSender> logger) : IWhatsAppSender
{
    public Task<string> SendAsync(string toPhoneNumber, string body, IReadOnlyList<TicketAttachmentDto> attachments, bool isTemplate, CancellationToken ct)
    {
        var fakeMessageId = $"mock-whatsapp-{Guid.NewGuid():N}";
        logger.LogInformation("Mock WhatsApp send: to={To} bodyLength={BodyLength} attachments={AttachmentCount} isTemplate={IsTemplate} providerMessageId={MessageId}",
            toPhoneNumber, body.Length, attachments.Count, isTemplate, fakeMessageId);
        return Task.FromResult(fakeMessageId);
    }
}
