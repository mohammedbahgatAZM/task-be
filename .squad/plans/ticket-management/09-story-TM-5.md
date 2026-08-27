# Story 09 — Ticket history (Story: TM-5)

---

## Prerequisites

- Story 05 completed: [`05-story-TM-1.md`](05-story-TM-1.md) — `TicketStatusChangeEntry`.
- Story 07 completed: [`07-story-TM-3.md`](07-story-TM-3.md) — `TicketAssignmentChangeEntry`.
- Story 08 completed: [`08-story-TM-4.md`](08-story-TM-4.md) — `TicketEscalationEntry`.

---

## Story Goal

1. Every message, status change, assignment, and note on a ticket appears in **one chronological timeline**, merging four existing audit tables (`TicketStatusChangeEntry`, `TicketAssignmentChangeEntry`, `TicketEscalationEntry`) plus two new ones this story introduces (`TicketMessage`, `TicketNote`) — this story does not introduce new source-of-truth tables for status/assignment/escalation, only merges what TM-1/TM-3/TM-4 already record.
2. The timeline distinguishes **customer-visible messages** from **internal-only** entries. `TicketMessage` is always customer-visible by definition (the AC's "messages"); `TicketNote` (internal notes) and all three audit-trail entry types (status/assignment/escalation) are internal-only.
3. History remains fully queryable after a ticket is `Closed` — there is no status-based read restriction anywhere in this story's queries.
4. Export "as PDF": per the intake's explicit decision, this is a **frontend, browser-native print-to-PDF** view, not server-side PDF generation — this story's backend work is only to expose the merged timeline data cleanly enough for the frontend to render and print it (TM-5's frontend story does the print view). No PDF library is added to this codebase.

---

## Context — Read These Files First

1. [`05-story-TM-1.md`](05-story-TM-1.md) `### 1`, [`07-story-TM-3.md`](07-story-TM-3.md) `### 1`, [`08-story-TM-4.md`](08-story-TM-4.md) `### 1` — the three existing audit-entry shapes this story merges (`TicketStatusChangeEntry`, `TicketAssignmentChangeEntry`, `TicketEscalationEntry`), all already persisted by earlier stories. Re-read their exact property names before writing the merge projection below.
2. `src/SupportCrm.Application/Customers/NoteAndAttachmentService.cs` (from Customer Management CM-4) — the closest existing precedent for a "notes" entity + repository shape; this story's `TicketNote` follows the same pattern (`AuthorName`, `Text`, `CreatedAtUtc`, no pinning here since it isn't requested for tickets).
3. `src/SupportCrm.Application/Customers/CustomerTimelineService.cs` (27 lines, whole file, from Customer Management CM-3) — the closest precedent for a **merge-multiple-sources-and-sort** service; this story's `TicketTimelineService` is structurally similar but merges from concrete tables it queries directly (there's no need for a multi-implementation seam here, since all the sources are fixed and already known — TM-1/TM-3/TM-4/this story's own two new tables — unlike CM-3's open-ended future-channel seam).

---

## Backend Tasks

### 1 — Domain: `TicketMessage`, `TicketNote`

**Create file: `src/SupportCrm.Domain/Entities/TicketMessage.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// Always customer-visible by definition — this is the AC's "message" concept,
// distinct from TicketNote (always internal-only).
public class TicketMessage
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string Body { get; private set; } = default!;
    public string AuthorName { get; private set; } = default!;
    public string AuthorKind { get; private set; } = default!; // "Customer" | "Agent" | "System"
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private TicketMessage() { } // EF Core

    public TicketMessage(Guid ticketId, string body, string authorName, string authorKind, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Message body is required.", nameof(body));

        Id = Guid.NewGuid();
        TicketId = ticketId;
        Body = body;
        AuthorName = string.IsNullOrWhiteSpace(authorName) ? "unknown" : authorName;
        AuthorKind = authorKind is "Customer" or "Agent" or "System" ? authorKind : "Agent";
        CreatedAtUtc = createdAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Entities/TicketNote.cs`** — same shape as Customer Management's `CustomerNote` (CM-4), minus pinning:

```csharp
namespace SupportCrm.Domain.Entities;

public class TicketNote
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string Text { get; private set; } = default!;
    public string AuthorName { get; private set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private TicketNote() { } // EF Core

    public TicketNote(Guid ticketId, string text, string authorName, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Note text is required.", nameof(text));

        Id = Guid.NewGuid();
        TicketId = ticketId;
        Text = text;
        AuthorName = string.IsNullOrWhiteSpace(authorName) ? "unknown" : authorName;
        CreatedAtUtc = createdAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ITicketMessageRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketMessageRepository
{
    Task<IReadOnlyList<TicketMessage>> GetMessagesAsync(Guid ticketId, CancellationToken ct);
    Task AddMessageAsync(TicketMessage message, CancellationToken ct);
    Task<IReadOnlyList<TicketNote>> GetNotesAsync(Guid ticketId, CancellationToken ct);
    Task AddNoteAsync(TicketNote note, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, `TicketMessageService`, `TicketTimelineService`

**Create file: `src/SupportCrm.Application/Tickets/TicketMessageDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

public record AddTicketMessageRequest(string Body, string AuthorName, string AuthorKind);
public record TicketMessageDto(Guid Id, string Body, string AuthorName, string AuthorKind, DateTimeOffset CreatedAtUtc);
public record AddTicketNoteRequest(string Text, string AuthorName);
public record TicketNoteDto(Guid Id, string Text, string AuthorName, DateTimeOffset CreatedAtUtc);

public record TicketTimelineEntryDto(
    Guid Id,
    string Kind,          // "Message" | "Note" | "StatusChange" | "Assignment" | "Escalation"
    bool IsCustomerVisible,
    DateTimeOffset OccurredAtUtc,
    string Summary,
    string AuthorName);
```

**Create file: `src/SupportCrm.Application/Tickets/TicketMessageService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketMessageService(ITicketRepository ticketRepository, ITicketMessageRepository repository, TimeProvider timeProvider)
{
    public async Task<TicketMessageDto> AddMessageAsync(Guid ticketId, AddTicketMessageRequest request, CancellationToken ct)
    {
        _ = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var message = new TicketMessage(ticketId, request.Body.Trim(), request.AuthorName, request.AuthorKind, timeProvider.GetUtcNow());
        await repository.AddMessageAsync(message, ct);
        await repository.SaveChangesAsync(ct);
        return new TicketMessageDto(message.Id, message.Body, message.AuthorName, message.AuthorKind, message.CreatedAtUtc);
    }

    public async Task<TicketNoteDto> AddNoteAsync(Guid ticketId, AddTicketNoteRequest request, CancellationToken ct)
    {
        _ = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var note = new TicketNote(ticketId, request.Text.Trim(), request.AuthorName, timeProvider.GetUtcNow());
        await repository.AddNoteAsync(note, ct);
        await repository.SaveChangesAsync(ct);
        return new TicketNoteDto(note.Id, note.Text, note.AuthorName, note.CreatedAtUtc);
    }
}
```

**Create file: `src/SupportCrm.Application/Tickets/TicketTimelineService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Repositories;

public class TicketTimelineService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository)
{
    public async Task<IReadOnlyList<TicketTimelineEntryDto>> GetTimelineAsync(Guid ticketId, CancellationToken ct)
    {
        _ = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());

        var messages = await messageRepository.GetMessagesAsync(ticketId, ct);
        var notes = await messageRepository.GetNotesAsync(ticketId, ct);
        var statusChanges = await ticketRepository.GetStatusHistoryAsync(ticketId, ct);
        var assignments = await ticketRepository.GetAssignmentHistoryAsync(ticketId, ct); // new member, see ## Backend Tasks ### 1 extension below
        var escalations = await ticketRepository.GetEscalationsAsync(ticketId, ct);

        var entries = new List<TicketTimelineEntryDto>();
        entries.AddRange(messages.Select(m => new TicketTimelineEntryDto(m.Id, "Message", true, m.CreatedAtUtc, m.Body, m.AuthorName)));
        entries.AddRange(notes.Select(n => new TicketTimelineEntryDto(n.Id, "Note", false, n.CreatedAtUtc, n.Text, n.AuthorName)));
        entries.AddRange(statusChanges.Select(s => new TicketTimelineEntryDto(s.Id, "StatusChange", false, s.ChangedAtUtc,
            s.OldStatus is null ? $"Created with status {s.NewStatus}" : $"Status changed from {s.OldStatus} to {s.NewStatus}", s.ChangedBy)));
        entries.AddRange(assignments.Select(a => new TicketTimelineEntryDto(a.Id, "Assignment", false, a.ChangedAtUtc,
            "Reassigned", a.ChangedBy)));
        entries.AddRange(escalations.Select(e => new TicketTimelineEntryDto(e.Id, "Escalation", false, e.EscalatedAtUtc,
            $"Escalated: {e.Reason}", e.EscalatedBy)));

        // Chronological, oldest first — reads like a conversation, unlike Customer
        // Management's CM-3 timeline (newest first), which is a deliberate difference:
        // a single ticket's history is read start-to-end, a customer's cross-channel
        // feed is scanned most-recent-first.
        return entries.OrderBy(e => e.OccurredAtUtc).ToList();
    }
}
```

**Extend file: `src/SupportCrm.Domain/Repositories/ITicketRepository.cs`** — add (the assignment-history read was never exposed as its own query before this story; TM-3 only wrote `TicketAssignmentChangeEntry` rows, it never needed to read them back):

```csharp
    Task<IReadOnlyList<TicketAssignmentChangeEntry>> GetAssignmentHistoryAsync(Guid ticketId, CancellationToken ct);
```

### 3 — Infrastructure: EF config, repository, DI

**File: `SupportCrmDbContext.cs`** — add `DbSet<TicketMessage>` and `DbSet<TicketNote>` + `OnModelCreating` blocks (same style as `CustomerNote`/`CustomerAttachment` from CM-4).

**Create file: `src/SupportCrm.Infrastructure/Persistence/TicketMessageRepository.cs`** — straightforward EF implementation of `ITicketMessageRepository`, mirroring `NoteAndAttachmentRepository` (CM-4).

**File: `TicketRepository.cs`** — implement `GetAssignmentHistoryAsync`:

```csharp
    public async Task<IReadOnlyList<TicketAssignmentChangeEntry>> GetAssignmentHistoryAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketAssignmentChangeEntries.Where(e => e.TicketId == ticketId).ToListAsync(ct);
```

**File: `DependencyInjection.cs`** — add `ITicketMessageRepository/TicketMessageRepository`, `TicketMessageService`, `TicketTimelineService` registrations.

### 4 — Api: controller additions

**File: `TicketsController.cs`** — add:

```csharp
    [HttpPost("{id:guid}/messages")]
    public async Task<ActionResult<TicketMessageDto>> AddMessage(Guid id, [FromBody] AddTicketMessageRequest request, [FromServices] TicketMessageService messageService, CancellationToken ct)
    {
        try { return await messageService.AddMessageAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<TicketNoteDto>> AddNote(Guid id, [FromBody] AddTicketNoteRequest request, [FromServices] TicketMessageService messageService, CancellationToken ct)
    {
        try { return await messageService.AddNoteAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<IReadOnlyList<TicketTimelineEntryDto>>> GetTimeline(Guid id, [FromServices] TicketTimelineService timelineService, CancellationToken ct)
    {
        try { return Ok(await timelineService.GetTimelineAsync(id, ct)); }
        catch (TicketNotFoundException) { return NotFound(); }
    }
```

- After creating these files, run `dotnet ef migrations add AddTicketMessagesAndNotes --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

---

## Edge Cases & Failure Modes

- **Timeline requested for a `Closed` ticket** — no code path in `TicketTimelineService`/`TicketRepository` filters by `Status`; every query is keyed only by `TicketId`. Verified structurally, not assumed — satisfies "full history remains accessible after closed."
- **Blank message body / note text** — both entities' constructors throw `ArgumentException` → `400`.
- **`AuthorKind` outside the three allowed values** — `TicketMessage`'s constructor silently coerces to `"Agent"` rather than throwing, matching `TicketStatusChangeEntry`'s `ChangedByKind` precedent (TM-1) exactly.
- **A ticket with zero messages/notes/status-changes-beyond-creation/assignments/escalations** — the timeline still returns at least the "Created with status New" entry from TM-1's initial `TicketStatusChangeEntry`; never an empty list for any ticket that exists.
- **Very large ticket with hundreds of timeline entries** — no pagination is added in this story (unlike Customer Management's CM-3 timeline, which explicitly needed it for the 500-interaction/2-second AC) — the intake's AC for this story doesn't call out a performance target, so this is an accepted simplification, not an oversight. Flag as a follow-up if a specific ticket's timeline grows large in practice.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketTimelineServiceTests.cs`**:
   - `GetTimelineAsync_MergesAllFiveEntryKindsInChronologicalOrder`
   - `GetTimelineAsync_MessagesAreCustomerVisible_EverythingElseIsNot`
   - `GetTimelineAsync_UnknownTicket_ThrowsTicketNotFoundException`
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketMessageServiceTests.cs`**:
   - `AddMessageAsync_WithBlankBody_ThrowsArgumentException`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/TicketsControllerTimelineTests.cs`**:
   - `Get_TimelineForClosedTicket_StillReturnsFullHistory`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddTicketMessagesAndNotes --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] Messages, status changes, assignments, escalations, and notes all appear in one chronological endpoint (`GET /api/tickets/{id}/timeline`).
- [ ] Each entry is flagged `isCustomerVisible` — `true` only for `Message` entries.
- [ ] History remains fully queryable regardless of ticket status.
- [ ] No PDF-generation dependency is added to the backend — export is a frontend print view (TM-5 frontend story).
- [ ] `dotnet build SupportCrm.slnx` succeeds.
