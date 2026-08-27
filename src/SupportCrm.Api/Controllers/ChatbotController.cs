namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Ai;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/chatbot")]
public class ChatbotController(AiChatbotService chatbotService) : ControllerBase
{
    [HttpPost("sessions")]
    public async Task<ActionResult<ChatSessionDto>> Start([FromBody] StartChatbotRequest request, CancellationToken ct) =>
        await chatbotService.StartAsync(request, ct);

    [HttpPost("sessions/{id:guid}/messages")]
    public async Task<ActionResult<ChatbotReplyDto>> SendMessage(Guid id, [FromBody] SendChatbotMessageRequest request, CancellationToken ct)
    {
        try { return await chatbotService.SendMessageAsync(id, request, ct); }
        catch (ChatSessionNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("sessions/{id:guid}/request-human")]
    public async Task<ActionResult<ChatSessionDto>> RequestHuman(Guid id, CancellationToken ct)
    {
        try { return await chatbotService.RequestHumanAsync(id, ct); }
        catch (ChatSessionNotFoundException) { return NotFound(); }
    }

    [HttpPost("sessions/{id:guid}/create-ticket")]
    public async Task<IActionResult> CreateTicket(Guid id, CancellationToken ct)
    {
        try
        {
            var ticketId = await chatbotService.CreateTicketAsync(id, ct);
            return Ok(new { ticketId });
        }
        catch (ChatSessionNotFoundException) { return NotFound(); }
    }
}
