namespace SupportCrm.Api.Controllers.ExternalApi;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SupportCrm.Api.Security;
using SupportCrm.Application.Integrations;

// INT-1 — the external-facing REST API surface. ApiKey-secured (not the JWT scheme the agent UI
// uses) and rate-limited; see docs/API.md for the full contract.
[ApiController]
[Route("api/integrations/v1/customers")]
[EnableRateLimiting(RateLimitPolicies.IntegrationsApi)]
public class ExternalCustomersController(ExternalApiService externalApiService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "customers.read")]
    public async Task<ActionResult<IReadOnlyList<ExternalCustomerDto>>> GetAll(CancellationToken ct) =>
        Ok(await externalApiService.GetCustomersAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "customers.read")]
    public async Task<ActionResult<ExternalCustomerDto>> GetById(Guid id, CancellationToken ct)
    {
        var customer = await externalApiService.GetCustomerAsync(id, ct);
        return customer is null ? NotFound(new { error = $"Customer '{id}' was not found." }) : Ok(customer);
    }

    [HttpPost]
    [Authorize(Policy = "customers.write")]
    public async Task<ActionResult<ExternalCustomerDto>> Create([FromBody] ExternalCreateCustomerRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });
        return Ok(await externalApiService.CreateCustomerAsync(request, ct));
    }
}
