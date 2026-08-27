namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/chat-sessions")]
public class ChatController(ChatService chatService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ChatSessionDto>> Start([FromBody] StartChatRequest request, CancellationToken ct) =>
        await chatService.StartAsync(request, ct);

    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<ChatQueueStatusDto>> GetStatus(Guid id, CancellationToken ct)
    {
        try { return await chatService.GetQueueStatusAsync(id, ct); }
        catch (ChatSessionNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<ChatMessageDto>> AddMessage(Guid id, [FromBody] SendChatMessageRequest request, CancellationToken ct)
    {
        try { return await chatService.AddMessageAsync(id, request, ct); }
        catch (ChatSessionNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> GetMessages(Guid id, CancellationToken ct) =>
        Ok(await chatService.GetMessagesAsync(id, ct));

    [HttpPut("{id:guid}/typing")]
    public async Task<IActionResult> SetTyping(Guid id, [FromBody] SetTypingRequest request, CancellationToken ct)
    {
        try { await chatService.SetTypingAsync(id, request, ct); return NoContent(); }
        catch (ChatSessionNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        try
        {
            var ticketId = await chatService.CompleteAsync(id, ct);
            return Ok(new { ticketId });
        }
        catch (ChatSessionNotFoundException) { return NotFound(); }
    }
}
