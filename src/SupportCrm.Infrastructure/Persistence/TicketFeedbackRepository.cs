namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketFeedbackRepository(SupportCrmDbContext dbContext) : ITicketFeedbackRepository
{
    public Task<TicketFeedback?> GetByTicketAsync(Guid ticketId, CancellationToken ct) =>
        dbContext.TicketFeedback.FirstOrDefaultAsync(f => f.TicketId == ticketId, ct);

    public async Task<IReadOnlyList<TicketFeedback>> GetAllAsync(CancellationToken ct) =>
        await dbContext.TicketFeedback.ToListAsync(ct);

    public Task AddAsync(TicketFeedback feedback, CancellationToken ct)
    {
        dbContext.TicketFeedback.Add(feedback);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
