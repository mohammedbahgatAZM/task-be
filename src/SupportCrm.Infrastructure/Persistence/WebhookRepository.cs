namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class WebhookRepository(SupportCrmDbContext dbContext) : IWebhookRepository
{
    public Task<WebhookSubscription?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.WebhookSubscriptions.FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task<IReadOnlyList<WebhookSubscription>> GetAllAsync(CancellationToken ct) =>
        await dbContext.WebhookSubscriptions.ToListAsync(ct);

    // Filtered in memory, not via SQL LIKE against the CSV column — subscription counts in this
    // prototype are small, and this avoids a brittle string-contains query that could
    // false-positive match ("ticket.created" inside "ticket.createdSomething").
    public async Task<IReadOnlyList<WebhookSubscription>> GetActiveForEventAsync(string eventType, CancellationToken ct) =>
        (await dbContext.WebhookSubscriptions.Where(w => w.IsActive).ToListAsync(ct))
            .Where(w => w.EventTypeList.Contains(eventType))
            .ToList();

    public Task AddAsync(WebhookSubscription subscription, CancellationToken ct) { dbContext.WebhookSubscriptions.Add(subscription); return Task.CompletedTask; }

    public Task<WebhookDeliveryLog?> GetDeliveryByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.WebhookDeliveryLogs.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<WebhookDeliveryLog>> GetDeliveriesAsync(Guid? subscriptionId, CancellationToken ct)
    {
        var query = dbContext.WebhookDeliveryLogs.AsQueryable();
        if (subscriptionId is not null) query = query.Where(d => d.WebhookSubscriptionId == subscriptionId);
        return await query.OrderByDescending(d => d.AttemptedAtUtc).Take(200).ToListAsync(ct);
    }

    public Task AddDeliveryAsync(WebhookDeliveryLog log, CancellationToken ct) { dbContext.WebhookDeliveryLogs.Add(log); return Task.CompletedTask; }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
