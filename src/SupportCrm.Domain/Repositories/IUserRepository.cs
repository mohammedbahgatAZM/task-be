namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct);
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
    Task DeleteAsync(User user, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetRoleIdsForUserAsync(Guid userId, CancellationToken ct);
    Task SetUserRolesAsync(Guid userId, IReadOnlyList<Guid> roleIds, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
