namespace SupportCrm.Application.Reports;

using SupportCrm.Domain.Entities;

public enum ReportGranularity { Daily, Weekly, Monthly }
public enum ReportExportFormat { Xlsx, Pdf }

// RM-1 — ticket volume
public record TicketReportQuery(DateTimeOffset? From, DateTimeOffset? To, TicketChannel? Channel, Guid? CategoryId, string? Branch, ReportGranularity Granularity, Guid? DepartmentId = null);
public record VolumeTrendPointDto(DateOnly PeriodStart, int Count);
public record TicketVolumeReportDto(
    int TotalCount,
    IReadOnlyList<VolumeTrendPointDto> Trend,
    IReadOnlyDictionary<string, int> ByChannel,
    IReadOnlyDictionary<string, int> ByCategory,
    IReadOnlyDictionary<string, int> ByBranch,
    IReadOnlyDictionary<string, int>? ByDepartment = null);

// Export — shared by RM-1 and RM-4
public record ReportExportData(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

// RM-2 — SLA compliance
public record SlaComplianceReportQuery(DateTimeOffset? From, DateTimeOffset? To, Guid? TeamId, Guid? AgentId, Guid? CategoryId, TicketPriority? Priority);
public record SlaBreakdownDto(string Key, int EvaluatedCount, int BreachedCount, double CompliancePercentage);
public record SlaCompliancePointDto(DateOnly PeriodStart, int EvaluatedCount, double ResponseCompliancePercentage, double ResolutionCompliancePercentage);
public record SlaComplianceReportDto(
    int EvaluatedCount,
    int InProgressNotYetEvaluableCount,
    int NoPolicyCount,
    double ResponseCompliancePercentage,
    double ResolutionCompliancePercentage,
    IReadOnlyList<SlaBreakdownDto> ByTeam,
    IReadOnlyList<SlaBreakdownDto> ByAgent,
    IReadOnlyList<SlaBreakdownDto> ByCategory,
    IReadOnlyList<SlaBreakdownDto> ByPriority,
    IReadOnlyList<SlaCompliancePointDto> WeeklyTrend);

// RM-3 — agent performance
public record AgentPerformanceQuery(DateTimeOffset? From, DateTimeOffset? To, Guid? AgentId);
public record AgentPerformanceDto(
    Guid AgentId, string AgentName,
    int TicketsResolvedCount,
    double? AverageResponseMinutes,
    double? AverageResolutionMinutes,
    double? AverageCsatRating,
    int CsatResponseCount,
    int ReassignedAwayCount);

// RM-4 — customer satisfaction
public record CsatReportQuery(DateTimeOffset? From, DateTimeOffset? To, Guid? CategoryId, Guid? AgentId, TicketChannel? Channel);
public record CsatSegmentDto(string Key, double AverageRating, int Count);
public record CsatTrendPointDto(DateOnly PeriodStart, double AverageRating, int Count);
public record NegativeFeedbackDto(Guid TicketId, string ReferenceNumber, int Rating, string? Comment, DateTimeOffset SubmittedAtUtc);
public record CsatReportDto(
    double OverallAverageRating,
    int TotalRatingsCount,
    IReadOnlyList<CsatSegmentDto> ByCategory,
    IReadOnlyList<CsatSegmentDto> ByAgent,
    IReadOnlyList<CsatSegmentDto> ByChannel,
    IReadOnlyList<CsatTrendPointDto> Trend,
    IReadOnlyList<NegativeFeedbackDto> NegativeFeedback);

// RM-5 — management dashboard
public record ManagementDashboardQuery(DateTimeOffset? From, DateTimeOffset? To, string? Branch);
public record ManagementDashboardDto(
    TicketVolumeReportDto Volume,
    SlaComplianceReportDto SlaCompliance,
    CsatReportDto Csat,
    IReadOnlyList<AgentPerformanceDto> AgentPerformance,
    DateTimeOffset GeneratedAtUtc);
