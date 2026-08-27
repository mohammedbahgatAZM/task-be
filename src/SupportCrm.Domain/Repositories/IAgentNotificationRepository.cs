namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IAgentNotificationRepository
{
    Task<AgentNotification?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AgentNotification>> GetByAgentAsync(Guid agentId, CancellationToken ct);
    Task AddAsync(AgentNotification notification, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
