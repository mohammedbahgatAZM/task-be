namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketCollaboratorRepository
{
    Task<IReadOnlyList<TicketCollaborator>> GetByTicketAsync(Guid ticketId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid ticketId, Guid agentId, CancellationToken ct);
    Task AddAsync(TicketCollaborator collaborator, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
