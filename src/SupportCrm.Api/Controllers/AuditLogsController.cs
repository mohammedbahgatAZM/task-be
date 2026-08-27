namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Security;
using SupportCrm.Application.Security;
using SupportCrm.Application.Reports;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize]
public class AuditLogsController(AuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Administration", "View")]
    public async Task<ActionResult<IReadOnlyList<AuditLogEntryDto>>> GetLogs(
        [FromQuery] Guid? userId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? actionType, CancellationToken ct) =>
        Ok(await auditLogService.GetLogsAsync(new AuditLogQuery(userId, from, to, actionType), ct));

    [HttpGet("export")]
    [RequirePermission("Administration", "Export")]
    public async Task<IActionResult> Export(
        [FromQuery] Guid? userId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? actionType,
        [FromQuery] ReportExportFormat format, CancellationToken ct)
    {
        var bytes = await auditLogService.ExportAsync(new AuditLogQuery(userId, from, to, actionType), format, ct);
        var (contentType, fileName) = format == ReportExportFormat.Xlsx
            ? ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "audit-log.xlsx")
            : ("application/pdf", "audit-log.pdf");
        return File(bytes, contentType, fileName);
    }
}
