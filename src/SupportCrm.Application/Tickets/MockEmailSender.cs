namespace SupportCrm.Application.Tickets;

using Microsoft.Extensions.Logging;

public class MockEmailSender(ILogger<MockEmailSender> logger) : IEmailSender
{
    public Task<string> SendReplyAsync(string toAddress, string subject, string body, IReadOnlyList<TicketAttachmentDto> attachments, CancellationToken ct)
    {
        var fakeMessageId = $"mock-email-{Guid.NewGuid():N}";
        logger.LogInformation("Mock email send: to={To} subject={Subject} attachments={AttachmentCount} providerMessageId={MessageId}",
            toAddress, subject, attachments.Count, fakeMessageId);
        return Task.FromResult(fakeMessageId);
    }
}
