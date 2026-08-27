namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketTaskRepository
{
    Task<TicketTask?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TicketTask>> GetByTicketAsync(Guid ticketId, CancellationToken ct);
    Task<IReadOnlyList<TicketTask>> GetByAgentAsync(Guid agentId, CancellationToken ct);
    Task AddAsync(TicketTask task, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
