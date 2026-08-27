namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Security;
using SupportCrm.Application.Integrations;

// INT-3/INT-4 — admin CRUD + connection test for the configurable connector framework, plus
// INT-2's ERP sync log/trigger (a connector-scoped operation, so it lives on the same controller).
[ApiController]
[Route("api/admin/connectors")]
[Authorize]
public class ConnectorsController(IntegrationConnectorService connectorService, ErpSyncService erpSyncService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Integrations", "View")]
    public async Task<ActionResult<IReadOnlyList<ConnectorDto>>> GetAll(CancellationToken ct) => Ok(await connectorService.GetAllAsync(ct));

    [HttpPost]
    [RequirePermission("Integrations", "Create")]
    public async Task<ActionResult<ConnectorDto>> Create([FromBody] CreateConnectorRequest request, CancellationToken ct) =>
        Ok(await connectorService.CreateAsync(request, ct));

    [HttpPut("{id:guid}/config")]
    [RequirePermission("Integrations", "Edit")]
    public async Task<ActionResult<ConnectorDto>> UpdateConfig(Guid id, [FromBody] UpdateConnectorConfigRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await connectorService.UpdateConfigAsync(id, request, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}/enabled")]
    [RequirePermission("Integrations", "Edit")]
    public async Task<IActionResult> SetEnabled(Guid id, [FromBody] bool enabled, CancellationToken ct)
    {
        try
        {
            await connectorService.SetEnabledAsync(id, enabled, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/test")]
    [RequirePermission("Integrations", "Edit")]
    public async Task<ActionResult<ConnectorTestResultDto>> TestConnection(Guid id, CancellationToken ct)
    {
        try
        {
            return Ok(await connectorService.TestConnectionAsync(id, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    // INT-2 — the manual "trigger" alongside ErpSyncHostedService's "defined schedule."
    [HttpPost("erp/sync")]
    [RequirePermission("Integrations", "Edit")]
    public async Task<IActionResult> TriggerErpSync(CancellationToken ct)
    {
        await erpSyncService.SyncAllAsync(ct);
        return NoContent();
    }

    [HttpGet("erp/sync-logs")]
    [RequirePermission("Integrations", "View")]
    public async Task<ActionResult<IReadOnlyList<ErpSyncLogDto>>> GetErpSyncLogs([FromQuery] Guid? customerId, CancellationToken ct) =>
        Ok(await erpSyncService.GetLogsAsync(customerId, ct));
}
