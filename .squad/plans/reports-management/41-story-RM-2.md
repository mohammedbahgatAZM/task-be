# Story 41 — SLA performance (Story: RM-2)

---

## Prerequisites

- Story 40 completed: [`40-story-RM-1.md`](40-story-RM-1.md) — `Reports` bounded concern, `ITicketRepository.GetAllAsync`, `TicketReportService.BucketStart` (reused for this story's weekly trend).
- SLA & Automation Stories 21/22 completed — `SlaCalculationService`, `SlaTargetService.ResolveAsync`, `BusinessCalendarService.CalculateBusinessMinutesBetweenAsync`.

---

## Story Goal

1. `GET /api/reports/sla-compliance` — overall response/resolution compliance percentage, filterable by date range/team/agent/category/priority.
2. Breach breakdowns by team, agent, category, priority.
3. A weekly compliance trend.
4. Reuses `SlaCalculationService` verbatim for currently-open tickets — so this report's numbers for an open ticket can never disagree with that same ticket's own SLA status display.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Sla/SlaCalculationService.cs` (all of it) — `GetStatusesAsync` is called directly for open tickets; `GetPendingBusinessMinutesAsync` (private, bounded to "now") is the shape this story's own pending-pause helper mirrors, bounded to a historical event instead.
2. `src/SupportCrm.Application/Sla/SlaTargetService.cs`, `ResolveAsync` — reused directly for closed tickets (open tickets already get this via `SlaCalculationService`).
3. `src/SupportCrm.Application/Reports/TicketReportService.cs` (Story 40) — `BucketStart`, reused with `ReportGranularity.Weekly` for this story's trend.

---

## Backend Tasks

### 1 — `ITicketMessageRepository` batch lookup

**File: `src/SupportCrm.Domain/Repositories/ITicketMessageRepository.cs`** — add:

```csharp
    Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetFirstAgentMessageTimesAsync(IReadOnlyList<Guid> ticketIds, CancellationToken ct);
```

**File: `src/SupportCrm.Infrastructure/Persistence/TicketMessageRepository.cs`** — add:

```csharp
    public async Task<IReadOnlyDictionary<Guid, DateTimeOffset>> GetFirstAgentMessageTimesAsync(IReadOnlyList<Guid> ticketIds, CancellationToken ct) =>
        await dbContext.TicketMessages
            .Where(m => ticketIds.Contains(m.TicketId) && m.AuthorKind == "Agent")
            .GroupBy(m => m.TicketId)
            .Select(g => new { TicketId = g.Key, FirstAt = g.Min(m => m.CreatedAtUtc) })
            .ToDictionaryAsync(x => x.TicketId, x => x.FirstAt, ct);
```

(One grouped SQL query for every closed ticket in the report — not a per-ticket round trip.)

### 2 — DTOs

**File: `src/SupportCrm.Application/Reports/ReportDtos.cs`** — append:

```csharp
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
```

### 3 — `SlaComplianceService`

**Create file: `src/SupportCrm.Application/Reports/SlaComplianceService.cs`**

```csharp
namespace SupportCrm.Application.Reports;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Sla;

public class SlaComplianceService(
    ITicketRepository ticketRepository,
    ICustomerRepository customerRepository,
    ITicketMessageRepository messageRepository,
    ITeamRepository teamRepository,
    IAgentRepository agentRepository,
    ITicketCategoryRepository categoryRepository,
    SlaTargetService targetService,
    SlaCalculationService slaCalculationService,
    BusinessCalendarService calendarService)
{
    // Design note: breakdowns (ByTeam/ByAgent/ByCategory/ByPriority) report RESOLUTION-SLA
    // compliance specifically — the metric a support manager most commonly tracks day to day.
    // The overall response-vs-resolution split is still available at the top level.
    private record TicketSlaEvaluation(
        Guid TicketId, DateTimeOffset CreatedAtUtc, Guid? TeamId, Guid? AgentId, Guid? CategoryId, TicketPriority Priority,
        bool HasPolicy, bool? ResponseMet, bool? ResolutionMet);

    public async Task<SlaComplianceReportDto> GetComplianceReportAsync(SlaComplianceReportQuery query, CancellationToken ct)
    {
        var all = await ticketRepository.GetAllAsync(ct);
        IEnumerable<Ticket> filtered = all;
        if (query.From is not null) filtered = filtered.Where(t => t.CreatedAtUtc >= query.From);
        if (query.To is not null) filtered = filtered.Where(t => t.CreatedAtUtc <= query.To);
        if (query.TeamId is not null) filtered = filtered.Where(t => t.AssignedTeamId == query.TeamId);
        if (query.AgentId is not null) filtered = filtered.Where(t => t.AssignedAgentId == query.AgentId);
        if (query.CategoryId is not null) filtered = filtered.Where(t => t.CategoryId == query.CategoryId);
        if (query.Priority is not null) filtered = filtered.Where(t => t.Priority == query.Priority);
        var tickets = filtered.ToList();

        var evaluations = await EvaluateTicketsAsync(tickets, ct);
        var evaluated = evaluations.Where(e => e.HasPolicy).ToList();
        var noPolicyCount = evaluations.Count - evaluated.Count;
        var inProgressCount = evaluated.Count(e => e.ResponseMet is null && e.ResolutionMet is null);

        var responseJudged = evaluated.Where(e => e.ResponseMet is not null).ToList();
        var resolutionJudged = evaluated.Where(e => e.ResolutionMet is not null).ToList();

        var responseCompliance = Percentage(responseJudged, e => e.ResponseMet == true);
        var resolutionCompliance = Percentage(resolutionJudged, e => e.ResolutionMet == true);

        var teamsById = (await teamRepository.GetAllAsync(ct)).ToDictionary(t => t.Id, t => t.Name);
        var agentsById = (await agentRepository.GetAllAsync(ct)).ToDictionary(a => a.Id, a => a.Name);
        var categoriesById = (await categoryRepository.GetAllAsync(ct)).ToDictionary(c => c.Id, c => c.Name);

        var trend = resolutionJudged
            .GroupBy(e => TicketReportService.BucketStart(e.CreatedAtUtc, ReportGranularity.Weekly))
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var responseGroup = responseJudged.Where(r => TicketReportService.BucketStart(r.CreatedAtUtc, ReportGranularity.Weekly) == g.Key).ToList();
                return new SlaCompliancePointDto(g.Key, g.Count(), Percentage(responseGroup, e => e.ResponseMet == true), Percentage(g.ToList(), e => e.ResolutionMet == true));
            })
            .ToList();

        return new SlaComplianceReportDto(
            evaluated.Count, inProgressCount, noPolicyCount,
            responseCompliance, resolutionCompliance,
            BuildBreakdown(resolutionJudged, e => e.TeamId is Guid teamId && teamsById.TryGetValue(teamId, out var name) ? name : "Unassigned"),
            BuildBreakdown(resolutionJudged, e => e.AgentId is Guid agentId && agentsById.TryGetValue(agentId, out var name) ? name : "Unassigned"),
            BuildBreakdown(resolutionJudged, e => e.CategoryId is Guid categoryId && categoriesById.TryGetValue(categoryId, out var name) ? name : "Uncategorized"),
            BuildBreakdown(resolutionJudged, e => e.Priority.ToString()),
            trend);
    }

    private async Task<List<TicketSlaEvaluation>> EvaluateTicketsAsync(List<Ticket> tickets, CancellationToken ct)
    {
        var result = new List<TicketSlaEvaluation>();
        var customersById = (await customerRepository.GetAllAsync(ct)).ToDictionary(c => c.Id);

        var openTickets = tickets.Where(t => t.Status is not (TicketStatus.Resolved or TicketStatus.Closed)).ToList();
        var closedTickets = tickets.Where(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed).ToList();

        // Open tickets: literally the same calculation the ticket's own SLA status card uses.
        var openStatuses = await slaCalculationService.GetStatusesAsync(openTickets, ct);
        foreach (var ticket in openTickets)
        {
            if (!openStatuses.TryGetValue(ticket.Id, out var status))
            {
                result.Add(NoPolicy(ticket));
                continue;
            }
            result.Add(new TicketSlaEvaluation(
                ticket.Id, ticket.CreatedAtUtc, ticket.AssignedTeamId, ticket.AssignedAgentId, ticket.CategoryId, ticket.Priority,
                HasPolicy: true,
                ResponseMet: status.IsResponseBreached ? false : null,   // null = not yet due, not yet evaluable
                ResolutionMet: status.IsResolutionBreached ? false : null));
        }

        // Closed tickets: SlaCalculationService can't answer this (it only ever compares against
        // "now") — compute actual elapsed business time to the real response/resolution event.
        if (closedTickets.Count > 0)
        {
            var firstAgentMessageTimes = await messageRepository.GetFirstAgentMessageTimesAsync(closedTickets.Select(t => t.Id).ToList(), ct);
            foreach (var ticket in closedTickets)
            {
                var tier = customersById.TryGetValue(ticket.CustomerId, out var c) ? c.Tier : CustomerTier.Standard;
                var target = await targetService.ResolveAsync(ticket.Priority, ticket.CategoryId, tier, ct);
                if (target is null) { result.Add(NoPolicy(ticket)); continue; }

                var history = (await ticketRepository.GetStatusHistoryAsync(ticket.Id, ct)).OrderBy(h => h.ChangedAtUtc).ToList();
                var resolvedAt = history.FirstOrDefault(h => h.NewStatus is TicketStatus.Resolved or TicketStatus.Closed)?.ChangedAtUtc;

                bool responseMet;
                if (firstAgentMessageTimes.TryGetValue(ticket.Id, out var firstResponseAt))
                {
                    var elapsed = await calendarService.CalculateBusinessMinutesBetweenAsync(ticket.CreatedAtUtc, firstResponseAt, ct);
                    var pause = await PendingBusinessMinutesUpToAsync(history, firstResponseAt, ct);
                    responseMet = (elapsed - pause) <= target.ResponseTargetMinutes;
                }
                else
                {
                    responseMet = false; // resolved with zero agent replies — never met a response target
                }

                bool resolutionMet;
                if (resolvedAt is DateTimeOffset resolvedAtValue)
                {
                    var elapsed = await calendarService.CalculateBusinessMinutesBetweenAsync(ticket.CreatedAtUtc, resolvedAtValue, ct);
                    var pause = await PendingBusinessMinutesUpToAsync(history, resolvedAtValue, ct);
                    resolutionMet = (elapsed - pause) <= target.ResolutionTargetMinutes;
                }
                else
                {
                    resolutionMet = false; // defensive only — unreachable: closedTickets are Resolved/Closed by construction
                }

                result.Add(new TicketSlaEvaluation(ticket.Id, ticket.CreatedAtUtc, ticket.AssignedTeamId, ticket.AssignedAgentId, ticket.CategoryId, ticket.Priority, true, responseMet, resolutionMet));
            }
        }

        return result;
    }

    // Shaped like SlaCalculationService's own private GetPendingBusinessMinutesAsync, but bounded
    // to a historical event instead of "now" — kept as a separate method rather than parameterizing
    // the shipped real-time one, to avoid touching SLA & Automation's already-verified calculation.
    private async Task<int> PendingBusinessMinutesUpToAsync(IReadOnlyList<TicketStatusChangeEntry> orderedHistory, DateTimeOffset asOf, CancellationToken ct)
    {
        var total = 0;
        for (var i = 0; i < orderedHistory.Count; i++)
        {
            if (orderedHistory[i].NewStatus != TicketStatus.Pending) continue;
            var from = orderedHistory[i].ChangedAtUtc;
            if (from >= asOf) continue;
            var to = i + 1 < orderedHistory.Count ? orderedHistory[i + 1].ChangedAtUtc : asOf;
            if (to > asOf) to = asOf;
            total += await calendarService.CalculateBusinessMinutesBetweenAsync(from, to, ct);
        }
        return total;
    }

    private static TicketSlaEvaluation NoPolicy(Ticket t) =>
        new(t.Id, t.CreatedAtUtc, t.AssignedTeamId, t.AssignedAgentId, t.CategoryId, t.Priority, false, null, null);

    private static double Percentage(List<TicketSlaEvaluation> judged, Func<TicketSlaEvaluation, bool> metPredicate) =>
        judged.Count == 0 ? 0 : Math.Round(100.0 * judged.Count(metPredicate) / judged.Count, 1);

    private static List<SlaBreakdownDto> BuildBreakdown(List<TicketSlaEvaluation> resolutionJudged, Func<TicketSlaEvaluation, string> keySelector) =>
        resolutionJudged
            .GroupBy(keySelector)
            .Select(g => new SlaBreakdownDto(g.Key, g.Count(), g.Count(e => e.ResolutionMet == false), Percentage(g.ToList(), e => e.ResolutionMet == true)))
            .OrderBy(b => b.Key)
            .ToList();
}
```

### 4 — Infrastructure: DI

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<SlaComplianceService>();
```

### 5 — Api: `ReportsController` addition

**File: `src/SupportCrm.Api/Controllers/ReportsController.cs`** — inject `SlaComplianceService`, add:

```csharp

    [HttpGet("sla-compliance")]
    public async Task<ActionResult<SlaComplianceReportDto>> GetSlaComplianceReport(
        [FromServices] SlaComplianceService slaComplianceService,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] Guid? teamId,
        [FromQuery] Guid? agentId, [FromQuery] Guid? categoryId, [FromQuery] TicketPriority? priority, CancellationToken ct) =>
        Ok(await slaComplianceService.GetComplianceReportAsync(new SlaComplianceReportQuery(from, to, teamId, agentId, categoryId, priority), ct));
```

---

## Edge Cases & Failure Modes

- **A ticket with no matching `SlaTarget`** — counted in `NoPolicyCount`, excluded from every compliance percentage and breakdown — not silently treated as compliant or breached.
- **An open ticket already past its response due date but not yet its resolution due date** — `ResponseMet = false` (evaluated, breached), `ResolutionMet = null` (still in progress) — the two dimensions are judged independently, exactly as the AC's "percentage of tickets meeting response **and** resolution SLAs" implies they should be.
- **A resolved ticket that never received a single agent message** — `responseMet = false` unconditionally; a ticket can't be credited with meeting a response target it never actually hit.
- **Zero tickets evaluated for a given breakdown key** (e.g., a team with zero resolution-judged tickets this period) — that key simply doesn't appear in the breakdown list, not a zero-row placeholder — matches Customer Portal CP-4's identical convention for its own deflection report.
- **`Percentage` called with an empty list** — returns `0`, not `NaN`/a division-by-zero exception.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Reports/SlaComplianceServiceTests.cs`**:
   - `GetComplianceReportAsync_OpenTicketNotYetDue_IsExcludedFromPercentageButCountedInProgress`
   - `GetComplianceReportAsync_ClosedTicketWithNoAgentReply_ResponseMetIsFalse`
   - `GetComplianceReportAsync_TicketWithNoMatchingSlaTarget_IsExcludedAsNoPolicy`
   - `PendingBusinessMinutesUpToAsync_OnlyCountsPauseTimeBeforeTheAsOfCutoff`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Reconciliation spot-check:** pick one currently-open ticket, compare this report's per-ticket breach flag (via a debug breakpoint or a temporary log) against `GET /api/tickets/{id}/sla-status` — they must be identical, since both now call `SlaCalculationService.GetStatusesAsync`/`GetStatusAsync`.

---

## Done Criteria

- [ ] `GET /api/reports/sla-compliance` returns response/resolution compliance percentages, filterable by date/team/agent/category/priority.
- [ ] Breakdowns by team/agent/category/priority and a weekly trend are present.
- [ ] Open-ticket figures are computed via the exact same `SlaCalculationService` code path individual tickets use.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
