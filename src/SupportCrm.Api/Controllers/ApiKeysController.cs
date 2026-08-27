namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Security;
using SupportCrm.Application.Integrations;

// INT-1 — admin CRUD for API keys. JWT-secured (the agent UI's own session), distinct from the
// api/integrations/v1/* controllers this feature also adds, which are ApiKey-secured instead.
[ApiController]
[Route("api/admin/api-keys")]
[Authorize]
public class ApiKeysController(ApiKeyService apiKeyService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Integrations", "View")]
    public async Task<ActionResult<IReadOnlyList<ApiKeyDto>>> GetAll(CancellationToken ct) => Ok(await apiKeyService.GetAllAsync(ct));

    [HttpGet("scopes")]
    [RequirePermission("Integrations", "View")]
    public ActionResult<IReadOnlyList<string>> GetKnownScopes() => Ok(ApiKeyService.KnownScopes);

    [HttpPost]
    [RequirePermission("Integrations", "Create")]
    public async Task<ActionResult<ApiKeyCreatedDto>> Create([FromBody] CreateApiKeyRequest request, CancellationToken ct)
    {
        var createdBy = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value ?? "unknown";
        try
        {
            return Ok(await apiKeyService.CreateAsync(request, createdBy, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/revoke")]
    [RequirePermission("Integrations", "Delete")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        try
        {
            await apiKeyService.RevokeAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
