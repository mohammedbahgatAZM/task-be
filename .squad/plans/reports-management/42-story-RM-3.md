# Story 42 — Agent performance (Story: RM-3)

---

## Prerequisites

- Story 40 completed: [`40-story-RM-1.md`](40-story-RM-1.md) — `Reports` bounded concern, `ITicketRepository.GetAllAsync`, `ITicketMessageRepository.GetFirstAgentMessageTimesAsync` (Story 41).
- Customer Portal Story 39 completed — `TicketFeedback`, the only CSAT data source.

---

## Story Goal

1. `GET /api/reports/agent-performance` — per-agent tickets-resolved count, average response/resolution time, average CSAT, filterable by date range and (optionally) a single agent.
2. `ReassignedAwayCount` — a transparent, separately-reported count of tickets an agent worked on that resolved under someone else, per the AC's "exclude… or clearly flag them."
3. No new endpoint for "agents view their own metrics" — the same endpoint, called with the logged-in agent's own id.

---

## Context — Read These Files First

1. `src/SupportCrm.Domain/Entities/TicketAssignmentChangeEntry.cs` (all ~27 lines) — `OldAgentId`/`NewAgentId`, the fields `ReassignedAwayCount` is built from.
2. `src/SupportCrm.Application/Reports/SlaComplianceService.cs` (Story 41) — precedent for splitting a metric into "currently owns it" vs. "used to be involved" rather than inventing a third bookkeeping table.
3. `src/SupportCrm.Application/CustomerPortal/TicketFeedbackService.cs` — `TicketFeedback` shape, reused read-only here via a new `GetAllAsync` on its repository.

---

## Backend Tasks

### 1 — `ITicketFeedbackRepository.GetAllAsync`

**File: `src/SupportCrm.Domain/Repositories/ITicketFeedbackRepository.cs`** — add:

```csharp
    Task<IReadOnlyList<TicketFeedback>> GetAllAsync(CancellationToken ct);
```

**File: `src/SupportCrm.Infrastructure/Persistence/TicketFeedbackRepository.cs`** — add:

```csharp
    public async Task<IReadOnlyList<TicketFeedback>> GetAllAsync(CancellationToken ct) =>
        await dbContext.TicketFeedback.ToListAsync(ct);
```

### 2 — DTOs

**File: `src/SupportCrm.Application/Reports/ReportDtos.cs`** — append:

```csharp
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
```

### 3 — `AgentPerformanceService`

**Create file: `src/SupportCrm.Application/Reports/AgentPerformanceService.cs`**

```csharp
namespace SupportCrm.Application.Reports;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AgentPerformanceService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ITicketFeedbackRepository feedbackRepository,
    IAgentRepository agentRepository)
{
    public async Task<IReadOnlyList<AgentPerformanceDto>> GetPerformanceAsync(AgentPerformanceQuery query, CancellationToken ct)
    {
        var agents = await agentRepository.GetAllAsync(ct);
        if (query.AgentId is not null) agents = agents.Where(a => a.Id == query.AgentId).ToList();

        var allTickets = await ticketRepository.GetAllAsync(ct);
        var closedTickets = allTickets.Where(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed).ToList();

        // "Over selected time periods" means when the work happened (resolution), not when the
        // ticket arrived — the date filter is applied against each ticket's resolved-at time.
        var resolvedAtByTicket = await GetResolvedAtTimesAsync(closedTickets.Select(t => t.Id).ToList(), ct);
        IEnumerable<Ticket> resolvedInRange = closedTickets.Where(t => resolvedAtByTicket.ContainsKey(t.Id));
        if (query.From is not null) resolvedInRange = resolvedInRange.Where(t => resolvedAtByTicket[t.Id] >= query.From);
        if (query.To is not null) resolvedInRange = resolvedInRange.Where(t => resolvedAtByTicket[t.Id] <= query.To);
        var resolvedList = resolvedInRange.ToList();

        var firstAgentMessageTimes = await messageRepository.GetFirstAgentMessageTimesAsync(resolvedList.Select(t => t.Id).ToList(), ct);
        var feedbackByTicket = (await feedbackRepository.GetAllAsync(ct)).ToDictionary(f => f.TicketId);

        var result = new List<AgentPerformanceDto>();
        foreach (var agent in agents)
        {
            // "Tickets handled" = currently assigned to this agent — a ticket reassigned away
            // before resolution is simply no longer in this set, satisfying the AC's exclusion
            // rule with no extra bookkeeping (see ReassignedAwayCount below for the flip side).
            var ownedResolved = resolvedList.Where(t => t.AssignedAgentId == agent.Id).ToList();

            var responseMinutes = ownedResolved
                .Where(t => firstAgentMessageTimes.ContainsKey(t.Id))
                .Select(t => (firstAgentMessageTimes[t.Id] - t.CreatedAtUtc).TotalMinutes)
                .ToList();

            var resolutionMinutes = ownedResolved
                .Select(t => (resolvedAtByTicket[t.Id] - t.CreatedAtUtc).TotalMinutes)
                .ToList();

            var ratings = ownedResolved
                .Where(t => feedbackByTicket.ContainsKey(t.Id))
                .Select(t => feedbackByTicket[t.Id].Rating)
                .ToList();

            var reassignedAwayCount = await CountReassignedAwayAsync(agent.Id, resolvedList, ct);

            result.Add(new AgentPerformanceDto(
                agent.Id, agent.Name, ownedResolved.Count,
                responseMinutes.Count > 0 ? Math.Round(responseMinutes.Average(), 1) : null,
                resolutionMinutes.Count > 0 ? Math.Round(resolutionMinutes.Average(), 1) : null,
                ratings.Count > 0 ? Math.Round(ratings.Average(), 2) : null,
                ratings.Count,
                reassignedAwayCount));
        }

        return result.OrderByDescending(a => a.TicketsResolvedCount).ToList();
    }

    // Flagged N+1 (one history query per resolved ticket) — acceptable at this app's demo scale,
    // same standard already used throughout this codebase (e.g. Customer Portal CP-2's per-ticket
    // status-history lookup).
    private async Task<Dictionary<Guid, DateTimeOffset>> GetResolvedAtTimesAsync(IReadOnlyList<Guid> ticketIds, CancellationToken ct)
    {
        var result = new Dictionary<Guid, DateTimeOffset>();
        foreach (var ticketId in ticketIds)
        {
            var history = await ticketRepository.GetStatusHistoryAsync(ticketId, ct);
            var resolvedAt = history
                .Where(h => h.NewStatus is TicketStatus.Resolved or TicketStatus.Closed)
                .OrderBy(h => h.ChangedAtUtc)
                .Select(h => h.ChangedAtUtc)
                .Cast<DateTimeOffset?>()
                .FirstOrDefault();
            if (resolvedAt is DateTimeOffset value) result[ticketId] = value;
        }
        return result;
    }

    // "Tickets you worked on that finished under someone else" — transparency, not a metric
    // correction; never folded into TicketsResolvedCount. Known limitation, flagged not fixed:
    // a ticket that ping-pongs A → B → A and resolves under A credits A fully even if B also
    // worked it — this codebase has no per-agent time-in-assignment tracking to split that fairly.
    private async Task<int> CountReassignedAwayAsync(Guid agentId, IReadOnlyList<Ticket> resolvedTicketsInRange, CancellationToken ct)
    {
        var count = 0;
        foreach (var ticket in resolvedTicketsInRange)
        {
            if (ticket.AssignedAgentId == agentId) continue; // currently theirs — already in TicketsResolvedCount, not "away"
            var history = await ticketRepository.GetAssignmentHistoryAsync(ticket.Id, ct);
            if (history.Any(h => h.OldAgentId == agentId || h.NewAgentId == agentId)) count++;
        }
        return count;
    }
}
```

### 4 — Infrastructure: DI

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<AgentPerformanceService>();
```

### 5 — Api: `ReportsController` addition

**File: `src/SupportCrm.Api/Controllers/ReportsController.cs`** — inject `AgentPerformanceService`, add:

```csharp

    [HttpGet("agent-performance")]
    public async Task<ActionResult<IReadOnlyList<AgentPerformanceDto>>> GetAgentPerformance(
        [FromServices] AgentPerformanceService agentPerformanceService,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] Guid? agentId, CancellationToken ct) =>
        Ok(await agentPerformanceService.GetPerformanceAsync(new AgentPerformanceQuery(from, to, agentId), ct));
```

---

## Edge Cases & Failure Modes

- **An agent with zero resolved tickets in range** — still appears in the result with `TicketsResolvedCount = 0` and every average as `null` (not `0`, not omitted) — a manager comparing agents needs to see who did nothing this period, not have them silently vanish from the list.
- **A resolved ticket with no `TicketFeedback` row** — simply excluded from that agent's `AverageCsatRating`/`CsatResponseCount`, not treated as a `0` rating (an unrated ticket is not the same as a badly-rated one).
- **A ticket reassigned A → B → A within the range, resolved under A** — counted fully in A's `TicketsResolvedCount`; also (unavoidably, given the loop's `continue` on `AssignedAgentId == agentId`) NOT counted in A's own `ReassignedAwayCount` — correct, since it isn't "away" from A, it's currently A's. B's `ReassignedAwayCount` does include it, which is the intended flag.
- **`agentId` filter naming a nonexistent agent** — the pre-filter on `agents` yields an empty list, and the endpoint returns `[]`, not a `404` — matches this codebase's established "an empty result is not an error" convention for list endpoints.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Reports/AgentPerformanceServiceTests.cs`**:
   - `GetPerformanceAsync_TicketReassignedAway_ExcludedFromNewOwnersMetrics_CountedInOldOwnersReassignedAway`
   - `GetPerformanceAsync_AgentWithNoResolvedTickets_ReturnsZeroCountAndNullAverages`
   - `GetPerformanceAsync_DateFilterUsesResolutionTimeNotCreationTime`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.

---

## Done Criteria

- [ ] `GET /api/reports/agent-performance` returns tickets-resolved/response/resolution/CSAT per agent, filterable by date and single-agent.
- [ ] Reassigned-away tickets are excluded from the new metrics and separately flagged via `ReassignedAwayCount`.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
