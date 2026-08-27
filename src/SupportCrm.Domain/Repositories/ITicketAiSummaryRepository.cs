namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketAiSummaryRepository
{
    Task<TicketAiSummary?> GetByTicketAsync(Guid ticketId, CancellationToken ct);
    Task AddAsync(TicketAiSummary summary, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
