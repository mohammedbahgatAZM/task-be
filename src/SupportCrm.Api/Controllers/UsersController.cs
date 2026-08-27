namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Security;
using SupportCrm.Application.Security;

[ApiController]
[Route("api/admin/users")]
[Authorize]
public class UsersController(UserManagementService userManagementService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Administration", "View")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken ct) => Ok(await userManagementService.GetAllAsync(ct));

    [HttpPost]
    [RequirePermission("Administration", "Create")]
    public async Task<ActionResult<UserDto>> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        try { return await userManagementService.CreateAsync(request, ct); }
        catch (DuplicateEmailException ex) { return Conflict(new { error = ex.Message }); }
        catch (SecurityValidationException ex) { return BadRequest(new { errors = ex.Errors }); }
    }

    [HttpPut("{id:guid}/deactivate")]
    [RequirePermission("Administration", "Edit")]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        try { await userManagementService.DeactivateAsync(id, ct); return NoContent(); }
        catch (UserNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/activate")]
    [RequirePermission("Administration", "Edit")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        try { await userManagementService.ActivateAsync(id, ct); return NoContent(); }
        catch (UserNotFoundException) { return NotFound(); }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("Administration", "Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await userManagementService.DeleteAsync(id, ct); return NoContent(); }
        catch (UserNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/roles")]
    [RequirePermission("Administration", "Edit")]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] SetUserRolesRequest request, CancellationToken ct)
    {
        try { await userManagementService.SetRolesAsync(id, request, ct); return NoContent(); }
        catch (UserNotFoundException) { return NotFound(); }
    }
}
