namespace SupportCrm.Application.Security;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class RoleManagementService(IRoleRepository roleRepository, IPermissionRepository permissionRepository)
{
    public async Task<IReadOnlyList<PermissionDto>> GetPermissionCatalogAsync(CancellationToken ct) =>
        (await permissionRepository.GetAllAsync(ct)).Select(p => new PermissionDto(p.Id, p.Module, p.Action)).OrderBy(p => p.Module).ThenBy(p => p.Action).ToList();

    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken ct)
    {
        var result = new List<RoleDto>();
        foreach (var role in await roleRepository.GetAllAsync(ct))
        {
            var permissionIds = await permissionRepository.GetPermissionIdsForRolesAsync(new[] { role.Id }, ct);
            result.Add(new RoleDto(role.Id, role.Name, role.IsSystemDefined, permissionIds));
        }
        return result;
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken ct)
    {
        var role = new Role(request.Name, isSystemDefined: false);
        await roleRepository.AddAsync(role, ct);
        await roleRepository.SaveChangesAsync(ct);
        return new RoleDto(role.Id, role.Name, role.IsSystemDefined, Array.Empty<Guid>());
    }

    public async Task DeleteAsync(Guid roleId, CancellationToken ct)
    {
        var role = await roleRepository.GetByIdAsync(roleId, ct) ?? throw new RoleNotFoundException(roleId);
        if (role.IsSystemDefined) throw new SystemRoleDeletionException(role.Name);
        await roleRepository.DeleteAsync(role, ct);
        await roleRepository.SaveChangesAsync(ct);
    }

    public async Task SetPermissionsAsync(Guid roleId, SetRolePermissionsRequest request, CancellationToken ct)
    {
        _ = await roleRepository.GetByIdAsync(roleId, ct) ?? throw new RoleNotFoundException(roleId);
        await permissionRepository.SetRolePermissionsAsync(roleId, request.PermissionIds, ct);
        await permissionRepository.SaveChangesAsync(ct);
    }
}
