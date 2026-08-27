namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class RoleRepository(SupportCrmDbContext dbContext) : IRoleRepository
{
    public Task<Role?> GetByIdAsync(Guid id, CancellationToken ct) => dbContext.Roles.FirstOrDefaultAsync(r => r.Id == id, ct);
    public async Task<IReadOnlyList<Role>> GetAllAsync(CancellationToken ct) => await dbContext.Roles.ToListAsync(ct);
    public async Task<IReadOnlyList<Role>> GetByIdsAsync(IReadOnlyList<Guid> ids, CancellationToken ct) =>
        await dbContext.Roles.Where(r => ids.Contains(r.Id)).ToListAsync(ct);
    public Task AddAsync(Role role, CancellationToken ct) { dbContext.Roles.Add(role); return Task.CompletedTask; }
    public Task DeleteAsync(Role role, CancellationToken ct) { dbContext.Roles.Remove(role); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
