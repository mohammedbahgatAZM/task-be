namespace SupportCrm.Application.Security;

using SupportCrm.Domain.Repositories;

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(IReadOnlyList<Guid> roleIds, string module, string action, CancellationToken ct);
    Task<IReadOnlyList<string>> GetPermissionsAsync(IReadOnlyList<Guid> roleIds, CancellationToken ct);
}

// Deliberately re-queries the database on every call rather than trusting anything baked into the
// JWT — this is what makes "permission changes take effect without re-login" literally true.
public class PermissionChecker(IPermissionRepository permissionRepository) : IPermissionChecker
{
    public async Task<bool> HasPermissionAsync(IReadOnlyList<Guid> roleIds, string module, string action, CancellationToken ct)
    {
        if (roleIds.Count == 0) return false;
        var granted = await permissionRepository.GetPermissionIdsForRolesAsync(roleIds, ct);
        if (granted.Count == 0) return false;
        var all = await permissionRepository.GetAllAsync(ct);
        return all.Any(p => granted.Contains(p.Id) && p.Module == module && p.Action == action);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(IReadOnlyList<Guid> roleIds, CancellationToken ct)
    {
        var granted = await permissionRepository.GetPermissionIdsForRolesAsync(roleIds, ct);
        var all = await permissionRepository.GetAllAsync(ct);
        return all.Where(p => granted.Contains(p.Id)).Select(p => $"{p.Module}:{p.Action}").ToList();
    }
}
