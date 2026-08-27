namespace SupportCrm.Application.Reports;

// Pure composition — every number here is produced by RM-1..4's own services, called
// exactly as a standalone caller of each report would. Nothing is recomputed or duplicated.
public class ManagementDashboardService(
    TicketReportService ticketReportService,
    SlaComplianceService slaComplianceService,
    AgentPerformanceService agentPerformanceService,
    CsatReportService csatReportService,
    TimeProvider timeProvider)
{
    public async Task<ManagementDashboardDto> GetDashboardAsync(ManagementDashboardQuery query, CancellationToken ct)
    {
        // Scope note: Branch only reaches the volume widget. RM-2/RM-3/RM-4's own query shapes
        // (team/agent/category/priority; date+agent; category/agent/channel) never asked for a
        // branch dimension per their own ACs — widening all three to add one is a reasonable
        // follow-up, not something this composition story invents on their behalf.
        var volume = await ticketReportService.GetVolumeReportAsync(new TicketReportQuery(query.From, query.To, null, null, query.Branch, ReportGranularity.Daily), ct);
        var slaCompliance = await slaComplianceService.GetComplianceReportAsync(new SlaComplianceReportQuery(query.From, query.To, null, null, null, null), ct);
        var csat = await csatReportService.GetReportAsync(new CsatReportQuery(query.From, query.To, null, null, null), ct);
        var agentPerformance = await agentPerformanceService.GetPerformanceAsync(new AgentPerformanceQuery(query.From, query.To, null), ct);

        return new ManagementDashboardDto(volume, slaCompliance, csat, agentPerformance, timeProvider.GetUtcNow());
    }
}
