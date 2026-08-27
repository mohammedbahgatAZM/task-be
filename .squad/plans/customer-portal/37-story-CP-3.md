# Story 37 — View history (Story: CP-3)

---

## Prerequisites

- Story 36 completed: [`36-story-CP-2.md`](36-story-CP-2.md) — `CustomerPortalTicketService`, `TicketOwnershipException`, the ticket-list endpoint this story's filters already apply to.
- Ticket Management Story 08 completed ([`../ticket-management/08-story-TM-4.md`](../ticket-management/08-story-TM-4.md)) — `TicketService.RecordStatusChangeAsync`, reused verbatim for the reopen transition.

---

## Story Goal

1. `POST /api/tickets/{id}/reopen` — transitions a `Resolved`/`Closed` ticket back to `Open`, only within a configurable window since it last entered that status, ownership-checked like Story 36's portal-reply.
2. Confirm (no new code, documented here) that "closed/resolved tickets remain visible and searchable" and "filter by date range or category" are already satisfied by Story 36's list endpoint.
3. Confirm attachments and customer-visible messages ("resolution notes") on past tickets need no new endpoints — Ticket Management's existing attachment/timeline endpoints already serve them.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Tickets/TicketService.cs`, lines 59–72 (`RecordStatusChangeAsync`) — the exact method this story's reopen action calls; it already handles the audit-entry write and `Ticket.SetStatus` call.
2. `src/SupportCrm.Domain/Entities/TicketStatusChangeEntry.cs`, line 24 (`ChangedByKind = changedByKind is "Agent" or "System" ? changedByKind : "Agent";`) — confirms why this story passes `"System"`, not `"Customer"`, as `changedByKind` (see this story's own Extra Notes for the reasoning, repeated here for the executor: passing anything else silently miscoerces to `"Agent"`, which would misattribute the reopen).
3. `src/SupportCrm.Infrastructure/Storage/LocalDiskAttachmentStorage.cs`, `LocalDiskAttachmentStorageOptions` — the `IOptions<T>`-bound-from-config shape `CustomerPortalOptions` follows (same pattern as `AiFeaturesOptions`, AI Features Story 30).

---

## Backend Tasks

### 1 — Domain: none

The reopen transition is `Ticket.SetStatus`, already public and already used by `RecordStatusChangeAsync` — no domain change.

### 2 — Application: `CustomerPortalOptions`, `CustomerPortalTicketService.ReopenAsync`

**Create file: `src/SupportCrm.Application/CustomerPortal/CustomerPortalOptions.cs`**

```csharp
namespace SupportCrm.Application.CustomerPortal;

// Shared config for the whole Customer Portal feature — one options class, not one per story.
public class CustomerPortalOptions
{
    public const string SectionName = "CustomerPortal";
    public int ReopenWindowDays { get; set; } = 7;
    public int LowRatingThreshold { get; set; } = 2; // set by Story 39
}
```

**File: `src/SupportCrm.Application/CustomerPortal/CustomerPortalDtos.cs`** — append:

```csharp
public record ReopenTicketRequest(Guid CustomerId, string CustomerName);
```

**File: `src/SupportCrm.Application/CustomerPortal/CustomerPortalTicketService.cs`** — add `TicketService ticketService` and `IOptions<CustomerPortalOptions> options` to the primary constructor's parameter list, and add:

```csharp
    public async Task ReopenAsync(Guid ticketId, ReopenTicketRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        if (ticket.CustomerId != request.CustomerId)
            throw new TicketOwnershipException(ticketId);
        if (ticket.Status is not (TicketStatus.Resolved or TicketStatus.Closed))
            throw new InvalidOperationException("Only resolved or closed tickets can be reopened.");

        var history = await ticketRepository.GetStatusHistoryAsync(ticketId, ct);
        var lastResolvedOrClosedAt = history
            .Where(h => h.NewStatus is TicketStatus.Resolved or TicketStatus.Closed)
            .OrderByDescending(h => h.ChangedAtUtc)
            .Select(h => h.ChangedAtUtc)
            .First(); // guaranteed to exist — the ticket IS currently Resolved/Closed, so at least one such entry was written to get here

        var windowEnd = lastResolvedOrClosedAt.AddDays(options.Value.ReopenWindowDays);
        if (timeProvider.GetUtcNow() > windowEnd)
            throw new InvalidOperationException($"This ticket can no longer be reopened — the {options.Value.ReopenWindowDays}-day window has passed.");

        // changedByKind is "System", not "Customer" — TicketStatusChangeEntry's constructor only
        // accepts "Agent"/"System" and silently coerces anything else to "Agent", which would
        // misattribute this. Flagged as a stand-in until that entity gains a real "Customer" kind.
        await ticketService.RecordStatusChangeAsync(ticketId, TicketStatus.Open, request.CustomerName, "System", "Reopened by customer via self-service portal", ct);
    }
```

(Add `using Microsoft.Extensions.Options;` to this file.)

### 3 — Infrastructure: DI, appsettings

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.Configure<CustomerPortalOptions>(configuration.GetSection(CustomerPortalOptions.SectionName));
```

**File: `src/SupportCrm.Api/appsettings.json`** — add a top-level section:

```json
  "CustomerPortal": {
    "ReopenWindowDays": 7,
    "LowRatingThreshold": 2
  }
```

### 4 — Api: `TicketsController` addition

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add:

```csharp

    [HttpPost("{id:guid}/reopen")]
    public async Task<IActionResult> Reopen(Guid id, [FromBody] ReopenTicketRequest request, [FromServices] CustomerPortalTicketService portalTicketService, CancellationToken ct)
    {
        try { await portalTicketService.ReopenAsync(id, request, ct); return NoContent(); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (TicketOwnershipException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
```

---

## Edge Cases & Failure Modes

- **Reopening a ticket that's `New`/`Open`/`Pending`** — rejected (`400`, "only resolved or closed tickets can be reopened") — reopening an already-open ticket is meaningless.
- **Reopening exactly at the window boundary** — `now > windowEnd` (strictly greater) allows reopening in the same instant the window closes, not one tick early — consistent with this codebase's other inclusive-boundary threshold checks (e.g. SLA & Automation's escalation-tier `>=`).
- **A ticket that bounced between `Resolved`→`Open`→`Resolved` multiple times** — the window anchors on the *most recent* transition into `Resolved`/`Closed` (`OrderByDescending(...).First()`), not the first ever — each reopen effectively resets the clock, which is the intuitively correct behavior (a customer who reopened once still gets a fresh window from the second resolution).
- **Reopen on someone else's ticket** — `TicketOwnershipException` → `403`, before any status change, same ownership-check pattern as Story 36's portal-reply.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/CustomerPortal/CustomerPortalTicketServiceTests.cs`** (extend Story 36's tests):
   - `ReopenAsync_WithinWindow_TransitionsToOpen`
   - `ReopenAsync_PastWindow_Throws`
   - `ReopenAsync_TicketNotResolvedOrClosed_Throws`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Regression:** confirm `GET /api/tickets/{id}/attachments` and `/timeline` (Ticket Management TM-1/TM-5) are unmodified — this story adds no changes to either.

---

## Done Criteria

- [ ] `POST /api/tickets/{id}/reopen` works within the configured window, rejects outside it.
- [ ] Closed/resolved tickets remain listable/filterable/searchable via Story 36's endpoint (no code change needed, confirmed).
- [ ] Attachments and customer-visible messages remain accessible via existing, unmodified endpoints.
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 38.**
