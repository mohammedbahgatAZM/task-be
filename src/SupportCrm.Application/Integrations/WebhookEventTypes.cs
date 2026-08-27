namespace SupportCrm.Application.Integrations;

// INT-1 — "webhooks are available to notify external systems of key events (e.g. ticket
// created/resolved)." The fixed set of event types a subscription can register for.
public static class WebhookEventTypes
{
    public const string TicketCreated = "ticket.created";
    public const string TicketResolved = "ticket.resolved";

    public static readonly IReadOnlyList<string> All = [TicketCreated, TicketResolved];
}
