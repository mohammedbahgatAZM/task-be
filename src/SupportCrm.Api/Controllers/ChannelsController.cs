namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;
using SupportCrm.Domain.Entities;

[ApiController]
[Route("api/channels/email")]
public class ChannelsController(
    TicketIngestionService ingestionService,
    EmailChannelService emailChannelService,
    TicketAttachmentService attachmentService) : ControllerBase
{
    // Stub webhook: stands in for a real provider's inbound-parse callback (e.g. SendGrid
    // Inbound Parse, Mailgun Routes). Accepts multipart/form-data the way those providers do.
    [HttpPost("inbound")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Inbound(
        [FromForm] string fromAddress, [FromForm] string fromName, [FromForm] string subject, [FromForm] string body,
        IFormFileCollection? attachments,
        CancellationToken ct)
    {
        var ticket = await ingestionService.IngestInboundMessageAsync(
            new IngestInboundMessageRequest(TicketChannel.Email, fromName, fromAddress, subject, body), ct);

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

    // Stub webhook: stands in for a real provider's bounce/undeliverable callback.
    [HttpPost("bounce")]
    public async Task<IActionResult> Bounce([FromBody] RecordEmailBounceRequest request, CancellationToken ct)
    {
        try { await emailChannelService.RecordBounceAsync(request, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
