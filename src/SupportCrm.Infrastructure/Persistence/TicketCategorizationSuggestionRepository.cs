namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketCategorizationSuggestionRepository(SupportCrmDbContext dbContext) : ITicketCategorizationSuggestionRepository
{
    public Task AddAsync(TicketCategorizationSuggestion suggestion, CancellationToken ct)
    {
        dbContext.TicketCategorizationSuggestions.Add(suggestion);
        return Task.CompletedTask;
    }

    public Task<TicketCategorizationSuggestion?> GetByTicketAsync(Guid ticketId, CancellationToken ct) =>
        dbContext.TicketCategorizationSuggestions.FirstOrDefaultAsync(s => s.TicketId == ticketId, ct);

    public async Task<IReadOnlyList<TicketCategorizationSuggestion>> GetAllAsync(CancellationToken ct) =>
        await dbContext.TicketCategorizationSuggestions.ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetPendingManualCategorizationTicketIdsAsync(CancellationToken ct) =>
        await dbContext.TicketCategorizationSuggestions
            .Where(s => dbContext.Tickets.Any(t => t.Id == s.TicketId && t.CategoryId == null))
            .Select(s => s.TicketId)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
