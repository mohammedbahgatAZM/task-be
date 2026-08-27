namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IBranchRepository
{
    Task<Branch?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Branch>> GetAllAsync(CancellationToken ct);
    Task AddAsync(Branch branch, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
