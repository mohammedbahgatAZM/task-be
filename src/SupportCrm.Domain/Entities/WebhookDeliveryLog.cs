namespace SupportCrm.Domain.Entities;

// INT-1 — one row per delivery attempt, so admins can see what was sent, whether it succeeded,
// and manually redeliver a failed one. There is no automatic retry-with-backoff in this
// prototype (documented scope note) — redelivery is a deliberate admin action.
public class WebhookDeliveryLog
{
    public Guid Id { get; private set; }
    public Guid WebhookSubscriptionId { get; private set; }
    public string EventType { get; private set; } = default!;
    public string PayloadJson { get; private set; } = default!;
    public bool Success { get; private set; }
    public int? StatusCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTimeOffset AttemptedAtUtc { get; private set; }

    private WebhookDeliveryLog() { }

    public WebhookDeliveryLog(Guid webhookSubscriptionId, string eventType, string payloadJson, bool success, int? statusCode, string? errorMessage, DateTimeOffset now)
    {
        Id = Guid.NewGuid();
        WebhookSubscriptionId = webhookSubscriptionId;
        EventType = eventType;
        PayloadJson = payloadJson;
        Success = success;
        StatusCode = statusCode;
        ErrorMessage = errorMessage;
        AttemptedAtUtc = now;
    }
}
