namespace SupportCrm.Application.Tickets;

/// <summary>
/// WhatsApp Business API's real 24-hour customer-service window rule: a free-form
/// (non-template) message may only be sent within 24 hours of the customer's last
/// inbound message. No real provider enforces this yet, but the rule itself is real —
/// it is not a decorative check.
/// </summary>
public static class WhatsAppMessagingWindow
{
    public static bool IsOpen(DateTimeOffset? lastInboundAtUtc, DateTimeOffset nowUtc) =>
        lastInboundAtUtc is not null && nowUtc - lastInboundAtUtc.Value <= TimeSpan.FromHours(24);
}
