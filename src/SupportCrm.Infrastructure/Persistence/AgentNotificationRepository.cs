namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AgentNotificationRepository(SupportCrmDbContext dbContext) : IAgentNotificationRepository
{
    public Task<AgentNotification?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.AgentNotifications.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<AgentNotification>> GetByAgentAsync(Guid agentId, CancellationToken ct) =>
        await dbContext.AgentNotifications.Where(n => n.AgentId == agentId).ToListAsync(ct);

    public Task AddAsync(AgentNotification notification, CancellationToken ct)
    {
        dbContext.AgentNotifications.Add(notification);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
