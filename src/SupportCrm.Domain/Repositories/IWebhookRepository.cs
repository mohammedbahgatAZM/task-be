namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IWebhookRepository
{
    Task<WebhookSubscription?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<WebhookSubscription>> GetActiveForEventAsync(string eventType, CancellationToken ct);
    Task AddAsync(WebhookSubscription subscription, CancellationToken ct);

    Task<WebhookDeliveryLog?> GetDeliveryByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<WebhookDeliveryLog>> GetDeliveriesAsync(Guid? subscriptionId, CancellationToken ct);
    Task AddDeliveryAsync(WebhookDeliveryLog log, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
