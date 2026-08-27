# Story 20 — Team collaboration (Story: AD-5)

---

## Prerequisites

- Story 18 completed (`18-story-AD-3.md`) — `AgentNotificationService.NotifyAsync`, reused here as-is.
- Ticket Management Story 09 (`TicketNote`, `TicketTimelineService`).

---

## Story Goal

1. `TicketCollaborator` (many-to-many `Ticket`↔`Agent`, distinct from the single `AssignedAgentId`).
2. `@AgentName` mentions in an internal note (case-insensitive, exact full-name match) notify the mentioned agent (via Story 18's `AgentNotificationService.NotifyAsync`) and auto-add them as a collaborator.
3. `TicketTimelineService` gets a new `"Collaboration"` entry kind.

**Not in scope (see intake):** internal-comment customer-invisibility (already true since Ticket Management TM-5 — nothing to build), @-mention autocomplete, removing a collaborator.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Tickets/TicketMessageService.cs`'s `AddNoteAsync` and `src/SupportCrm.Api/Controllers/TicketsController.cs`'s `AddNote` action — mention parsing hooks in at the **controller** level (calling a new orchestration step after the note is saved), the same way Communication Channels CC-5's `WebFormSubmissionsController` orchestrates attachments after `SubmitAsync` — `TicketMessageService` itself is not touched.
2. `src/SupportCrm.Application/Tickets/TicketTimelineService.cs` and `TicketMessageDtos.cs`'s `TicketTimelineEntryDto` — the new `"Collaboration"` entries are added alongside the existing `"Assignment"`/`"Escalation"` ones; no frontend change needed since the timeline already renders arbitrary `Kind`s generically.
3. `src/SupportCrm.Application/Tickets/AgentNotificationService.cs` (Story 18) — `NotifyAsync` is called directly, not re-implemented.

---

## Backend Tasks

### 1 — Domain: one new entity, one repository

**Create file: `src/SupportCrm.Domain/Entities/TicketCollaborator.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class TicketCollaborator
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AgentId { get; private set; }
    public DateTimeOffset AddedAtUtc { get; private set; }

    private TicketCollaborator() { } // EF Core

    public TicketCollaborator(Guid ticketId, Guid agentId, DateTimeOffset addedAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        AgentId = agentId;
        AddedAtUtc = addedAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ITicketCollaboratorRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketCollaboratorRepository
{
    Task<IReadOnlyList<TicketCollaborator>> GetByTicketAsync(Guid ticketId, CancellationToken ct);
    Task<bool> ExistsAsync(Guid ticketId, Guid agentId, CancellationToken ct);
    Task AddAsync(TicketCollaborator collaborator, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, collaboration service, timeline extension

**Create file: `src/SupportCrm.Application/Tickets/TicketCollaborationDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

public record TicketCollaboratorDto(Guid Id, Guid TicketId, Guid AgentId, DateTimeOffset AddedAtUtc);
public record AddTicketCollaboratorRequest(Guid AgentId);
```

**Create file: `src/SupportCrm.Application/Tickets/TicketCollaborationService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketCollaborationService(
    ITicketCollaboratorRepository collaboratorRepository,
    IAgentRepository agentRepository,
    AgentNotificationService notificationService,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<TicketCollaboratorDto>> GetForTicketAsync(Guid ticketId, CancellationToken ct) =>
        (await collaboratorRepository.GetByTicketAsync(ticketId, ct))
            .Select(c => new TicketCollaboratorDto(c.Id, c.TicketId, c.AgentId, c.AddedAtUtc))
            .ToList();

    public async Task AddCollaboratorAsync(Guid ticketId, Guid agentId, CancellationToken ct)
    {
        if (await collaboratorRepository.ExistsAsync(ticketId, agentId, ct)) return; // idempotent — already a collaborator
        await collaboratorRepository.AddAsync(new TicketCollaborator(ticketId, agentId, timeProvider.GetUtcNow()), ct);
        await collaboratorRepository.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Called after an internal note is saved. Matching is deliberately simple — an
    /// exact, case-insensitive "@FullName" substring check against every known agent's
    /// name, not a regex tokenizer or fuzzy match (per the intake's explicit scope
    /// decision) — there is no mention-autocomplete UI, so the note text is always a
    /// plain string typed by the agent.
    /// </summary>
    public async Task ProcessMentionsAsync(Guid ticketId, string noteText, CancellationToken ct)
    {
        var agents = await agentRepository.GetAllAsync(ct);
        var mentioned = agents.Where(a => noteText.Contains($"@{a.Name}", StringComparison.OrdinalIgnoreCase));

        foreach (var agent in mentioned)
        {
            await notificationService.NotifyAsync(agent.Id, "Mention", "You were mentioned in an internal note.", ticketId, ct);
            await AddCollaboratorAsync(ticketId, agent.Id, ct);
        }
    }
}
```

**File: `TicketTimelineService.cs`** — inject `ITicketCollaboratorRepository` and `IAgentRepository`, and add a `"Collaboration"` entry per collaborator:

```csharp
public class TicketTimelineService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ITicketCollaboratorRepository collaboratorRepository,
    IAgentRepository agentRepository)
{
    public async Task<IReadOnlyList<TicketTimelineEntryDto>> GetTimelineAsync(Guid ticketId, CancellationToken ct)
    {
        // ...existing lookups (messages, notes, statusChanges, assignments, escalations)...
        var collaborators = await collaboratorRepository.GetByTicketAsync(ticketId, ct);
        var agentNames = (await agentRepository.GetAllAsync(ct)).ToDictionary(a => a.Id, a => a.Name);

        var entries = new List<TicketTimelineEntryDto>();
        // ...existing entries.AddRange(...) calls, unchanged...
        entries.AddRange(collaborators.Select(c => new TicketTimelineEntryDto(
            c.Id, "Collaboration", false, c.AddedAtUtc,
            $"{(agentNames.TryGetValue(c.AgentId, out var name) ? name : "Unknown agent")} added as a collaborator",
            "System", null, null)));

        return entries.OrderBy(e => e.OccurredAtUtc).ToList();
    }
}
```

(This is an additive change to the existing method — every other line stays exactly as Communication Channels CC-6 left it; only the constructor and one more `entries.AddRange(...)` call are new.)

### 3 — Infrastructure: EF config, repository, DI

**File: `SupportCrmDbContext.cs`** — add `DbSet<TicketCollaborator> TicketCollaborators` and:

```csharp
        modelBuilder.Entity<TicketCollaborator>(entity =>
        {
            entity.ToTable("TicketCollaborators");
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => new { c.TicketId, c.AgentId }).IsUnique();
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/TicketCollaboratorRepository.cs`** — straightforward EF implementation; `ExistsAsync` via `AnyAsync(c => c.TicketId == ticketId && c.AgentId == agentId, ct)`.

**File: `DependencyInjection.cs`** — add:

```csharp
        services.AddScoped<ITicketCollaboratorRepository, TicketCollaboratorRepository>();
        services.AddScoped<TicketCollaborationService>();
```

### 4 — Api: controller

**File: `TicketsController.cs`** — add:

```csharp
    [HttpGet("{id:guid}/collaborators")]
    public async Task<ActionResult<IReadOnlyList<TicketCollaboratorDto>>> GetCollaborators(
        Guid id, [FromServices] TicketCollaborationService collaborationService, CancellationToken ct) =>
        Ok(await collaborationService.GetForTicketAsync(id, ct));

    [HttpPost("{id:guid}/collaborators")]
    public async Task<IActionResult> AddCollaborator(
        Guid id, [FromBody] AddTicketCollaboratorRequest request, [FromServices] TicketCollaborationService collaborationService, CancellationToken ct)
    {
        await collaborationService.AddCollaboratorAsync(id, request.AgentId, ct);
        return NoContent();
    }
```

**File: `TicketsController.cs`**'s existing `AddNote` action — extend to process mentions after the note is saved:

```csharp
    [HttpPost("{id:guid}/notes")]
    public async Task<ActionResult<TicketNoteDto>> AddNote(
        Guid id, [FromBody] AddTicketNoteRequest request, [FromServices] TicketMessageService messageService,
        [FromServices] TicketCollaborationService collaborationService, CancellationToken ct)
    {
        try
        {
            var note = await messageService.AddNoteAsync(id, request, ct);
            await collaborationService.ProcessMentionsAsync(id, note.Text, ct);
            return note;
        }
        catch (TicketNotFoundException) { return NotFound(); }
    }
```

---

## Edge Cases & Failure Modes

- **A note mentions an agent who is already a collaborator** — `AddCollaboratorAsync`'s `ExistsAsync` check makes this a no-op for the collaborator row, but the agent still gets notified every time they're mentioned (that part is intentionally not deduplicated — a second mention is a second, legitimate ping).
- **A note mentions a name that happens to be a substring of another agent's name** (e.g. `"@Ann"` when both "Ann" and "Anna" exist) — matching is on the *whole* agent name (`@{a.Name}`), so `"@Ann"` only matches an agent literally named "Ann", not "Anna"; it would, however, still match if the note text is `"@Anna ..."` and there's also an agent named "Anna" — exact per-agent full-name matching, no partial-name collisions beyond what the underlying `Contains` naturally allows.
- **`TicketTimelineEntryDto` shape unchanged** (Story 15/CC-6 already added `Channel`/`AuthorKind`) — this story passes `null` for both on `"Collaboration"` entries, consistent with every other non-message kind.
- **Unique index on `(TicketId, AgentId)`** — guards against a race adding the same collaborator twice concurrently; the application-level `ExistsAsync` check is the common-path guard, the index is the backstop.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketCollaborationServiceTests.cs`**:
   - `ProcessMentionsAsync_MentionedAgent_GetsNotifiedAndAddedAsCollaborator`
   - `ProcessMentionsAsync_AlreadyCollaborator_StillNotifiedButNotDuplicated`
   - `ProcessMentionsAsync_NoMention_NoOp`
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketTimelineServiceCollaborationTests.cs`**:
   - `GetTimelineAsync_IncludesCollaborationEntryWithAgentName`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Manual smoke:** add an internal note mentioning a real agent by name; confirm that agent's notifications list shows a `"Mention"` entry and the ticket's collaborators list includes them.

---

## Done Criteria

- [ ] `@AgentName` in an internal note notifies that agent and adds them as a collaborator.
- [ ] Internal notes remain customer-invisible (already true, verified not regressed).
- [ ] A ticket's collaborators are listable/addable independent of the primary assignee.
- [ ] The timeline shows a `"Collaboration"` entry for each collaborator added.
- [ ] `dotnet build SupportCrm.slnx` succeeds. Migration needed: new `TicketCollaborators` table.
