namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketCategoryRepository
{
    Task<TicketCategory?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TicketCategory>> GetActiveAsync(CancellationToken ct);
    // Reports & Management — includes inactive categories so old tickets still resolve a name.
    Task<IReadOnlyList<TicketCategory>> GetAllAsync(CancellationToken ct);
    Task AddAsync(TicketCategory category, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
