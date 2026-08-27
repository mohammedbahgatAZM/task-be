namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;
using SupportCrm.Domain.Entities;

[ApiController]
[Route("api/channels/sms")]
public class SmsChannelController(TicketIngestionService ingestionService, SmsChannelService smsChannelService) : ControllerBase
{
    // Stub webhook: stands in for a real SMS gateway's inbound-message callback. SMS senders
    // have no separate "name" field (unlike email/WhatsApp) — the phone number doubles as the
    // requester name here, a real and deliberate difference from the other channels' shape.
    [HttpPost("inbound")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Inbound([FromForm] string fromPhoneNumber, [FromForm] string body, CancellationToken ct)
    {
        var ticket = await ingestionService.IngestInboundMessageAsync(
            new IngestInboundMessageRequest(TicketChannel.Sms, fromPhoneNumber, fromPhoneNumber, "SMS message", body), ct);
        return Ok(new { ticketId = ticket.Id, referenceNumber = ticket.ReferenceNumber });
    }

    // Stub webhook: stands in for a real gateway's delivery-report callback.
    [HttpPost("delivery-failure")]
    public async Task<IActionResult> DeliveryFailure([FromBody] RecordSmsDeliveryFailureRequest request, CancellationToken ct)
    {
        try { await smsChannelService.RecordDeliveryFailureAsync(request, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
