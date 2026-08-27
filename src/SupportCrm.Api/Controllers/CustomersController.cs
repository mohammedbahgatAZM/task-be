namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Customers;
using SupportCrm.Application.CustomerPortal;
using SupportCrm.Domain.Entities;

[ApiController]
[Route("api/customers")]
public class CustomersController(CustomerService customerService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var dto = await customerService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetSummary), new { id = dto.Id }, dto);
    }

    [HttpGet("by-number/{customerNumber}")]
    public async Task<ActionResult<CustomerDto>> GetByCustomerNumber(string customerNumber, CancellationToken ct)
    {
        var customer = await customerService.GetByCustomerNumberAsync(customerNumber, ct);
        return customer is null ? NotFound() : customer;
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerSummaryDto>> GetSummary(Guid id, CancellationToken ct)
    {
        try
        {
            return await customerService.GetSummaryAsync(id, ct);
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<CustomerTimelinePageDto>> GetTimeline(
        Guid id,
        [FromServices] CustomerTimelineService timelineService,
        [FromQuery] string? channel,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? agent,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        try
        {
            var query = new CustomerTimelineQuery(channel, from, to, agent, page, pageSize);
            return await timelineService.GetTimelineAsync(id, query, ct);
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("duplicates")]
    public async Task<ActionResult<IReadOnlyList<DuplicateCandidateDto>>> FindDuplicates([FromQuery] string name, [FromQuery] string? company, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Query parameter 'name' is required.");
        return Ok(await customerService.FindDuplicatesAsync(name, company, ct));
    }

    [HttpPost("merge")]
    public async Task<IActionResult> Merge([FromBody] MergeCustomersRequest request, CancellationToken ct)
    {
        try
        {
            await customerService.MergeAsync(request, ct);
            return NoContent();
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("{id:guid}/agent-panel")]
    public async Task<ActionResult<CustomerAgentPanelDto>> GetAgentPanel(
        Guid id, [FromQuery] Guid requestingAgentId, [FromServices] CustomerAgentPanelService panelService, CancellationToken ct)
    {
        try { return await panelService.GetPanelAsync(id, requestingAgentId, ct); }
        catch (CustomerNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/account-flags")]
    public async Task<IActionResult> SetAccountFlags(Guid id, [FromBody] SetCustomerAccountFlagsRequest request, CancellationToken ct)
    {
        try { await customerService.SetAccountFlagsAsync(id, request, ct); return NoContent(); }
        catch (CustomerNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/language")]
    public async Task<IActionResult> SetLanguage(Guid id, [FromBody] SetCustomerLanguageRequest request, CancellationToken ct)
    {
        try { await customerService.SetPreferredLanguageAsync(id, request.Language, ct); return NoContent(); }
        catch (CustomerNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/branch")]
    public async Task<IActionResult> SetBranch(Guid id, [FromBody] SetCustomerBranchRequest request, CancellationToken ct)
    {
        try { await customerService.SetBranchAsync(id, request.BranchId, ct); return NoContent(); }
        catch (CustomerNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/company")]
    public async Task<IActionResult> SetCompany(Guid id, [FromBody] SetCustomerCompanyRequest request, CancellationToken ct)
    {
        try { await customerService.SetCompanyAsync(id, request.Company, ct); return NoContent(); }
        catch (CustomerNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/tickets")]
    public async Task<ActionResult<IReadOnlyList<CustomerTicketSummaryDto>>> GetTickets(
        Guid id, [FromServices] CustomerPortalTicketService portalTicketService,
        [FromQuery] TicketStatus? status, [FromQuery] Guid? categoryId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? query,
        CancellationToken ct) =>
        Ok(await portalTicketService.GetTicketsForCustomerAsync(id, new CustomerTicketListQuery(status, categoryId, from, to, query), ct));
}
