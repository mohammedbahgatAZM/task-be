namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketCollaboratorRepository(SupportCrmDbContext dbContext) : ITicketCollaboratorRepository
{
    public async Task<IReadOnlyList<TicketCollaborator>> GetByTicketAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketCollaborators.Where(c => c.TicketId == ticketId).ToListAsync(ct);

    public Task<bool> ExistsAsync(Guid ticketId, Guid agentId, CancellationToken ct) =>
        dbContext.TicketCollaborators.AnyAsync(c => c.TicketId == ticketId && c.AgentId == agentId, ct);

    public Task AddAsync(TicketCollaborator collaborator, CancellationToken ct)
    {
        dbContext.TicketCollaborators.Add(collaborator);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
