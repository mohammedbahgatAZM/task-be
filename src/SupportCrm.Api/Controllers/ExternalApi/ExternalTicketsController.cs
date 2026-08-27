namespace SupportCrm.Api.Controllers.ExternalApi;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SupportCrm.Api.Security;
using SupportCrm.Application.Integrations;

[ApiController]
[Route("api/integrations/v1/tickets")]
[EnableRateLimiting(RateLimitPolicies.IntegrationsApi)]
public class ExternalTicketsController(ExternalApiService externalApiService) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = "tickets.read")]
    public async Task<ActionResult<IReadOnlyList<ExternalTicketDto>>> GetAll(CancellationToken ct) =>
        Ok(await externalApiService.GetTicketsAsync(ct));

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "tickets.read")]
    public async Task<ActionResult<ExternalTicketDto>> GetById(Guid id, CancellationToken ct)
    {
        var ticket = await externalApiService.GetTicketAsync(id, ct);
        return ticket is null ? NotFound(new { error = $"Ticket '{id}' was not found." }) : Ok(ticket);
    }

    // Reuses TicketService.CreateAsync internally — an externally-created ticket gets AI
    // categorization, department routing, assignment-rule evaluation, and the ticket.created
    // webhook dispatch exactly like one created from the agent UI (see ExternalApiService).
    [HttpPost]
    [Authorize(Policy = "tickets.write")]
    public async Task<ActionResult<ExternalTicketDto>> Create([FromBody] ExternalCreateTicketRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Subject) || string.IsNullOrWhiteSpace(request.RequesterName))
            return BadRequest(new { error = "Subject and requesterName are required." });
        try
        {
            return Ok(await externalApiService.CreateTicketAsync(request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
