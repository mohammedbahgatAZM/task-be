# Story 44 — Management dashboards (Story: RM-5)

---

## Prerequisites

- Stories 40–43 completed — `TicketReportService`, `SlaComplianceService`, `AgentPerformanceService`, `CsatReportService`. This story adds no new metric calculations; it composes theirs.

---

## Story Goal

1. `GET /api/reports/dashboard` — one call returning ticket volume, SLA compliance, CSAT, and agent performance together.
2. Widget layout customization is **client-side only** (no backend change — see Extra Notes in the intake for why).
3. Date-range filtering reaches every widget; branch filtering reaches the volume widget only (see the scope note below — a deliberate, documented boundary, not an oversight).

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Reports/TicketReportService.cs`, `SlaComplianceService.cs`, `AgentPerformanceService.cs`, `CsatReportService.cs` (Stories 40–43) — this story calls their public methods only; it reads none of their internals.

---

## Backend Tasks

### 1 — DTOs

**File: `src/SupportCrm.Application/Reports/ReportDtos.cs`** — append:

```csharp
// RM-5 — management dashboard
public record ManagementDashboardQuery(DateTimeOffset? From, DateTimeOffset? To, string? Branch);
public record ManagementDashboardDto(
    TicketVolumeReportDto Volume,
    SlaComplianceReportDto SlaCompliance,
    CsatReportDto Csat,
    IReadOnlyList<AgentPerformanceDto> AgentPerformance,
    DateTimeOffset GeneratedAtUtc);
```

### 2 — `ManagementDashboardService`

**Create file: `src/SupportCrm.Application/Reports/ManagementDashboardService.cs`**

```csharp
namespace SupportCrm.Application.Reports;

// Pure composition — every number here is produced by Stories 40–43's own services, called
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
```

### 3 — Infrastructure: DI

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<ManagementDashboardService>();
```

### 4 — Api: `ReportsController` addition

**File: `src/SupportCrm.Api/Controllers/ReportsController.cs`** — inject `ManagementDashboardService`, add:

```csharp

    [HttpGet("dashboard")]
    public async Task<ActionResult<ManagementDashboardDto>> GetDashboard(
        [FromServices] ManagementDashboardService dashboardService,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? branch, CancellationToken ct) =>
        Ok(await dashboardService.GetDashboardAsync(new ManagementDashboardQuery(from, to, branch), ct));
```

---

## Edge Cases & Failure Modes

- **One of the four underlying reports throws** (shouldn't happen — none of Stories 40–43's methods throw on empty/no-match input, only on truly invalid state) — this composition method has no try/catch of its own; a failure surfaces as a `500` for the whole dashboard rather than a partially-populated response, which is the more honest failure mode for a single-call composite endpoint.
- **`from`/`to` omitted entirely** — every underlying report already treats a missing `From`/`To` as "no lower/upper bound," so the dashboard naturally shows all-time figures — no special-casing needed here.
- **`branch` provided but no ticket anywhere has that branch** — the volume widget's `TotalCount` is `0` and its breakdowns are empty; the other three widgets are computed over the *unfiltered-by-branch* date range (per the scope note above) — a manager reading the dashboard sees a `0`-volume widget next to non-zero SLA/CSAT/agent widgets, which is the correct, honest picture of what was actually filtered, not a bug.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Reports/ManagementDashboardServiceTests.cs`**:
   - `GetDashboardAsync_ComposesAllFourReports_WithTheSameDateRange`
   - `GetDashboardAsync_BranchFilter_OnlyAppliesToVolumeWidget`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.

---

## Done Criteria

- [ ] `GET /api/reports/dashboard` returns volume, SLA compliance, CSAT, and agent performance in one response.
- [ ] Date range reaches every widget; branch reaches the volume widget, with the boundary documented (not silently partial).
- [ ] `dotnet build SupportCrm.slnx` succeeds.
