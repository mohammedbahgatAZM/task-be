namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketFeedbackRepository
{
    Task<TicketFeedback?> GetByTicketAsync(Guid ticketId, CancellationToken ct);
    // Reports & Management — full listing for CSAT/agent-performance aggregation.
    Task<IReadOnlyList<TicketFeedback>> GetAllAsync(CancellationToken ct);
    Task AddAsync(TicketFeedback feedback, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
