# Story 43 — Customer satisfaction (Story: RM-4)

---

## Prerequisites

- Story 40 completed: [`40-story-RM-1.md`](40-story-RM-1.md) — `Reports` bounded concern, `IReportExporter`, `TicketReportService.BucketStart`.
- Customer Portal Story 39 completed — `TicketFeedback`, `CustomerPortalOptions.LowRatingThreshold` (reused as the "negative feedback" cutoff).

---

## Story Goal

1. `GET /api/reports/csat` — overall average rating, segmented by category/agent/channel, a weekly trend, and a list of negative-feedback comments.
2. `GET /api/reports/csat/export?format=xlsx|pdf` — the same filtered feedback rows exported.
3. **No NPS** — see Extra Notes in the intake for why this story does not fabricate one from CSAT data.

---

## Context — Read These Files First

1. `src/SupportCrm.Domain/Entities/TicketFeedback.cs` (all ~25 lines) — the sole data source.
2. `src/SupportCrm.Application/CustomerPortal/CustomerPortalOptions.cs` — `LowRatingThreshold`, reused directly as this story's "negative" cutoff (no second threshold introduced).
3. `src/SupportCrm.Application/Reports/TicketReportService.cs` (Story 40) — `BucketStart` (reused with `Weekly`) and the export-endpoint shape this story's `csat/export` action copies.

---

## Backend Tasks

### 1 — DTOs

**File: `src/SupportCrm.Application/Reports/ReportDtos.cs`** — append:

```csharp
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
```

### 2 — `CsatReportService`

**Create file: `src/SupportCrm.Application/Reports/CsatReportService.cs`**

```csharp
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
```

### 3 — Infrastructure: DI

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<CsatReportService>();
```

### 4 — Api: `ReportsController` additions

**File: `src/SupportCrm.Api/Controllers/ReportsController.cs`** — inject `CsatReportService`, add:

```csharp

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
```

---

## Edge Cases & Failure Modes

- **No feedback submitted yet at all** — `OverallAverageRating = 0`, `TotalRatingsCount = 0`, every segment/trend list empty, `NegativeFeedback` empty — not an error.
- **A ticket referenced by feedback no longer resolves** (defensive only — can't actually happen, `TicketFeedback.TicketId` has no delete path) — that row is skipped rather than throwing.
- **A rating exactly at `LowRatingThreshold`** — included in `NegativeFeedback` (`<=`), same inclusive convention `TicketFeedbackService.SubmitAsync` itself already uses for triggering the supervisor follow-up task.
- **Exporting with a filter that matches zero rows** — a header-only file, not a `404`, same as RM-1's export.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Reports/CsatReportServiceTests.cs`**:
   - `GetReportAsync_SegmentsByCategoryAgentAndChannel`
   - `GetReportAsync_NegativeFeedback_UsesLowRatingThresholdInclusive`
   - `GetReportAsync_EmptyFeedback_ReturnsZeroNotError`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Manual smoke:** call `GET /api/reports/csat/export?format=xlsx`, confirm the file opens.

---

## Done Criteria

- [ ] `GET /api/reports/csat` returns overall/segmented scores, a weekly trend, and negative feedback with comments.
- [ ] `GET /api/reports/csat/export` produces a genuine, openable file.
- [ ] No NPS field/calculation exists anywhere in this story's output.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
