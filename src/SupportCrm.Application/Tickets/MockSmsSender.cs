namespace SupportCrm.Application.Tickets;

using Microsoft.Extensions.Logging;

public class MockSmsSender(ILogger<MockSmsSender> logger) : ISmsSender
{
    public Task<string> SendAsync(string toPhoneNumber, string body, CancellationToken ct)
    {
        var fakeMessageId = $"mock-sms-{Guid.NewGuid():N}";
        var segmentCount = SmsSegmenter.Split(body).Count;
        logger.LogInformation("Mock SMS send: to={To} segments={SegmentCount} providerMessageId={MessageId}", toPhoneNumber, segmentCount, fakeMessageId);
        return Task.FromResult(fakeMessageId);
    }
}
