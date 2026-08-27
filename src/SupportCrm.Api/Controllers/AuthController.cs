namespace SupportCrm.Api.Controllers;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Security;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResultDto>> Login([FromBody] LoginRequest request, CancellationToken ct) =>
        Ok(await authService.LoginAsync(request, ct));

    [HttpPost("login/mfa")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResultDto>> CompleteMfaLogin([FromBody] CompleteMfaLoginRequest request, CancellationToken ct) =>
        Ok(await authService.CompleteMfaLoginAsync(request, ct));

    [HttpPost("login/change-expired-password")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResultDto>> CompleteExpiredPasswordChange([FromBody] CompleteExpiredPasswordChangeRequest request, CancellationToken ct)
    {
        try { return await authService.CompleteExpiredPasswordChangeAsync(request, ct); }
        catch (SecurityValidationException ex) { return BadRequest(new { errors = ex.Errors }); }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        try { await authService.ChangePasswordAsync(CurrentUserId(), request, ct); return NoContent(); }
        catch (SecurityValidationException ex) { return BadRequest(new { errors = ex.Errors }); }
    }

    [HttpPost("mfa/enable")]
    [Authorize]
    public async Task<ActionResult<MfaSetupDto>> EnableMfa(CancellationToken ct) => await authService.BeginMfaSetupAsync(CurrentUserId(), ct);

    [HttpPost("mfa/confirm")]
    [Authorize]
    public async Task<IActionResult> ConfirmMfa([FromBody] ConfirmMfaRequest request, CancellationToken ct)
    {
        try { await authService.ConfirmMfaSetupAsync(CurrentUserId(), request, ct); return NoContent(); }
        catch (SecurityValidationException ex) { return BadRequest(new { errors = ex.Errors }); }
    }

    [HttpPost("mfa/disable")]
    [Authorize]
    public async Task<IActionResult> DisableMfa(CancellationToken ct)
    {
        await authService.DisableMfaAsync(CurrentUserId(), ct);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserDto>> Me(
        [FromServices] SupportCrm.Domain.Repositories.IUserRepository userRepository,
        [FromServices] SupportCrm.Domain.Repositories.IRoleRepository roleRepository,
        [FromServices] IPermissionChecker permissionChecker, CancellationToken ct)
    {
        var userId = CurrentUserId();
        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null) return NotFound();
        var roleIds = await userRepository.GetRoleIdsForUserAsync(userId, ct);
        var roles = await roleRepository.GetByIdsAsync(roleIds, ct);
        var permissions = await permissionChecker.GetPermissionsAsync(roleIds, ct);
        return new CurrentUserDto(user.Id, user.Email, roles.Select(r => r.Name).ToList(), permissions);
    }

    private Guid CurrentUserId() =>
        Guid.Parse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value ?? throw new InvalidOperationException("Missing sub claim."));
}
