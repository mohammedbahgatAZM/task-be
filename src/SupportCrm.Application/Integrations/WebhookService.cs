namespace SupportCrm.Application.Integrations;

using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;

// INT-1 — "Webhooks are available to notify external systems of key events." Delivery is
// synchronous, once per event, with a short per-request timeout (configured on the named
// "webhooks" HttpClient in DependencyInjection) — there is no background retry queue in this
// prototype (documented scope note). A failed delivery is logged and an in-app alert is raised;
// redelivery is a deliberate admin action via RedeliverAsync, not automatic.
public class WebhookService(
    IWebhookRepository repository,
    IAgentRepository agentRepository,
    AgentNotificationService notificationService,
    IHttpClientFactory httpClientFactory,
    TimeProvider timeProvider)
{
    public async Task<WebhookCreatedDto> CreateAsync(CreateWebhookRequest request, CancellationToken ct)
    {
        var unknown = request.EventTypes.Where(e => !WebhookEventTypes.All.Contains(e)).ToList();
        if (unknown.Count > 0)
            throw new ArgumentException($"Unknown event type(s): {string.Join(", ", unknown)}.", nameof(request));

        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var subscription = new WebhookSubscription(request.Url, secret, request.EventTypes, timeProvider.GetUtcNow());
        await repository.AddAsync(subscription, ct);
        await repository.SaveChangesAsync(ct);
        return new WebhookCreatedDto(subscription.Id, subscription.Url, secret, subscription.EventTypeList, subscription.CreatedAtUtc);
    }

    public async Task<IReadOnlyList<WebhookDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var subscription = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Webhook '{id}' was not found.");
        if (isActive) subscription.Activate(); else subscription.Deactivate();
        await repository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WebhookDeliveryDto>> GetDeliveriesAsync(Guid? subscriptionId, CancellationToken ct) =>
        (await repository.GetDeliveriesAsync(subscriptionId, ct)).Select(ToDeliveryDto).ToList();

    // Called by TicketService at the two lifecycle points INT-1 names explicitly: ticket
    // created and ticket resolved. A subscription with no matching event type is skipped
    // entirely — this is a no-op, not a failure, when nobody is subscribed.
    public async Task DispatchAsync(string eventType, object payload, CancellationToken ct)
    {
        var subscriptions = await repository.GetActiveForEventAsync(eventType, ct);
        if (subscriptions.Count == 0) return;

        var payloadJson = JsonSerializer.Serialize(payload);
        foreach (var subscription in subscriptions)
            await DeliverAsync(subscription, eventType, payloadJson, ct);
    }

    public async Task<WebhookDeliveryDto> RedeliverAsync(Guid deliveryLogId, CancellationToken ct)
    {
        var log = await repository.GetDeliveryByIdAsync(deliveryLogId, ct) ?? throw new KeyNotFoundException($"Delivery '{deliveryLogId}' was not found.");
        var subscription = await repository.GetByIdAsync(log.WebhookSubscriptionId, ct) ?? throw new KeyNotFoundException($"Webhook '{log.WebhookSubscriptionId}' was not found.");
        return await DeliverAsync(subscription, log.EventType, log.PayloadJson, ct);
    }

    private async Task<WebhookDeliveryDto> DeliverAsync(WebhookSubscription subscription, string eventType, string payloadJson, CancellationToken ct)
    {
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(subscription.Secret), Encoding.UTF8.GetBytes(payloadJson))).ToLowerInvariant();
        var now = timeProvider.GetUtcNow();
        bool success;
        int? statusCode = null;
        string? error = null;
        try
        {
            var client = httpClientFactory.CreateClient("webhooks");
            using var request = new HttpRequestMessage(HttpMethod.Post, subscription.Url)
            {
                Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
            };
            request.Headers.Add("X-Webhook-Event", eventType);
            request.Headers.Add("X-Webhook-Signature", signature);
            using var response = await client.SendAsync(request, ct);
            statusCode = (int)response.StatusCode;
            success = response.IsSuccessStatusCode;
            if (!success) error = $"Endpoint responded with HTTP {statusCode}.";
        }
        catch (Exception ex)
        {
            success = false;
            error = ex.Message;
        }

        var log = new WebhookDeliveryLog(subscription.Id, eventType, payloadJson, success, statusCode, error, now);
        await repository.AddDeliveryAsync(log, ct);
        await repository.SaveChangesAsync(ct);

        if (!success)
            await AlertSupervisorsAsync(subscription, eventType, error, ct);

        return ToDeliveryDto(log);
    }

    // "Provider outages or authentication failures trigger an alert to the admin" (INT-3),
    // reused here for webhook delivery failure too — the same AgentNotificationService every
    // other feature in this codebase already uses for in-app alerts, not a second mechanism.
    private async Task AlertSupervisorsAsync(WebhookSubscription subscription, string eventType, string? error, CancellationToken ct)
    {
        var supervisors = (await agentRepository.GetAllAsync(ct)).Where(a => a.IsSupervisor);
        foreach (var supervisor in supervisors)
            await notificationService.NotifyAsync(supervisor.Id, "WebhookDeliveryFailed",
                $"Webhook delivery to {subscription.Url} failed for event '{eventType}': {error}", null, ct);
    }

    private static WebhookDto ToDto(WebhookSubscription w) => new(w.Id, w.Url, w.EventTypeList, w.IsActive, w.CreatedAtUtc);
    private static WebhookDeliveryDto ToDeliveryDto(WebhookDeliveryLog d) => new(d.Id, d.WebhookSubscriptionId, d.EventType, d.Success, d.StatusCode, d.ErrorMessage, d.AttemptedAtUtc);
}
