namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Reports;
using SupportCrm.Domain.Entities;

[ApiController]
[Route("api/reports")]
public class ReportsController(TicketReportService ticketReportService) : ControllerBase
{
    // RM-1 — ticket volume
    [HttpGet("tickets")]
    public async Task<ActionResult<TicketVolumeReportDto>> GetTicketReport(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] TicketChannel? channel,
        [FromQuery] Guid? categoryId, [FromQuery] string? branch, [FromQuery] ReportGranularity granularity,
        [FromQuery] Guid? departmentId, CancellationToken ct) =>
        Ok(await ticketReportService.GetVolumeReportAsync(new TicketReportQuery(from, to, channel, categoryId, branch, granularity, departmentId), ct));

    [HttpGet("tickets/export")]
    public async Task<IActionResult> ExportTicketReport(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] TicketChannel? channel,
        [FromQuery] Guid? categoryId, [FromQuery] string? branch, [FromQuery] ReportGranularity granularity,
        [FromQuery] Guid? departmentId, [FromQuery] ReportExportFormat format, CancellationToken ct)
    {
        var bytes = await ticketReportService.ExportVolumeReportAsync(new TicketReportQuery(from, to, channel, categoryId, branch, granularity, departmentId), format, ct);
        var (contentType, fileName) = format == ReportExportFormat.Xlsx
            ? ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ticket-report.xlsx")
            : ("application/pdf", "ticket-report.pdf");
        return File(bytes, contentType, fileName);
    }

    // RM-2 — SLA compliance
    [HttpGet("sla-compliance")]
    public async Task<ActionResult<SlaComplianceReportDto>> GetSlaComplianceReport(
        [FromServices] SlaComplianceService slaComplianceService,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] Guid? teamId,
        [FromQuery] Guid? agentId, [FromQuery] Guid? categoryId, [FromQuery] TicketPriority? priority, CancellationToken ct) =>
        Ok(await slaComplianceService.GetComplianceReportAsync(new SlaComplianceReportQuery(from, to, teamId, agentId, categoryId, priority), ct));

    // RM-3 — agent performance
    [HttpGet("agent-performance")]
    public async Task<ActionResult<IReadOnlyList<AgentPerformanceDto>>> GetAgentPerformance(
        [FromServices] AgentPerformanceService agentPerformanceService,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] Guid? agentId, CancellationToken ct) =>
        Ok(await agentPerformanceService.GetPerformanceAsync(new AgentPerformanceQuery(from, to, agentId), ct));

    // RM-4 — customer satisfaction
    [HttpGet("csat")]
    public async Task<ActionResult<CsatReportDto>> GetCsatReport(
        [FromServices] CsatReportService csatReportService,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] Guid? categoryId,
        [FromQuery] Guid? agentId, [FromQuery] TicketChannel? channel, CancellationToken ct) =>
        Ok(await csatReportService.GetReportAsync(new CsatReportQuery(from, to, categoryId, agentId, channel), ct));

    [HttpGet("csat/export")]
    public async Task<IActionResult> ExportCsatReport(
        [FromServices] CsatReportService csatReportService,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] Guid? categoryId,
        [FromQuery] Guid? agentId, [FromQuery] TicketChannel? channel, [FromQuery] ReportExportFormat format, CancellationToken ct)
    {
        var bytes = await csatReportService.ExportAsync(new CsatReportQuery(from, to, categoryId, agentId, channel), format, ct);
        var (contentType, fileName) = format == ReportExportFormat.Xlsx
            ? ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "csat-report.xlsx")
            : ("application/pdf", "csat-report.pdf");
        return File(bytes, contentType, fileName);
    }

    // RM-5 — management dashboard
    [HttpGet("dashboard")]
    public async Task<ActionResult<ManagementDashboardDto>> GetDashboard(
        [FromServices] ManagementDashboardService dashboardService,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? branch, CancellationToken ct) =>
        Ok(await dashboardService.GetDashboardAsync(new ManagementDashboardQuery(from, to, branch), ct));
}
