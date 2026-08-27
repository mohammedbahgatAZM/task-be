namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketAiSummaryRepository(SupportCrmDbContext dbContext) : ITicketAiSummaryRepository
{
    public Task<TicketAiSummary?> GetByTicketAsync(Guid ticketId, CancellationToken ct) =>
        dbContext.TicketAiSummaries.FirstOrDefaultAsync(s => s.TicketId == ticketId, ct);

    public Task AddAsync(TicketAiSummary summary, CancellationToken ct)
    {
        dbContext.TicketAiSummaries.Add(summary);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
