namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Security;
using SupportCrm.Application.Security;

[ApiController]
[Route("api/admin/roles")]
[Authorize]
public class RolesController(RoleManagementService roleManagementService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Administration", "View")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetAll(CancellationToken ct) => Ok(await roleManagementService.GetAllAsync(ct));

    [HttpGet("permissions")]
    [RequirePermission("Administration", "View")]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> GetPermissionCatalog(CancellationToken ct) => Ok(await roleManagementService.GetPermissionCatalogAsync(ct));

    [HttpPost]
    [RequirePermission("Administration", "Create")]
    public async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleRequest request, CancellationToken ct) => await roleManagementService.CreateAsync(request, ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission("Administration", "Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await roleManagementService.DeleteAsync(id, ct); return NoContent(); }
        catch (RoleNotFoundException) { return NotFound(); }
        catch (SystemRoleDeletionException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/permissions")]
    [RequirePermission("Administration", "Edit")]
    public async Task<IActionResult> SetPermissions(Guid id, [FromBody] SetRolePermissionsRequest request, CancellationToken ct)
    {
        try { await roleManagementService.SetPermissionsAsync(id, request, ct); return NoContent(); }
        catch (RoleNotFoundException) { return NotFound(); }
    }
}
