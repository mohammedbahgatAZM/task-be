namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketCategoryRepository(SupportCrmDbContext dbContext) : ITicketCategoryRepository
{
    public Task<TicketCategory?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.TicketCategories.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<TicketCategory>> GetActiveAsync(CancellationToken ct) =>
        await dbContext.TicketCategories.Where(c => c.IsActive).ToListAsync(ct);

    public async Task<IReadOnlyList<TicketCategory>> GetAllAsync(CancellationToken ct) =>
        await dbContext.TicketCategories.ToListAsync(ct);

    public Task AddAsync(TicketCategory category, CancellationToken ct)
    {
        dbContext.TicketCategories.Add(category);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
