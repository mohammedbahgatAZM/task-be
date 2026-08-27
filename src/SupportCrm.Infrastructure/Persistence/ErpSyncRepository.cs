namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ErpSyncRepository(SupportCrmDbContext dbContext) : IErpSyncRepository
{
    public Task<ErpSyncState?> GetStateAsync(Guid customerId, CancellationToken ct) =>
        dbContext.ErpSyncStates.FirstOrDefaultAsync(s => s.CustomerId == customerId, ct);

    public Task UpsertStateAsync(ErpSyncState state, CancellationToken ct)
    {
        if (dbContext.Entry(state).State == EntityState.Detached)
            dbContext.ErpSyncStates.Add(state);
        return Task.CompletedTask;
    }

    public Task AddLogAsync(ErpSyncLog log, CancellationToken ct) { dbContext.ErpSyncLogs.Add(log); return Task.CompletedTask; }

    public async Task<IReadOnlyList<ErpSyncLog>> GetLogsAsync(Guid? customerId, CancellationToken ct)
    {
        var query = dbContext.ErpSyncLogs.AsQueryable();
        if (customerId is not null) query = query.Where(l => l.CustomerId == customerId);
        return await query.OrderByDescending(l => l.OccurredAtUtc).Take(200).ToListAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
