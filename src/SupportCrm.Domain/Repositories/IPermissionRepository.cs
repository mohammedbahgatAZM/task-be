namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IPermissionRepository
{
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetPermissionIdsForRolesAsync(IReadOnlyList<Guid> roleIds, CancellationToken ct);
    Task SetRolePermissionsAsync(Guid roleId, IReadOnlyList<Guid> permissionIds, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
