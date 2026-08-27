namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketCategorizationSuggestionRepository
{
    Task AddAsync(TicketCategorizationSuggestion suggestion, CancellationToken ct);
    Task<TicketCategorizationSuggestion?> GetByTicketAsync(Guid ticketId, CancellationToken ct);
    Task<IReadOnlyList<TicketCategorizationSuggestion>> GetAllAsync(CancellationToken ct);
    // Joins against Tickets in the Infrastructure implementation rather than loading every
    // ticket into application code — avoids an N+1/full-table-scan pattern for what could
    // otherwise be a large list.
    Task<IReadOnlyList<Guid>> GetPendingManualCategorizationTicketIdsAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
