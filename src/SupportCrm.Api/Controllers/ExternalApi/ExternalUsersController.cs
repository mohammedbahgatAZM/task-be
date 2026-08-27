namespace SupportCrm.Api.Controllers.ExternalApi;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SupportCrm.Api.Security;
using SupportCrm.Application.Integrations;

[ApiController]
[Route("api/integrations/v1/users")]
[EnableRateLimiting(RateLimitPolicies.IntegrationsApi)]
public class ExternalUsersController(ExternalApiService externalApiService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "users.read")]
    public async Task<ActionResult<IReadOnlyList<ExternalUserDto>>> GetAll(CancellationToken ct) =>
        Ok(await externalApiService.GetUsersAsync(ct));
}
