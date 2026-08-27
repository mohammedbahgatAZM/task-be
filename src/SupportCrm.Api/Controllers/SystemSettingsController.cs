namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Security;
using SupportCrm.Application.Security;

[ApiController]
[Route("api/admin/system-settings")]
[Authorize]
public class SystemSettingsController(SystemSettingsService systemSettingsService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Administration", "View")]
    public async Task<ActionResult<IReadOnlyList<SystemSettingDto>>> GetAll(CancellationToken ct) => Ok(await systemSettingsService.GetAllAsync(ct));

    [HttpPost("validate")]
    [RequirePermission("Administration", "Edit")]
    public async Task<ActionResult<ValidationResultDto>> Validate([FromBody] ValidateSettingsRequest request, CancellationToken ct) =>
        Ok(await systemSettingsService.ValidateAsync(request, ct));

    [HttpPost("apply")]
    [RequirePermission("Administration", "Edit")]
    public async Task<ActionResult<ValidationResultDto>> Apply([FromBody] ApplySettingsRequest request, CancellationToken ct)
    {
        var changedBy = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value ?? "unknown";
        var result = await systemSettingsService.ApplyAsync(request, changedBy, ct);
        return result.IsValid ? Ok(result) : BadRequest(result);
    }
}
