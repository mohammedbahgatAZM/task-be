namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Security;
using SupportCrm.Application.Integrations;

[ApiController]
[Route("api/admin/webhooks")]
[Authorize]
public class WebhooksController(WebhookService webhookService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Integrations", "View")]
    public async Task<ActionResult<IReadOnlyList<WebhookDto>>> GetAll(CancellationToken ct) => Ok(await webhookService.GetAllAsync(ct));

    [HttpGet("event-types")]
    [RequirePermission("Integrations", "View")]
    public ActionResult<IReadOnlyList<string>> GetKnownEventTypes() => Ok(WebhookEventTypes.All);

    [HttpPost]
    [RequirePermission("Integrations", "Create")]
    public async Task<ActionResult<WebhookCreatedDto>> Create([FromBody] CreateWebhookRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await webhookService.CreateAsync(request, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/active")]
    [RequirePermission("Integrations", "Edit")]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] bool isActive, CancellationToken ct)
    {
        try
        {
            await webhookService.SetActiveAsync(id, isActive, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpGet("deliveries")]
    [RequirePermission("Integrations", "View")]
    public async Task<ActionResult<IReadOnlyList<WebhookDeliveryDto>>> GetDeliveries([FromQuery] Guid? subscriptionId, CancellationToken ct) =>
        Ok(await webhookService.GetDeliveriesAsync(subscriptionId, ct));

    [HttpPost("deliveries/{deliveryId:guid}/redeliver")]
    [RequirePermission("Integrations", "Edit")]
    public async Task<ActionResult<WebhookDeliveryDto>> Redeliver(Guid deliveryId, CancellationToken ct)
    {
        try
        {
            return Ok(await webhookService.RedeliverAsync(deliveryId, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }
}
