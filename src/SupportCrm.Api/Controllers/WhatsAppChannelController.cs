namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;
using SupportCrm.Domain.Entities;

[ApiController]
[Route("api/channels/whatsapp")]
public class WhatsAppChannelController(
    TicketIngestionService ingestionService,
    WhatsAppChannelService whatsAppChannelService,
    TicketAttachmentService attachmentService) : ControllerBase
{
    // Stub webhook: stands in for a real WhatsApp Business API incoming-message callback.
    [HttpPost("inbound")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Inbound(
        [FromForm] string fromPhoneNumber, [FromForm] string fromName, [FromForm] string body,
        IFormFileCollection? attachments,
        CancellationToken ct)
    {
        var ticket = await ingestionService.IngestInboundMessageAsync(
            new IngestInboundMessageRequest(TicketChannel.WhatsApp, fromName, fromPhoneNumber, "WhatsApp message", body), ct);

        if (attachments is { Count: > 0 })
        {
            foreach (var file in attachments)
            {
                await using var stream = file.OpenReadStream();
                await attachmentService.AddAsync(ticket.Id, file.FileName, file.ContentType, file.Length, stream, fromName, ct);
            }
        }

        return Ok(new { ticketId = ticket.Id, referenceNumber = ticket.ReferenceNumber });
    }

    // Stub webhook: stands in for a real provider's delivery/read status callback.
    [HttpPost("status")]
    public async Task<IActionResult> Status([FromBody] RecordWhatsAppStatusRequest request, CancellationToken ct)
    {
        try { await whatsAppChannelService.RecordStatusAsync(request, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
