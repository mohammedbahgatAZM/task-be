namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketTaskRepository(SupportCrmDbContext dbContext) : ITicketTaskRepository
{
    public Task<TicketTask?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.TicketTasks.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<TicketTask>> GetByTicketAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketTasks.Where(t => t.TicketId == ticketId).ToListAsync(ct);

    public async Task<IReadOnlyList<TicketTask>> GetByAgentAsync(Guid agentId, CancellationToken ct) =>
        await dbContext.TicketTasks.Where(t => t.AssignedAgentId == agentId).ToListAsync(ct);

    public Task AddAsync(TicketTask task, CancellationToken ct)
    {
        dbContext.TicketTasks.Add(task);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
