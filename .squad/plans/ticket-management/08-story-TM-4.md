# Story 08 — Status and escalation (Story: TM-4)

---

## Prerequisites

- Story 05 completed: [`05-story-TM-1.md`](05-story-TM-1.md) — provides `Ticket`, `TicketStatus` (already the full New/Open/Pending/Resolved/Closed vocabulary), and `TicketService.RecordStatusChangeAsync` (the internal building block this story exposes publicly).
- Story 07 completed: [`07-story-TM-3.md`](07-story-TM-3.md) — provides `Agent`/`Team` and `TicketAssignmentService.AssignAsync`, which this story's escalation action reuses rather than duplicating reassignment logic.

---

## Story Goal

1. Expose a public endpoint to change a ticket's status among the vocabulary TM-1 already defined (`New`/`Open`/`Pending`/`Resolved`/`Closed`).
2. A one-action "escalate" endpoint that reassigns the ticket to a supervisor (`Agent`) or specialist team (`Team`) — reusing TM-3's `TicketAssignmentService` — and requires a reason/comment, recorded as its own auditable `TicketEscalationEntry` (distinct from a plain reassignment, so TM-5's history can label it as an escalation specifically).
3. An optional customer notification on status change, gated by a per-request flag — routed through a stub/no-op seam, since no real notification channel exists in this codebase (same documented gap as TM-3's `IAssignmentNotifier` and Customer Management's CM-2 preferred-channel AC).

**Not in scope:** real notification delivery; SLA timers or automatic escalation rules — this is a manual, one-action escalation only.

---

## Context — Read These Files First

1. [`05-story-TM-1.md`](05-story-TM-1.md), `## Backend Tasks` → `### 2` — `TicketService.RecordStatusChangeAsync` already exists and does exactly what a status-change endpoint needs; this story adds the public endpoint + request DTO, it does not reimplement the write path.
2. [`07-story-TM-3.md`](07-story-TM-3.md), `## Backend Tasks` → `### 2` — `IAssignmentNotifier`/`NoOpAssignmentNotifier` seam pattern; this story's `ICustomerStatusNotifier`/`NoOpCustomerStatusNotifier` follows the identical shape.
3. `src/SupportCrm.Application/Tickets/TicketAssignmentService.cs` (from TM-3) — `AssignAsync`'s signature; the new `TicketEscalationService` calls this directly for the reassignment half of escalation, then adds its own `TicketEscalationEntry` on top — do not copy/duplicate the assignment-writing logic.
4. `src/SupportCrm.Domain/Entities/TicketStatusChangeEntry.cs` (from TM-1) — already carries a `Reason` field (nullable) — status changes made via this story's new endpoint can populate it directly; no new audit entity is needed for plain status changes, only for escalation specifically (see below).

---

## Backend Tasks

### 1 — Domain: `TicketEscalationEntry`, `Ticket.LastEscalatedAtUtc`

**Create file: `src/SupportCrm.Domain/Entities/TicketEscalationEntry.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class TicketEscalationEntry
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid? EscalatedToAgentId { get; private set; }
    public Guid? EscalatedToTeamId { get; private set; }
    public string Reason { get; private set; } = default!;
    public string EscalatedBy { get; private set; } = default!;
    public DateTimeOffset EscalatedAtUtc { get; private set; }

    private TicketEscalationEntry() { } // EF Core

    public TicketEscalationEntry(Guid ticketId, Guid? escalatedToAgentId, Guid? escalatedToTeamId, string reason, string escalatedBy, DateTimeOffset escalatedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to escalate a ticket.", nameof(reason));
        if (escalatedToAgentId is null && escalatedToTeamId is null)
            throw new ArgumentException("Escalation must target an agent or a team.", nameof(escalatedToAgentId));

        Id = Guid.NewGuid();
        TicketId = ticketId;
        EscalatedToAgentId = escalatedToAgentId;
        EscalatedToTeamId = escalatedToTeamId;
        Reason = reason;
        EscalatedBy = string.IsNullOrWhiteSpace(escalatedBy) ? "unknown" : escalatedBy;
        EscalatedAtUtc = escalatedAtUtc;
    }
}
```

**File: `src/SupportCrm.Domain/Entities/Ticket.cs`** — add one property (for quick "is this currently escalated" dashboard flagging without a join) and a setter method:

```csharp
    public DateTimeOffset? LastEscalatedAtUtc { get; private set; }
```

```csharp
    public void MarkEscalated(DateTimeOffset atUtc) => LastEscalatedAtUtc = atUtc;
```

**Extend file: `src/SupportCrm.Domain/Repositories/ITicketRepository.cs`** — add:

```csharp
    Task AddEscalationAsync(TicketEscalationEntry entry, CancellationToken ct);
    Task<IReadOnlyList<TicketEscalationEntry>> GetEscalationsAsync(Guid ticketId, CancellationToken ct);
```

### 2 — Application: status-change DTO, customer-notifier seam, `TicketEscalationService`

**File: `src/SupportCrm.Application/Tickets/TicketDtos.cs`** — add:

```csharp
public record SetTicketStatusRequest(TicketStatus NewStatus, string ChangedBy, string? Reason, bool NotifyCustomer);
public record EscalateTicketRequest(Guid? EscalateToAgentId, Guid? EscalateToTeamId, string Reason, string ChangedBy);
public record TicketEscalationDto(Guid Id, Guid? EscalatedToAgentId, Guid? EscalatedToTeamId, string Reason, string EscalatedBy, DateTimeOffset EscalatedAtUtc);
```

**Create file: `src/SupportCrm.Application/Tickets/ICustomerStatusNotifier.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

/// <summary>
/// Notifies a ticket's customer that its status changed, when the caller opts in via
/// `NotifyCustomer` on the status-change request. No real notification channel exists
/// in this codebase yet — register <see cref="NoOpCustomerStatusNotifier"/> until one does.
/// </summary>
public interface ICustomerStatusNotifier
{
    Task NotifyStatusChangedAsync(Guid ticketId, TicketStatus newStatus, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Tickets/NoOpCustomerStatusNotifier.cs`** — mirrors `NoOpAssignmentNotifier` (TM-3): a single method returning `Task.CompletedTask`.

**File: `src/SupportCrm.Application/Tickets/TicketService.cs`** — add a public wrapper around the existing internal method:

```csharp
    public async Task SetStatusAsync(Guid ticketId, SetTicketStatusRequest request, CancellationToken ct)
    {
        await RecordStatusChangeAsync(ticketId, request.NewStatus, request.ChangedBy, "Agent", request.Reason, ct);
        if (request.NotifyCustomer)
            await customerStatusNotifier.NotifyStatusChangedAsync(ticketId, request.NewStatus, ct);
    }
```

(add `ICustomerStatusNotifier customerStatusNotifier` to `TicketService`'s primary-constructor parameter list.)

**Create file: `src/SupportCrm.Application/Tickets/TicketEscalationService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketEscalationService(
    ITicketRepository ticketRepository,
    TicketAssignmentService assignmentService,
    TimeProvider timeProvider)
{
    public async Task EscalateAsync(Guid ticketId, EscalateTicketRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var now = timeProvider.GetUtcNow();

        // Reuses TM-3's assignment write path — an escalation IS a reassignment, plus a reason.
        await assignmentService.AssignAsync(ticketId,
            new AssignTicketRequest(request.EscalateToAgentId, request.EscalateToTeamId, request.ChangedBy), ct);

        var entry = new TicketEscalationEntry(ticketId, request.EscalateToAgentId, request.EscalateToTeamId, request.Reason, request.ChangedBy, now);
        await ticketRepository.AddEscalationAsync(entry, ct);

        ticket.MarkEscalated(now);
        await ticketRepository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TicketEscalationDto>> GetEscalationsAsync(Guid ticketId, CancellationToken ct) =>
        (await ticketRepository.GetEscalationsAsync(ticketId, ct))
            .OrderByDescending(e => e.EscalatedAtUtc)
            .Select(e => new TicketEscalationDto(e.Id, e.EscalatedToAgentId, e.EscalatedToTeamId, e.Reason, e.EscalatedBy, e.EscalatedAtUtc))
            .ToList();
}
```

**Design note for the executor:** `EscalateAsync` calls `ticketRepository.GetByIdAsync` and `assignmentService.AssignAsync` (which internally also loads the ticket) — two loads of the same row within one request. This is a deliberate simplification, not an oversight: keeping `TicketAssignmentService.AssignAsync` self-contained (it doesn't need a pre-loaded `Ticket` passed in) is worth the minor redundant query, since it keeps the two services independently testable and avoids a save-then-reload race between them. Flag it in review if the double-load is a concern; consolidating it is a safe follow-up, not a correctness issue as written (both loads happen inside the same unit-of-work `DbContext`, so `MarkEscalated` on the `ticket` reference here still applies to the same tracked entity `AssignAsync` mutated).

### 3 — Infrastructure: EF config, repository, DI

**File: `SupportCrmDbContext.cs`** — add `DbSet<TicketEscalationEntry>` + an `OnModelCreating` block (same style as `TicketAssignmentChangeEntry`'s, from TM-3); extend the `Ticket` block with nothing new required (`LastEscalatedAtUtc` needs no special configuration, EF maps a nullable `DateTimeOffset?` by convention).

**File: `TicketRepository.cs`** — implement the 2 new members:

```csharp
    public Task AddEscalationAsync(TicketEscalationEntry entry, CancellationToken ct)
    {
        dbContext.TicketEscalationEntries.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TicketEscalationEntry>> GetEscalationsAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketEscalationEntries.Where(e => e.TicketId == ticketId).ToListAsync(ct);
```

**File: `DependencyInjection.cs`** — add `ICustomerStatusNotifier → NoOpCustomerStatusNotifier` and `TicketEscalationService` registrations.

### 4 — Api: controller additions

**File: `TicketsController.cs`** — add:

```csharp
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromBody] SetTicketStatusRequest request, CancellationToken ct)
    {
        try { await ticketService.SetStatusAsync(id, request, ct); return NoContent(); }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid id, [FromBody] EscalateTicketRequest request, [FromServices] TicketEscalationService escalationService, CancellationToken ct)
    {
        try { await escalationService.EscalateAsync(id, request, ct); return NoContent(); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:guid}/escalations")]
    public async Task<ActionResult<IReadOnlyList<TicketEscalationDto>>> GetEscalations(Guid id, [FromServices] TicketEscalationService escalationService, CancellationToken ct) =>
        Ok(await escalationService.GetEscalationsAsync(id, ct));
```

- After creating these files, run `dotnet ef migrations add AddTicketEscalation --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

---

## Edge Cases & Failure Modes

- **Escalating with a blank reason** — `TicketEscalationEntry`'s constructor throws `ArgumentException` → `400`, enforced at the domain layer so no caller can bypass it.
- **Escalating without specifying an agent or a team** — same constructor throws — the AC requires escalating *to* a supervisor or specialist team, so at least one target is mandatory (stricter than plain assignment, which allows clearing both).
- **Escalating to both an agent and a team** — rejected by `TicketAssignmentService.AssignAsync`'s existing check (TM-3), reused here rather than re-validated.
- **`NotifyCustomer: true` on a ticket whose customer has no contact details yet** — `NoOpCustomerStatusNotifier` doesn't care (it's a no-op); a real implementation later would need to handle "no contact channel available" itself — flagged for that future story, not this one.
- **Unknown ticket id** — `TicketNotFoundException` → `404` on all three new endpoints.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/TicketEscalationEntryTests.cs`**:
   - `Constructor_BlankReason_Throws`
   - `Constructor_NoAgentOrTeam_Throws`
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketEscalationServiceTests.cs`**:
   - `EscalateAsync_ReassignsAndRecordsEscalationEntry`
   - `EscalateAsync_SetsLastEscalatedAtUtc`
3. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketServiceStatusTests.cs`**:
   - `SetStatusAsync_WithNotifyCustomerTrue_CallsNotifier`
   - `SetStatusAsync_WithNotifyCustomerFalse_DoesNotCallNotifier`
4. **Integration — `tests/SupportCrm.Api.Tests/Controllers/TicketsControllerStatusTests.cs`**:
   - `Post_EscalateWithBlankReason_Returns400`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddTicketEscalation --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] Status can be set to any of New/Open/Pending/Resolved/Closed (`PUT /api/tickets/{id}/status`).
- [ ] A ticket can be escalated to a supervisor or team in one call, with a required reason (`POST /api/tickets/{id}/escalate`).
- [ ] Escalation history is queryable (`GET /api/tickets/{id}/escalations`).
- [ ] Customer notification on status change is opt-in per request and routed through a stub seam.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
