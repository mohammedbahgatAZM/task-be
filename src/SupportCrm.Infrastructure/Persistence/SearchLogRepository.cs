namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SearchLogRepository(SupportCrmDbContext dbContext) : ISearchLogRepository
{
    public Task AddAsync(SearchLog entry, CancellationToken ct)
    {
        dbContext.SearchLogs.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<SearchLog>> GetZeroResultLogsAsync(int take, CancellationToken ct) =>
        await dbContext.SearchLogs
            .Where(s => s.ResultCount == 0)
            .OrderByDescending(s => s.SearchedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
