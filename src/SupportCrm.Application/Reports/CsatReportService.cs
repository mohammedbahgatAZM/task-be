namespace SupportCrm.Application.Reports;

using Microsoft.Extensions.Options;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.CustomerPortal;

public class CsatReportService(
    ITicketFeedbackRepository feedbackRepository,
    ITicketRepository ticketRepository,
    ITicketCategoryRepository categoryRepository,
    IAgentRepository agentRepository,
    IOptions<CustomerPortalOptions> portalOptions,
    IReportExporter exporter)
{
    private record FeedbackRow(Guid TicketId, string ReferenceNumber, int Rating, string? Comment, DateTimeOffset SubmittedAtUtc, string CategoryName, string AgentName, string Channel);

    public async Task<CsatReportDto> GetReportAsync(CsatReportQuery query, CancellationToken ct) =>
        BuildDto(await BuildRowsAsync(query, ct), portalOptions.Value.LowRatingThreshold);

    public async Task<byte[]> ExportAsync(CsatReportQuery query, ReportExportFormat format, CancellationToken ct)
    {
        var rows = await BuildRowsAsync(query, ct);
        var columns = new[] { "Reference", "Rating", "Comment", "Category", "Agent", "Channel", "Submitted (UTC)" };
        var exportRows = rows
            .OrderByDescending(r => r.SubmittedAtUtc)
            .Select(r => (IReadOnlyList<string>)new[] { r.ReferenceNumber, r.Rating.ToString(), r.Comment ?? "", r.CategoryName, r.AgentName, r.Channel, r.SubmittedAtUtc.ToString("u") })
            .ToList();
        var data = new ReportExportData("Customer satisfaction report", columns, exportRows);
        return format == ReportExportFormat.Xlsx ? exporter.ExportToExcel(data) : exporter.ExportToPdf(data);
    }

    private async Task<List<FeedbackRow>> BuildRowsAsync(CsatReportQuery query, CancellationToken ct)
    {
        var feedback = await feedbackRepository.GetAllAsync(ct);
        var tickets = (await ticketRepository.GetAllAsync(ct)).ToDictionary(t => t.Id);
        var categoriesById = (await categoryRepository.GetAllAsync(ct)).ToDictionary(c => c.Id, c => c.Name);
        var agentsById = (await agentRepository.GetAllAsync(ct)).ToDictionary(a => a.Id, a => a.Name);

        var rows = new List<FeedbackRow>();
        foreach (var f in feedback)
        {
            if (!tickets.TryGetValue(f.TicketId, out var ticket)) continue; // defensive — feedback always has a ticket by construction
            if (query.From is not null && f.SubmittedAtUtc < query.From) continue;
            if (query.To is not null && f.SubmittedAtUtc > query.To) continue;
            if (query.CategoryId is not null && ticket.CategoryId != query.CategoryId) continue;
            if (query.AgentId is not null && ticket.AssignedAgentId != query.AgentId) continue;
            if (query.Channel is not null && ticket.Channel != query.Channel) continue;

            rows.Add(new FeedbackRow(
                ticket.Id, ticket.ReferenceNumber, f.Rating, f.Comment, f.SubmittedAtUtc,
                ticket.CategoryId is Guid categoryId && categoriesById.TryGetValue(categoryId, out var catName) ? catName : "Uncategorized",
                ticket.AssignedAgentId is Guid agentId && agentsById.TryGetValue(agentId, out var agentName) ? agentName : "Unassigned",
                ticket.Channel.ToString()));
        }
        return rows;
    }

    private static CsatReportDto BuildDto(List<FeedbackRow> rows, int lowRatingThreshold)
    {
        var overall = rows.Count > 0 ? Math.Round(rows.Average(r => r.Rating), 2) : 0;

        var byCategory = Segment(rows, r => r.CategoryName);
        var byAgent = Segment(rows, r => r.AgentName);
        var byChannel = Segment(rows, r => r.Channel);

        var trend = rows
            .GroupBy(r => TicketReportService.BucketStart(r.SubmittedAtUtc, ReportGranularity.Weekly))
            .OrderBy(g => g.Key)
            .Select(g => new CsatTrendPointDto(g.Key, Math.Round(g.Average(r => r.Rating), 2), g.Count()))
            .ToList();

        var negative = rows
            .Where(r => r.Rating <= lowRatingThreshold)
            .OrderByDescending(r => r.SubmittedAtUtc)
            .Select(r => new NegativeFeedbackDto(r.TicketId, r.ReferenceNumber, r.Rating, r.Comment, r.SubmittedAtUtc))
            .ToList();

        return new CsatReportDto(overall, rows.Count, byCategory, byAgent, byChannel, trend, negative);
    }

    private static List<CsatSegmentDto> Segment(List<FeedbackRow> rows, Func<FeedbackRow, string> keySelector) =>
        rows.GroupBy(keySelector)
            .Select(g => new CsatSegmentDto(g.Key, Math.Round(g.Average(r => r.Rating), 2), g.Count()))
            .OrderBy(s => s.Key)
            .ToList();
}
