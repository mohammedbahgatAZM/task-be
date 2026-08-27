namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Customers;

[ApiController]
[Route("api/customers/{customerId:guid}/contact-details")]
public class ContactDetailsController(ContactDetailService contactDetailService, CustomerProfileService customerProfileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContactDetailDto>>> GetAll(Guid customerId, CancellationToken ct) =>
        Ok(await contactDetailService.GetForCustomerAsync(customerId, ct));

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<ContactDetailChangeLogDto>>> GetHistory(Guid customerId, CancellationToken ct) =>
        Ok(await contactDetailService.GetChangeLogAsync(customerId, ct));

    [HttpPost]
    public async Task<ActionResult<ContactDetailDto>> Add(Guid customerId, [FromBody] AddContactDetailRequest request, CancellationToken ct)
    {
        try
        {
            return await contactDetailService.AddAsync(customerId, request, ct);
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{contactDetailId:guid}")]
    public async Task<ActionResult<ContactDetailDto>> UpdateValue(Guid contactDetailId, [FromBody] UpdateContactDetailRequest request, CancellationToken ct)
    {
        try
        {
            return await contactDetailService.UpdateValueAsync(contactDetailId, request, ct);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{contactDetailId:guid}/set-primary")]
    public async Task<IActionResult> SetPrimary(Guid contactDetailId, [FromBody] SetPrimaryContactDetailRequest request, CancellationToken ct)
    {
        try
        {
            await contactDetailService.SetPrimaryAsync(contactDetailId, request, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("/api/customers/{customerId:guid}/preferred-channel")]
    public async Task<IActionResult> SetPreferredChannel(Guid customerId, [FromBody] SetPreferredChannelRequest request, CancellationToken ct)
    {
        try
        {
            await customerProfileService.SetPreferredChannelAsync(customerId, request, ct);
            return NoContent();
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("/api/customers/{customerId:guid}/address")]
    public async Task<IActionResult> SetAddress(Guid customerId, [FromBody] SetAddressRequest request, CancellationToken ct)
    {
        try
        {
            await customerProfileService.SetAddressAsync(customerId, request, ct);
            return NoContent();
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
    }
}
