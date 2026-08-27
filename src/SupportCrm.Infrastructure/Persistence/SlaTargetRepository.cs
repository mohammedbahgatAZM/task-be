namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SlaTargetRepository(SupportCrmDbContext dbContext) : ISlaTargetRepository
{
    public async Task<IReadOnlyList<SlaTarget>> GetActiveAsync(CancellationToken ct) =>
        await dbContext.SlaTargets.Where(t => t.IsActive).ToListAsync(ct);

    public Task AddAsync(SlaTarget target, CancellationToken ct)
    {
        dbContext.SlaTargets.Add(target);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
