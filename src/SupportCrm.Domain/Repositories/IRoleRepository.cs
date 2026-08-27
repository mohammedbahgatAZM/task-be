namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IRoleRepository
{
    Task<Role?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct);
    Task AddAsync(Role role, CancellationToken ct);
    Task DeleteAsync(Role role, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
