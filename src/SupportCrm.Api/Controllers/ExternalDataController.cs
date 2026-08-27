namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Integrations;

// INT-2/INT-4 — the external-data panel shown on the ticket detail / customer profile pages.
// Deliberately unauthenticated, same as TicketsController/CustomersController — every
// agent-facing controller in this codebase except Security & Administration and the new
// api/admin/* Integrations controllers sits on this app's "no real auth" stand-in identity
// convention, and this panel is read by exactly the same ticket/customer pages those live on.
[ApiController]
[Route("api/customers/{customerId:guid}/external-data")]
public class ExternalDataController(ExternalDataService externalDataService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ExternalDataSnippetDto>>> GetForCustomer(Guid customerId, CancellationToken ct)
    {
        try
        {
            return Ok(await externalDataService.GetForCustomerAsync(customerId, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
