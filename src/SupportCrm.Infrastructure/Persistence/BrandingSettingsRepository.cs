namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class BrandingSettingsRepository(SupportCrmDbContext dbContext) : IBrandingSettingsRepository
{
    public Task<BrandingSettings?> GetByBranchIdAsync(Guid? branchId, CancellationToken ct) =>
        dbContext.BrandingSettings.FirstOrDefaultAsync(b => b.BranchId == branchId, ct);

    public Task AddAsync(BrandingSettings settings, CancellationToken ct)
    {
        dbContext.BrandingSettings.Add(settings);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
