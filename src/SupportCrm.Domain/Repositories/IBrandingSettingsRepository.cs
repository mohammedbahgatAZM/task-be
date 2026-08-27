namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IBrandingSettingsRepository
{
    Task<BrandingSettings?> GetByBranchIdAsync(Guid? branchId, CancellationToken ct);
    Task AddAsync(BrandingSettings settings, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
