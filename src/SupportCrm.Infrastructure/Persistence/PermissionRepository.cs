namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class PermissionRepository(SupportCrmDbContext dbContext) : IPermissionRepository
{
    public async Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken ct) => await dbContext.Permissions.ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetPermissionIdsForRolesAsync(IReadOnlyList<Guid> roleIds, CancellationToken ct) =>
        await dbContext.RolePermissions.Where(rp => roleIds.Contains(rp.RoleId)).Select(rp => rp.PermissionId).Distinct().ToListAsync(ct);

    public Task SetRolePermissionsAsync(Guid roleId, IReadOnlyList<Guid> permissionIds, CancellationToken ct)
    {
        dbContext.RolePermissions.RemoveRange(dbContext.RolePermissions.Where(rp => rp.RoleId == roleId));
        foreach (var permissionId in permissionIds) dbContext.RolePermissions.Add(new RolePermission(roleId, permissionId));
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
