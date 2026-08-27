# Story 07 — Assign tickets to agents (Story: TM-3)

---

## Prerequisites

- Story 05 completed: [`05-story-TM-1.md`](05-story-TM-1.md) — provides the `Ticket` aggregate.

---

## Story Goal

1. A ticket can be manually assigned to exactly one `Agent` **or** one `Team` (mutually exclusive).
2. Reassignment is possible; both the previous and new assignee are notified via a stub/no-op seam (no real notification channel exists in this codebase — same documented gap as Customer Management's CM-2 preferred-channel AC).
3. A dashboard query returns current open-ticket load per agent, to support balanced assignment.
4. Unassigned tickets are queryable/flaggable so none are missed.

**Assumption (no identity/user-management system exists yet):** `Agent` and `Team` are modeled as lightweight reference entities (id + name) scoped to this feature, not real authenticated users — the same kind of stand-in Customer Management used for `ChangedBy`. Flag this explicitly; a real identity system would replace these with actual user/team records later.

**Not in scope:** real notification delivery; automatic/rules-based assignment (round-robin, skill routing) — this story is manual assignment only.

---

## Context — Read These Files First

1. [`05-story-TM-1.md`](05-story-TM-1.md), `## Backend Tasks` → `### 1` — the `Ticket` entity this story adds assignment fields to, and the private-setter/validating-constructor pattern for the new `Agent`/`Team` entities.
2. `../customer-management/01-story-CM-1.md`, `## Backend Tasks` → `### 2` (`ICustomerActivitySummaryProvider`/`StubCustomerActivitySummaryProvider`) — the seam-with-stub-implementation pattern this story's `IAssignmentNotifier`/`NoOpAssignmentNotifier` follows.
3. `src/SupportCrm.Application/Tickets/TicketService.cs` (from TM-1, extended by TM-2) — add assignment methods to a **new** `TicketAssignmentService` rather than growing this file further, since assignment is a distinct enough concern (its own notification seam, its own dashboard query) to warrant a separate service — matching Customer Management's split between `CustomerService` and `ContactDetailService`/`CustomerProfileService`.
4. `src/SupportCrm.Domain/Repositories/ITicketRepository.cs` (from TM-1, extended by TM-2) — add assignment-related query members here in the same style.

---

## Backend Tasks

### 1 — Domain: `Agent`, `Team`, assignment fields + audit trail

**Create file: `src/SupportCrm.Domain/Entities/Agent.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// Stand-in for a real user/identity record — no authentication/user-management system
// exists yet in this codebase. Replace with a real user reference once one does.
public class Agent
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;

    private Agent() { } // EF Core

    public Agent(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Agent name is required.", nameof(name));
        Id = Guid.NewGuid();
        Name = name;
    }
}
```

**Create file: `src/SupportCrm.Domain/Entities/Team.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class Team
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;

    private Team() { } // EF Core

    public Team(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Team name is required.", nameof(name));
        Id = Guid.NewGuid();
        Name = name;
    }
}
```

**Create file: `src/SupportCrm.Domain/Entities/TicketAssignmentChangeEntry.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class TicketAssignmentChangeEntry
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid? OldAgentId { get; private set; }
    public Guid? NewAgentId { get; private set; }
    public Guid? OldTeamId { get; private set; }
    public Guid? NewTeamId { get; private set; }
    public string ChangedBy { get; private set; } = default!;
    public DateTimeOffset ChangedAtUtc { get; private set; }

    private TicketAssignmentChangeEntry() { } // EF Core

    public TicketAssignmentChangeEntry(Guid ticketId, Guid? oldAgentId, Guid? newAgentId, Guid? oldTeamId, Guid? newTeamId, string changedBy, DateTimeOffset changedAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        OldAgentId = oldAgentId;
        NewAgentId = newAgentId;
        OldTeamId = oldTeamId;
        NewTeamId = newTeamId;
        ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        ChangedAtUtc = changedAtUtc;
    }
}
```

**File: `src/SupportCrm.Domain/Entities/Ticket.cs`** — add properties:

```csharp
    public Guid? AssignedAgentId { get; private set; }
    public Guid? AssignedTeamId { get; private set; }
```

and a method that enforces the mutual-exclusivity rule from the AC ("one agent **or** one team"):

```csharp
    public void AssignTo(Guid? agentId, Guid? teamId)
    {
        if (agentId is not null && teamId is not null)
            throw new InvalidOperationException("A ticket can be assigned to an agent or a team, not both.");
        AssignedAgentId = agentId;
        AssignedTeamId = teamId;
    }
```

**Create file: `src/SupportCrm.Domain/Repositories/IAgentRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Agent>> GetAllAsync(CancellationToken ct);
    Task AddAsync(Agent agent, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ITeamRepository.cs`** — identical shape, for `Team`.

**Extend file: `src/SupportCrm.Domain/Repositories/ITicketRepository.cs`** — add:

```csharp
    Task<IReadOnlyList<Ticket>> GetUnassignedAsync(CancellationToken ct);
    Task<IReadOnlyDictionary<Guid, int>> CountOpenGroupedByAgentAsync(CancellationToken ct);
    Task AddAssignmentChangeAsync(TicketAssignmentChangeEntry entry, CancellationToken ct);
```

### 2 — Application: notifier seam, DTOs, `TicketAssignmentService`

**Create file: `src/SupportCrm.Application/Tickets/IAssignmentNotifier.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

/// <summary>
/// Notifies the previous and new assignee of a reassignment. No real notification
/// channel (email/push/SMS) exists in this codebase yet — register
/// <see cref="NoOpAssignmentNotifier"/> until one does.
/// </summary>
public interface IAssignmentNotifier
{
    Task NotifyReassignedAsync(Guid ticketId, Guid? previousAgentId, Guid? previousTeamId, Guid? newAgentId, Guid? newTeamId, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Tickets/NoOpAssignmentNotifier.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

public class NoOpAssignmentNotifier : IAssignmentNotifier
{
    public Task NotifyReassignedAsync(Guid ticketId, Guid? previousAgentId, Guid? previousTeamId, Guid? newAgentId, Guid? newTeamId, CancellationToken ct)
        => Task.CompletedTask;
}
```

**Create file: `src/SupportCrm.Application/Tickets/AgentTeamDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

public record CreateAgentRequest(string Name);
public record AgentDto(Guid Id, string Name);
public record CreateTeamRequest(string Name);
public record TeamDto(Guid Id, string Name);
public record AgentLoadDto(Guid AgentId, string AgentName, int OpenTicketCount);
public record AssignTicketRequest(Guid? AgentId, Guid? TeamId, string ChangedBy);
```

**Create file: `src/SupportCrm.Application/Tickets/AgentService.cs`** and **`TeamService.cs`** — minimal CRUD (`CreateAsync`, `GetAllAsync`), mirroring `TicketCategoryService`'s shape from TM-2.

**Create file: `src/SupportCrm.Application/Tickets/TicketAssignmentService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketAssignmentService(
    ITicketRepository ticketRepository,
    IAgentRepository agentRepository,
    IAssignmentNotifier notifier,
    TimeProvider timeProvider)
{
    public async Task AssignAsync(Guid ticketId, AssignTicketRequest request, CancellationToken ct)
    {
        if (request.AgentId is not null && request.TeamId is not null)
            throw new ArgumentException("Assign to an agent or a team, not both.", nameof(request));

        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var previousAgentId = ticket.AssignedAgentId;
        var previousTeamId = ticket.AssignedTeamId;

        ticket.AssignTo(request.AgentId, request.TeamId);

        await ticketRepository.AddAssignmentChangeAsync(
            new TicketAssignmentChangeEntry(ticketId, previousAgentId, request.AgentId, previousTeamId, request.TeamId, request.ChangedBy, timeProvider.GetUtcNow()), ct);
        await ticketRepository.SaveChangesAsync(ct);

        await notifier.NotifyReassignedAsync(ticketId, previousAgentId, previousTeamId, request.AgentId, request.TeamId, ct);
    }

    public async Task<IReadOnlyList<AgentLoadDto>> GetAgentLoadAsync(CancellationToken ct)
    {
        var agents = await agentRepository.GetAllAsync(ct);
        var loadByAgent = await ticketRepository.CountOpenGroupedByAgentAsync(ct);

        return agents
            .Select(a => new AgentLoadDto(a.Id, a.Name, loadByAgent.GetValueOrDefault(a.Id, 0)))
            .ToList();
    }

    public async Task<IReadOnlyList<TicketDto>> GetUnassignedAsync(CancellationToken ct)
    {
        var tickets = await ticketRepository.GetUnassignedAsync(ct);
        return tickets.Select(t => new TicketDto(t.Id, t.ReferenceNumber, t.CustomerId, t.Channel, t.Subject, t.Description, t.Status, t.CreatedAtUtc, t.ClosedAtUtc)).ToList();
    }
}
```

### 3 — Infrastructure: EF config, repositories, DI

**File: `SupportCrmDbContext.cs`** — add `DbSet<Agent>`, `DbSet<Team>`, `DbSet<TicketAssignmentChangeEntry>` + `OnModelCreating` blocks (same style); extend the `Ticket` block with `entity.HasIndex(t => t.AssignedAgentId); entity.HasIndex(t => t.AssignedTeamId);`.

**Create files: `AgentRepository.cs`, `TeamRepository.cs`** — straightforward EF implementations mirroring `CustomerRepository`.

**File: `TicketRepository.cs`** — implement the 3 new members:

```csharp
    public async Task<IReadOnlyList<Ticket>> GetUnassignedAsync(CancellationToken ct) =>
        await dbContext.Tickets.Where(t => t.AssignedAgentId == null && t.AssignedTeamId == null).ToListAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, int>> CountOpenGroupedByAgentAsync(CancellationToken ct) =>
        await dbContext.Tickets
            .Where(t => t.AssignedAgentId != null && OpenStatuses.Contains(t.Status))
            .GroupBy(t => t.AssignedAgentId!.Value)
            .ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

    public Task AddAssignmentChangeAsync(TicketAssignmentChangeEntry entry, CancellationToken ct)
    {
        dbContext.TicketAssignmentChangeEntries.Add(entry);
        return Task.CompletedTask;
    }
```

**File: `DependencyInjection.cs`** — add registrations for `IAgentRepository`/`AgentRepository`, `ITeamRepository`/`TeamRepository`, `AgentService`, `TeamService`, `TicketAssignmentService`, and `IAssignmentNotifier → NoOpAssignmentNotifier`.

### 4 — Api: controllers

**Create files: `AgentsController.cs`, `TeamsController.cs`** — `[Route("api/agents")]`/`[Route("api/teams")]`, `GET` (list), `POST` (create), mirroring `TicketCategoriesController`.

**File: `TicketsController.cs`** — add:

```csharp
    [HttpPut("{id:guid}/assignment")]
    public async Task<IActionResult> Assign(Guid id, [FromBody] AssignTicketRequest request, [FromServices] TicketAssignmentService assignmentService, CancellationToken ct)
    {
        try { await assignmentService.AssignAsync(id, request, ct); return NoContent(); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("unassigned")]
    public async Task<ActionResult<IReadOnlyList<TicketDto>>> GetUnassigned([FromServices] TicketAssignmentService assignmentService, CancellationToken ct) =>
        Ok(await assignmentService.GetUnassignedAsync(ct));

    [HttpGet("agent-load")]
    public async Task<ActionResult<IReadOnlyList<AgentLoadDto>>> GetAgentLoad([FromServices] TicketAssignmentService assignmentService, CancellationToken ct) =>
        Ok(await assignmentService.GetAgentLoadAsync(ct));
```

- After creating these files, run `dotnet ef migrations add AddTicketAssignment --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

---

## Edge Cases & Failure Modes

- **Assigning to both an agent and a team in the same request** — rejected with `400` at the service layer (`ArgumentException`) *and* enforced again in `Ticket.AssignTo` (`InvalidOperationException`) as a domain invariant — defense in depth, not redundant, since the domain method must be safe to call from any future caller.
- **Reassigning an already-unassigned ticket** — `previousAgentId`/`previousTeamId` are both `null`; `NotifyReassignedAsync` is still called with `null` "previous" values — the no-op stub handles this trivially, but a real notifier implementation must not crash when there's no previous assignee to notify.
- **Agent load for an agent with zero open tickets** — `GetValueOrDefault(a.Id, 0)` returns `0`, not an exception or missing row — every agent appears in the load list.
- **Unknown ticket id on assignment** — `TicketNotFoundException` → `404`.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/TicketTests.cs`**:
   - `AssignTo_BothAgentAndTeam_Throws`
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketAssignmentServiceTests.cs`**:
   - `AssignAsync_WritesAssignmentChangeEntryAndCallsNotifier`
   - `GetAgentLoadAsync_IncludesAgentsWithZeroOpenTickets`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/TicketsControllerAssignmentTests.cs`**:
   - `Get_Unassigned_ReturnsOnlyTicketsWithNoAssignee`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddTicketAssignment --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] A ticket can be assigned to one agent or one team (`PUT /api/tickets/{id}/assignment`).
- [ ] Reassignment works and calls the notifier seam for both previous and new assignee.
- [ ] Agent load is queryable (`GET /api/tickets/agent-load`).
- [ ] Unassigned tickets are queryable (`GET /api/tickets/unassigned`).
- [ ] `dotnet build SupportCrm.slnx` succeeds.
