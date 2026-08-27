namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ContentVersionRepository(SupportCrmDbContext dbContext) : IContentVersionRepository
{
    public async Task<IReadOnlyList<ContentVersionEntry>> GetForContentAsync(string contentType, Guid contentId, CancellationToken ct) =>
        await dbContext.ContentVersionEntries.Where(v => v.ContentType == contentType && v.ContentId == contentId).ToListAsync(ct);

    public async Task<int> GetNextVersionNumberAsync(string contentType, Guid contentId, CancellationToken ct)
    {
        var max = await dbContext.ContentVersionEntries
            .Where(v => v.ContentType == contentType && v.ContentId == contentId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(ct);
        return (max ?? 0) + 1;
    }

    public Task AddAsync(ContentVersionEntry entry, CancellationToken ct)
    {
        dbContext.ContentVersionEntries.Add(entry);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
