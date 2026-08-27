# Story 16 — Assigned tickets (Story: AD-1)

---

## Prerequisites

- Ticket Management Stories 05–09 completed — `Ticket.AssignedAgentId`/`Priority`/`Status`/`CategoryId`, `Agent`.

---

## Story Goal

1. An endpoint returning the tickets assigned to a given agent, sorted by priority then SLA due-at, filterable by status/priority/category.
2. A derived SLA state (`OnTrack` | `NearingBreach` | `Breached`) per ticket — computed from `Priority` + `CreatedAtUtc`, not stored.
3. A minimal agent-admin surface: extend `Agent` with `IsAvailable`'s frontend-facing sibling — actually `IsAvailable` already exists (CC-3); this story just needs `PUT /api/agents/{id}/availability` so the frontend's new admin list can toggle it (currently only settable via `Agent.SetAvailability`, never exposed over HTTP).

**Not in scope:** authentication, WebSockets/SignalR, SLA policy configuration UI.

---

## Context — Read These Files First

1. `src/SupportCrm.Domain/Entities/Ticket.cs`, `TicketPriority.cs`, `TicketStatus.cs` — the fields this story's SLA/sort logic reads; no changes to these files.
2. `src/SupportCrm.Domain/Entities/Agent.cs` — already has `IsAvailable`/`SetAvailability` (Communication Channels CC-3) with no HTTP endpoint; this story adds one.
3. `src/SupportCrm.Application/Tickets/AgentService.cs`, `src/SupportCrm.Api/Controllers/AgentsController.cs` — extend both, following their existing shape.
4. `src/SupportCrm.Domain/Repositories/ITicketRepository.cs` and `src/SupportCrm.Infrastructure/Persistence/TicketRepository.cs` — add one query method.

---

## Backend Tasks

### 1 — Domain: repository method signature only

**File: `ITicketRepository.cs`** — add:

```csharp
Task<IReadOnlyList<Ticket>> GetAssignedToAgentAsync(Guid agentId, CancellationToken ct);
```

No new entity — SLA state is computed in the Application layer from fields `Ticket` already has.

### 2 — Application: SLA policy, dashboard DTO/service, agent availability endpoint plumbing

**Create file: `src/SupportCrm.Application/Tickets/SlaPolicy.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

/// <summary>
/// SLA due-at is derived, never stored: CreatedAtUtc + a fixed window per priority.
/// "Nearing" = remaining time is at or below 20% of the total window (and not yet
/// breached). Only meaningful for tickets not already Closed — a closed ticket's SLA
/// state is moot, reported as "NotApplicable" rather than a stale Breached/OnTrack.
/// </summary>
public static class SlaPolicy
{
    private static readonly IReadOnlyDictionary<TicketPriority, TimeSpan> Windows = new Dictionary<TicketPriority, TimeSpan>
    {
        [TicketPriority.Urgent] = TimeSpan.FromHours(4),
        [TicketPriority.High] = TimeSpan.FromHours(8),
        [TicketPriority.Medium] = TimeSpan.FromHours(24),
        [TicketPriority.Low] = TimeSpan.FromHours(72)
    };

    public static DateTimeOffset DueAt(TicketPriority priority, DateTimeOffset createdAtUtc) => createdAtUtc + Windows[priority];

    public static string StateFor(TicketPriority priority, TicketStatus status, DateTimeOffset createdAtUtc, DateTimeOffset nowUtc)
    {
        if (status == TicketStatus.Closed) return "NotApplicable";
        var window = Windows[priority];
        var remaining = DueAt(priority, createdAtUtc) - nowUtc;
        if (remaining <= TimeSpan.Zero) return "Breached";
        return remaining <= window * 0.2 ? "NearingBreach" : "OnTrack";
    }
}
```

**Create file: `src/SupportCrm.Application/Tickets/AgentDashboardDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record AgentDashboardTicketDto(
    Guid Id,
    string ReferenceNumber,
    string Subject,
    TicketStatus Status,
    TicketPriority Priority,
    Guid? CategoryId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset SlaDueAtUtc,
    string SlaState); // "OnTrack" | "NearingBreach" | "Breached" | "NotApplicable"

public record SetAgentAvailabilityRequest(bool IsAvailable);
```

**Create file: `src/SupportCrm.Application/Tickets/AgentDashboardService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AgentDashboardService(ITicketRepository ticketRepository, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<AgentDashboardTicketDto>> GetAssignedTicketsAsync(
        Guid agentId, TicketStatus? status, TicketPriority? priority, Guid? categoryId, CancellationToken ct)
    {
        var tickets = await ticketRepository.GetAssignedToAgentAsync(agentId, ct);
        var now = timeProvider.GetUtcNow();

        // Default view is "my workload" — excludes Closed unless the agent explicitly
        // filters for it; an explicit status filter always wins over that default.
        IEnumerable<Ticket> filtered = status.HasValue
            ? tickets.Where(t => t.Status == status.Value)
            : tickets.Where(t => t.Status != TicketStatus.Closed);

        if (priority.HasValue) filtered = filtered.Where(t => t.Priority == priority.Value);
        if (categoryId.HasValue) filtered = filtered.Where(t => t.CategoryId == categoryId.Value);

        return filtered
            .Select(t => new AgentDashboardTicketDto(
                t.Id, t.ReferenceNumber, t.Subject, t.Status, t.Priority, t.CategoryId, t.CreatedAtUtc,
                SlaPolicy.DueAt(t.Priority, t.CreatedAtUtc),
                SlaPolicy.StateFor(t.Priority, t.Status, t.CreatedAtUtc, now)))
            // TicketPriority is declared Low < Medium < High < Urgent, so descending puts
            // the most severe first; SLA due-at ascending breaks ties within a priority.
            .OrderByDescending(d => d.Priority)
            .ThenBy(d => d.SlaDueAtUtc)
            .ToList();
    }
}
```

**File: `AgentService.cs`** — add, alongside the existing `CreateAsync`/`GetAllAsync`:

```csharp
    public async Task SetAvailabilityAsync(Guid agentId, bool isAvailable, CancellationToken ct)
    {
        var agent = await repository.GetByIdAsync(agentId, ct) ?? throw new KeyNotFoundException($"Agent '{agentId}' was not found.");
        agent.SetAvailability(isAvailable);
        await repository.SaveChangesAsync(ct);
    }
```

**File: `AgentDto`** (in `AgentTeamDtos.cs`) — extend with availability so the admin list can render current state without a second call:

```csharp
public record AgentDto(Guid Id, string Name, bool IsAvailable);
```

(Update `AgentService.CreateAsync`'s and `GetAllAsync`'s `AgentDto` construction to pass `agent.IsAvailable`.)

### 3 — Infrastructure: repository implementation, DI

**File: `TicketRepository.cs`** — add:

```csharp
    public async Task<IReadOnlyList<Ticket>> GetAssignedToAgentAsync(Guid agentId, CancellationToken ct) =>
        await dbContext.Tickets.Where(t => t.AssignedAgentId == agentId).ToListAsync(ct);
```

**File: `DependencyInjection.cs`** — add `services.AddScoped<AgentDashboardService>();` near the existing `AgentService`/`TeamService` registrations.

### 4 — Api: controllers

**File: `TicketsController.cs`** — add:

```csharp
    [HttpGet("assigned")]
    public async Task<ActionResult<IReadOnlyList<AgentDashboardTicketDto>>> GetAssigned(
        [FromQuery] Guid agentId, [FromQuery] TicketStatus? status, [FromQuery] TicketPriority? priority, [FromQuery] Guid? categoryId,
        [FromServices] AgentDashboardService dashboardService, CancellationToken ct) =>
        Ok(await dashboardService.GetAssignedTicketsAsync(agentId, status, priority, categoryId, ct));
```

**File: `AgentsController.cs`** — add:

```csharp
    [HttpPut("{id:guid}/availability")]
    public async Task<IActionResult> SetAvailability(Guid id, [FromBody] SetAgentAvailabilityRequest request, CancellationToken ct)
    {
        try { await agentService.SetAvailabilityAsync(id, request.IsAvailable, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
```

---

## Edge Cases & Failure Modes

- **Agent has no assigned tickets** — `GetAssignedToAgentAsync` returns an empty list; the endpoint returns `200 []`, not a 404 (an agent with zero tickets isn't an error).
- **Unknown `agentId` (not a real agent)** — the ticket query simply returns nothing (no `Agent` existence check needed here, since it's a pure filter, not a mutation); the frontend's "Acting as" switcher only ever offers real agent ids anyway.
- **Ticket priority changes after creation** (Ticket Management TM-2's `SetPriority`) — SLA state recalculates correctly on every read since it's derived from the ticket's *current* `Priority`, not the priority at creation time. This is intentional: an escalated-to-Urgent ticket should immediately reflect a tighter SLA window.
- **A ticket assigned to a team, not an agent** (`AssignedTeamId` set, `AssignedAgentId` null) — never appears on any agent's dashboard; that's correct, this story is explicitly "assigned to *me*."

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/SlaPolicyTests.cs`**:
   - `StateFor_WellWithinWindow_ReturnsOnTrack`
   - `StateFor_WithinTwentyPercentOfWindow_ReturnsNearingBreach`
   - `StateFor_PastDueAt_ReturnsBreached`
   - `StateFor_ClosedTicket_ReturnsNotApplicable`
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/AgentDashboardServiceTests.cs`**:
   - `GetAssignedTicketsAsync_DefaultFilter_ExcludesClosed`
   - `GetAssignedTicketsAsync_ExplicitClosedFilter_IncludesClosed`
   - `GetAssignedTicketsAsync_SortsByPriorityThenSlaDueAt`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Manual smoke:** assign two tickets of different priorities to one agent, call `GET /api/tickets/assigned?agentId=...`, confirm order and `slaState`.

---

## Done Criteria

- [ ] `GET /api/tickets/assigned` returns the agent's tickets, sorted priority-then-SLA, filterable by status/priority/category.
- [ ] Each ticket exposes a computed `slaDueAtUtc`/`slaState`.
- [ ] `PUT /api/agents/{id}/availability` toggles `Agent.IsAvailable`.
- [ ] `dotnet build SupportCrm.slnx` succeeds. No migration needed (no new columns/tables).
