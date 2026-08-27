namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IKbCategoryRepository
{
    Task<IReadOnlyList<KbCategory>> GetActiveAsync(CancellationToken ct);
    Task AddAsync(KbCategory category, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
