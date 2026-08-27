namespace SupportCrm.Domain.Entities;

// INT-1 — an external system registers a URL + the event types it wants pushed to it.
// WebhookDispatcher POSTs a signed JSON payload to Url whenever a matching event fires.
public class WebhookSubscription
{
    public Guid Id { get; private set; }
    public string Url { get; private set; } = default!;
    // HMAC-SHA256 signing secret, sent back to the admin only at creation time (same "shown
    // once" discipline as ApiKey) and used to compute the X-Webhook-Signature header on delivery.
    public string Secret { get; private set; } = default!;
    // Comma-separated event type names, e.g. "ticket.created,ticket.resolved".
    public string EventTypes { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private WebhookSubscription() { }

    public WebhookSubscription(string url, string secret, IReadOnlyList<string> eventTypes, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
            throw new ArgumentException("A valid absolute URL is required.", nameof(url));
        if (eventTypes.Count == 0)
            throw new ArgumentException("At least one event type is required.", nameof(eventTypes));
        Id = Guid.NewGuid();
        Url = url.Trim();
        Secret = secret;
        EventTypes = string.Join(',', eventTypes);
        CreatedAtUtc = now;
    }

    public IReadOnlyList<string> EventTypeList => EventTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
