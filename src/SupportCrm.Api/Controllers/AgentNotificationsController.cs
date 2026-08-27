namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/agents/{agentId:guid}/notifications")]
public class AgentNotificationsController(AgentNotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentNotificationDto>>> GetAll(Guid agentId, CancellationToken ct) =>
        Ok(await notificationService.GetForAgentAsync(agentId, ct));

    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid agentId, Guid notificationId, CancellationToken ct)
    {
        try { await notificationService.MarkReadAsync(notificationId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
