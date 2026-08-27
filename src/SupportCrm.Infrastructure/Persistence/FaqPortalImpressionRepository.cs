namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class FaqPortalImpressionRepository(SupportCrmDbContext dbContext) : IFaqPortalImpressionRepository
{
    public Task AddAsync(FaqPortalImpression impression, CancellationToken ct)
    {
        dbContext.FaqPortalImpressions.Add(impression);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<FaqPortalImpression>> GetBySessionAsync(string draftSessionId, CancellationToken ct) =>
        await dbContext.FaqPortalImpressions.Where(i => i.DraftSessionId == draftSessionId).ToListAsync(ct);

    public async Task<IReadOnlyList<FaqPortalImpression>> GetAllAsync(CancellationToken ct) =>
        await dbContext.FaqPortalImpressions.ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
