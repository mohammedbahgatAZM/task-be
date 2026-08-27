# Story 40 — Ticket reports (Story: RM-1)

---

## Prerequisites

None — first story in this feature.

---

## Story Goal

1. `GET /api/reports/tickets` — ticket volume filtered by date range/channel/category/branch, with a daily/weekly/monthly trend and by-channel/by-category/by-branch breakdowns.
2. `GET /api/reports/tickets/export?format=xlsx|pdf` — the same filtered ticket list as a real, openable Excel or PDF file.
3. The `SupportCrm.Application.Reports` bounded concern and the `IReportExporter` abstraction every later RM story builds on.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Tickets/TicketService.cs`, `GetGroupedCountsAsync` (lines 107–114) — the existing, simpler precedent this story's richer report supersedes for manager-facing reporting (left untouched — still used by the agent-facing `ticket-reports` page).
2. `src/SupportCrm.Infrastructure/Persistence/TicketRepository.cs`, `CustomerRepository.cs`, all of both (short files) — the repositories this story adds one `GetAllAsync` method to each.
3. `src/SupportCrm.Infrastructure/DependencyInjection.cs` — the single registration point every new service/repo/exporter is added to.

---

## Backend Tasks

### 1 — Repository gap-fills

**File: `src/SupportCrm.Domain/Repositories/ITicketRepository.cs`** — add:

```csharp
    Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken ct);
```

**File: `src/SupportCrm.Infrastructure/Persistence/TicketRepository.cs`** — add:

```csharp
    public async Task<IReadOnlyList<Ticket>> GetAllAsync(CancellationToken ct) =>
        await dbContext.Tickets.ToListAsync(ct);
```

**File: `src/SupportCrm.Domain/Repositories/ICustomerRepository.cs`** — add:

```csharp
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct);
```

**File: `src/SupportCrm.Infrastructure/Persistence/CustomerRepository.cs`** — add:

```csharp
    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken ct) =>
        await dbContext.Customers.ToListAsync(ct);
```

**File: `src/SupportCrm.Domain/Repositories/ITicketCategoryRepository.cs`** — add:

```csharp
    Task<IReadOnlyList<TicketCategory>> GetAllAsync(CancellationToken ct);
```

**File: `src/SupportCrm.Infrastructure/Persistence/TicketCategoryRepository.cs`** — add (mirrors the existing `GetActiveAsync`, just without the filter — read that method first to match its exact shape):

```csharp
    public async Task<IReadOnlyList<TicketCategory>> GetAllAsync(CancellationToken ct) =>
        await dbContext.TicketCategories.ToListAsync(ct);
```

### 2 — Shared DTOs, export abstraction

**Create file: `src/SupportCrm.Application/Reports/ReportDtos.cs`**

```csharp
namespace SupportCrm.Application.Reports;

using SupportCrm.Domain.Entities;

public enum ReportGranularity { Daily, Weekly, Monthly }
public enum ReportExportFormat { Xlsx, Pdf }

// RM-1 — ticket volume
public record TicketReportQuery(DateTimeOffset? From, DateTimeOffset? To, TicketChannel? Channel, Guid? CategoryId, string? Branch, ReportGranularity Granularity);
public record VolumeTrendPointDto(DateOnly PeriodStart, int Count);
public record TicketVolumeReportDto(
    int TotalCount,
    IReadOnlyList<VolumeTrendPointDto> Trend,
    IReadOnlyDictionary<string, int> ByChannel,
    IReadOnlyDictionary<string, int> ByCategory,
    IReadOnlyDictionary<string, int> ByBranch);

// Export — shared by RM-1 and RM-4
public record ReportExportData(string Title, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);
```

(Stories 41–44 append their own DTOs to this same file, one shared file per the established per-feature convention.)

**Create file: `src/SupportCrm.Application/Reports/IReportExporter.cs`**

```csharp
namespace SupportCrm.Application.Reports;

public interface IReportExporter
{
    byte[] ExportToExcel(ReportExportData data);
    byte[] ExportToPdf(ReportExportData data);
}
```

**Create file: `src/SupportCrm.Infrastructure/Reports/ReportExporter.cs`**

```csharp
namespace SupportCrm.Infrastructure.Reports;

using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SupportCrm.Application.Reports;

public class ReportExporter : IReportExporter
{
    public byte[] ExportToExcel(ReportExportData data)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(Truncate(data.Title, 31)); // Excel sheet-name limit
        for (var col = 0; col < data.Columns.Count; col++)
            sheet.Cell(1, col + 1).Value = data.Columns[col];
        sheet.Row(1).Style.Font.Bold = true;

        for (var row = 0; row < data.Rows.Count; row++)
            for (var col = 0; col < data.Rows[row].Count; col++)
                sheet.Cell(row + 2, col + 1).Value = data.Rows[row][col];

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public byte[] ExportToPdf(ReportExportData data)
    {
        var document = QuestPDF.Fluent.Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Text(data.Title).FontSize(16).Bold();

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var _ in data.Columns) columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var column in data.Columns)
                            header.Cell().Element(CellStyle).Text(column).Bold();
                    });

                    foreach (var row in data.Rows)
                        foreach (var cell in row)
                            table.Cell().Element(CellStyle).Text(cell);
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    private static IContainer CellStyle(IContainer container) =>
        container.PaddingVertical(2).PaddingHorizontal(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
```

### 3 — `TicketReportService`

**Create file: `src/SupportCrm.Application/Reports/TicketReportService.cs`**

```csharp
namespace SupportCrm.Application.Reports;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketReportService(ITicketRepository ticketRepository, ICustomerRepository customerRepository, ITicketCategoryRepository categoryRepository, IReportExporter exporter)
{
    public async Task<TicketVolumeReportDto> GetVolumeReportAsync(TicketReportQuery query, CancellationToken ct)
    {
        var customersById = (await customerRepository.GetAllAsync(ct)).ToDictionary(c => c.Id);
        var tickets = Filter(await ticketRepository.GetAllAsync(ct), query, customersById);

        var categoriesById = (await categoryRepository.GetAllAsync(ct)).ToDictionary(c => c.Id, c => c.Name);

        var byChannel = tickets.GroupBy(t => t.Channel.ToString()).ToDictionary(g => g.Key, g => g.Count());
        var byCategory = tickets
            .GroupBy(t => t.CategoryId is Guid categoryId && categoriesById.TryGetValue(categoryId, out var name) ? name : "Uncategorized")
            .ToDictionary(g => g.Key, g => g.Count());
        var byBranch = tickets
            .GroupBy(t => customersById.TryGetValue(t.CustomerId, out var c) ? (c.Branch ?? "Unspecified") : "Unspecified")
            .ToDictionary(g => g.Key, g => g.Count());

        var trend = tickets
            .GroupBy(t => BucketStart(t.CreatedAtUtc, query.Granularity))
            .OrderBy(g => g.Key)
            .Select(g => new VolumeTrendPointDto(g.Key, g.Count()))
            .ToList();

        return new TicketVolumeReportDto(tickets.Count, trend, byChannel, byCategory, byBranch);
    }

    public async Task<byte[]> ExportVolumeReportAsync(TicketReportQuery query, ReportExportFormat format, CancellationToken ct)
    {
        var customersById = (await customerRepository.GetAllAsync(ct)).ToDictionary(c => c.Id);
        var tickets = Filter(await ticketRepository.GetAllAsync(ct), query, customersById);
        var categoriesById = (await categoryRepository.GetAllAsync(ct)).ToDictionary(c => c.Id, c => c.Name);

        var columns = new[] { "Reference", "Subject", "Channel", "Status", "Priority", "Category", "Branch", "Created (UTC)" };
        var rows = tickets
            .OrderByDescending(t => t.CreatedAtUtc)
            .Select(t => (IReadOnlyList<string>)new[]
            {
                t.ReferenceNumber, t.Subject, t.Channel.ToString(), t.Status.ToString(), t.Priority.ToString(),
                t.CategoryId is Guid categoryId && categoriesById.TryGetValue(categoryId, out var name) ? name : "Uncategorized",
                customersById.TryGetValue(t.CustomerId, out var c) ? (c.Branch ?? "Unspecified") : "Unspecified",
                t.CreatedAtUtc.ToString("u")
            })
            .ToList();

        var data = new ReportExportData("Ticket report", columns, rows);
        return format == ReportExportFormat.Xlsx ? exporter.ExportToExcel(data) : exporter.ExportToPdf(data);
    }

    private static List<Ticket> Filter(IReadOnlyList<Ticket> all, TicketReportQuery query, IReadOnlyDictionary<Guid, Customer> customersById)
    {
        IEnumerable<Ticket> filtered = all;
        if (query.From is not null) filtered = filtered.Where(t => t.CreatedAtUtc >= query.From);
        if (query.To is not null) filtered = filtered.Where(t => t.CreatedAtUtc <= query.To);
        if (query.Channel is not null) filtered = filtered.Where(t => t.Channel == query.Channel);
        if (query.CategoryId is not null) filtered = filtered.Where(t => t.CategoryId == query.CategoryId);
        if (!string.IsNullOrWhiteSpace(query.Branch))
            filtered = filtered.Where(t => customersById.TryGetValue(t.CustomerId, out var c) && string.Equals(c.Branch, query.Branch, StringComparison.OrdinalIgnoreCase));
        return filtered.ToList();
    }

    // Monday-start week, per this codebase's existing calendar convention (BusinessCalendarService's
    // own working-week assumption). Public + static so RM-2's weekly trend can reuse it verbatim.
    public static DateOnly BucketStart(DateTimeOffset dt, ReportGranularity granularity)
    {
        var date = DateOnly.FromDateTime(dt.UtcDateTime);
        return granularity switch
        {
            ReportGranularity.Daily => date,
            ReportGranularity.Weekly => date.AddDays(-((7 + (int)date.DayOfWeek - (int)DayOfWeek.Monday) % 7)),
            ReportGranularity.Monthly => new DateOnly(date.Year, date.Month, 1),
            _ => date
        };
    }
}
```

### 4 — Infrastructure: DI, `Program.cs`

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add `using SupportCrm.Application.Reports;`, `using SupportCrm.Infrastructure.Reports;`, and before `return services;`:

```csharp
        services.AddScoped<IReportExporter, ReportExporter>();
        services.AddScoped<TicketReportService>();
```

**File: `src/SupportCrm.Api/Program.cs`** — QuestPDF requires a one-time license declaration at startup (mandatory since QuestPDF 2023+); add near the top, before `builder.Build()`:

```csharp
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
```

### 5 — Api: `ReportsController`

**Create file: `src/SupportCrm.Api/Controllers/ReportsController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Reports;
using SupportCrm.Domain.Entities;

[ApiController]
[Route("api/reports")]
public class ReportsController(TicketReportService ticketReportService) : ControllerBase
{
    [HttpGet("tickets")]
    public async Task<ActionResult<TicketVolumeReportDto>> GetTicketReport(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] TicketChannel? channel,
        [FromQuery] Guid? categoryId, [FromQuery] string? branch, [FromQuery] ReportGranularity granularity, CancellationToken ct) =>
        Ok(await ticketReportService.GetVolumeReportAsync(new TicketReportQuery(from, to, channel, categoryId, branch, granularity), ct));

    [HttpGet("tickets/export")]
    public async Task<IActionResult> ExportTicketReport(
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] TicketChannel? channel,
        [FromQuery] Guid? categoryId, [FromQuery] string? branch, [FromQuery] ReportGranularity granularity,
        [FromQuery] ReportExportFormat format, CancellationToken ct)
    {
        var bytes = await ticketReportService.ExportVolumeReportAsync(new TicketReportQuery(from, to, channel, categoryId, branch, granularity), format, ct);
        var (contentType, fileName) = format == ReportExportFormat.Xlsx
            ? ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ticket-report.xlsx")
            : ("application/pdf", "ticket-report.pdf");
        return File(bytes, contentType, fileName);
    }
}
```

(Stories 41–44 add their own actions to this same controller.)

---

## Edge Cases & Failure Modes

- **No tickets match the filter** — `TotalCount = 0`, empty `Trend`/breakdown dictionaries, not an error; the export produces a header-only file, not a `404`.
- **A ticket's `CategoryId` references a category that's since been deleted** — can't happen (categories are never deleted, only deactivated — `GetAllAsync` here intentionally includes inactive ones so old tickets still resolve a name instead of falling into "Uncategorized" incorrectly).
- **A customer's `Branch` is `null`** — grouped under `"Unspecified"`, not silently dropped from the totals.
- **`granularity` omitted from the query string** — model-binds to the enum's default value (`Daily`, value `0`) — acceptable; the frontend always sends it explicitly.
- **Excel sheet title longer than 31 characters** — `Truncate` prevents `ClosedXML` throwing on Excel's own sheet-name length limit.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Reports/TicketReportServiceTests.cs`**:
   - `GetVolumeReportAsync_FiltersByChannelAndDateRange`
   - `BucketStart_Weekly_AlwaysReturnsAMonday`
2. **Unit — `tests/SupportCrm.Infrastructure.Tests/Reports/ReportExporterTests.cs`**:
   - `ExportToExcel_ProducesAValidWorkbook_ReadableByClosedXML`
   - `ExportToPdf_ProducesNonEmptyBytes`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Manual smoke:** call `GET /api/reports/tickets/export?format=xlsx` and `?format=pdf`, confirm both files open without a repair prompt.

---

## Done Criteria

- [ ] `GET /api/reports/tickets` filters by date/channel/category/branch and returns a daily/weekly/monthly trend.
- [ ] `GET /api/reports/tickets/export` produces genuine, openable `.xlsx`/PDF files.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
