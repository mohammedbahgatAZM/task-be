namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class UserRepository(SupportCrmDbContext dbContext) : IUserRepository
{
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) => dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct) => dbContext.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct) => await dbContext.Users.ToListAsync(ct);
    public Task AddAsync(User user, CancellationToken ct) { dbContext.Users.Add(user); return Task.CompletedTask; }

    public Task DeleteAsync(User user, CancellationToken ct)
    {
        dbContext.UserRoles.RemoveRange(dbContext.UserRoles.Where(ur => ur.UserId == user.Id));
        dbContext.Users.Remove(user);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<Guid>> GetRoleIdsForUserAsync(Guid userId, CancellationToken ct) =>
        await dbContext.UserRoles.Where(ur => ur.UserId == userId).Select(ur => ur.RoleId).ToListAsync(ct);

    public Task SetUserRolesAsync(Guid userId, IReadOnlyList<Guid> roleIds, CancellationToken ct)
    {
        dbContext.UserRoles.RemoveRange(dbContext.UserRoles.Where(ur => ur.UserId == userId));
        foreach (var roleId in roleIds) dbContext.UserRoles.Add(new UserRole(userId, roleId));
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
