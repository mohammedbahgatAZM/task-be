namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class KbCategoryRepository(SupportCrmDbContext dbContext) : IKbCategoryRepository
{
    public async Task<IReadOnlyList<KbCategory>> GetActiveAsync(CancellationToken ct) =>
        await dbContext.KbCategories.Where(c => c.IsActive).ToListAsync(ct);

    public Task AddAsync(KbCategory category, CancellationToken ct)
    {
        dbContext.KbCategories.Add(category);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
