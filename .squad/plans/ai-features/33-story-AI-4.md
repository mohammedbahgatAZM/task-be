# Story 33 — Suggested solutions (Story: AI-4)

---

## Prerequisites

- Knowledge Base Story 28 completed ([`../knowledge-base/28-story-KB-4.md`](../knowledge-base/28-story-KB-4.md)) — `KbSearchService.SearchAsync`, reused directly (this story is not a new AI call).

---

## Story Goal

1. `GET /api/tickets/{id}/solution-suggestions` returns the top matching `Article`/`Guide` results (FAQs excluded) for the ticket's current content — re-run fresh on every call, so it naturally "updates as the conversation develops."
2. `POST /api/tickets/{id}/solution-suggestions/feedback` logs an agent's "this was irrelevant" flag — explicitly **not** wired back into ranking yet.

**Not in scope:** inserting a suggestion into a reply (frontend-only) and any actual relevance-tuning from flagged feedback (logged only).

---

## Context — Read These Files First

1. `src/SupportCrm.Application/KnowledgeBase/KbSearchService.cs`, `SearchAsync` — called with the ticket's subject + latest messages as the query, `take` capped small (this story uses 5).
2. `src/SupportCrm.Application/KnowledgeBase/KbSearchDtos.cs` — `KbSearchResultDto`'s `ContentType`/`ContentId`/`Title`/`Snippet`/`Score` fields, filtered to `"Article"`/`"Guide"` here.
3. `src/SupportCrm.Domain/Repositories/ITicketMessageRepository.cs` — `GetMessagesAsync`, the source of "the conversation" this story's query text is built from.

---

## Backend Tasks

### 1 — Domain: `SolutionSuggestionFeedback`

**Create file: `src/SupportCrm.Domain/Entities/SolutionSuggestionFeedback.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// Logged only — does not yet feed back into KbSearchService's ranking. A stand-in for a
// future relevance-tuning pass, flagged explicitly rather than silently doing nothing useful.
public class SolutionSuggestionFeedback
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string ContentType { get; private set; } = default!; // "Article" | "Guide"
    public Guid ContentId { get; private set; }
    public string FlaggedByName { get; private set; } = default!;
    public DateTimeOffset FlaggedAtUtc { get; private set; }

    private SolutionSuggestionFeedback() { } // EF Core

    public SolutionSuggestionFeedback(Guid ticketId, string contentType, Guid contentId, string flaggedByName, DateTimeOffset flaggedAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        ContentType = contentType;
        ContentId = contentId;
        FlaggedByName = string.IsNullOrWhiteSpace(flaggedByName) ? "unknown" : flaggedByName;
        FlaggedAtUtc = flaggedAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ISolutionSuggestionFeedbackRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ISolutionSuggestionFeedbackRepository
{
    Task AddAsync(SolutionSuggestionFeedback feedback, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: `TicketSolutionSuggestionService`

**File: `src/SupportCrm.Application/Ai/AiDtos.cs`** — append:

```csharp
public record FlagSolutionSuggestionRequest(string ContentType, Guid ContentId, string FlaggedByName);
```

**Create file: `src/SupportCrm.Application/Ai/TicketSolutionSuggestionService.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;
using SupportCrm.Application.KnowledgeBase;

public class TicketSolutionSuggestionService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ISolutionSuggestionFeedbackRepository feedbackRepository,
    KbSearchService kbSearchService,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<KbSearchResultDto>> GetSuggestionsAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundForAiException(ticketId.ToString());
        var messages = await messageRepository.GetMessagesAsync(ticketId, ct);

        // Rebuilt fresh from current content on every call — no caching — so results
        // naturally "update as the conversation develops" without any diffing logic.
        var conversationText = string.Join(" ", new[] { ticket.Subject, ticket.Description }
            .Concat(messages.OrderByDescending(m => m.CreatedAtUtc).Take(5).Select(m => m.Body))
            .Where(t => !string.IsNullOrWhiteSpace(t)));

        var response = await kbSearchService.SearchAsync(conversationText, take: 5, ct);
        return response.Results.Where(r => r.ContentType is "Article" or "Guide").ToList();
    }

    public async Task FlagIrrelevantAsync(Guid ticketId, FlagSolutionSuggestionRequest request, CancellationToken ct)
    {
        var feedback = new SolutionSuggestionFeedback(ticketId, request.ContentType, request.ContentId, request.FlaggedByName, timeProvider.GetUtcNow());
        await feedbackRepository.AddAsync(feedback, ct);
        await feedbackRepository.SaveChangesAsync(ct);
    }
}
```

**Note for the executor:** `KbSearchService.SearchAsync` returns `KbSearchResponseDto` whose `Results` are `KbSearchResultDto` (see `src/SupportCrm.Application/KnowledgeBase/KbSearchDtos.cs`) — reused verbatim as this story's own response shape rather than mapping to a parallel DTO.

### 3 — Infrastructure: EF config, repository, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add a `DbSet` after Story 32's:

```csharp
    public DbSet<SolutionSuggestionFeedback> SolutionSuggestionFeedback => Set<SolutionSuggestionFeedback>();
```

Add an `OnModelCreating` block after Story 32's:

```csharp

        modelBuilder.Entity<SolutionSuggestionFeedback>(entity =>
        {
            entity.ToTable("SolutionSuggestionFeedback");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.ContentType).IsRequired().HasMaxLength(16);
            entity.Property(f => f.FlaggedByName).IsRequired().HasMaxLength(256);
            entity.HasIndex(f => new { f.ContentType, f.ContentId });
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/SolutionSuggestionFeedbackRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SolutionSuggestionFeedbackRepository(SupportCrmDbContext dbContext) : ISolutionSuggestionFeedbackRepository
{
    public Task AddAsync(SolutionSuggestionFeedback feedback, CancellationToken ct)
    {
        dbContext.SolutionSuggestionFeedback.Add(feedback);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<ISolutionSuggestionFeedbackRepository, SolutionSuggestionFeedbackRepository>();
        services.AddScoped<TicketSolutionSuggestionService>();
```

- After creating these files, run `dotnet ef migrations add AddSolutionSuggestionFeedback --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: `TicketsController` additions

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add, and add `using SupportCrm.Application.KnowledgeBase;` if not already present (needed for `KbSearchResultDto` in the return type):

```csharp

    [HttpGet("{id:guid}/solution-suggestions")]
    public async Task<ActionResult<IReadOnlyList<KbSearchResultDto>>> GetSolutionSuggestions(Guid id, [FromServices] TicketSolutionSuggestionService suggestionService, CancellationToken ct)
    {
        try { return Ok(await suggestionService.GetSuggestionsAsync(id, ct)); }
        catch (TicketNotFoundForAiException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/solution-suggestions/feedback")]
    public async Task<IActionResult> FlagSolutionSuggestion(Guid id, [FromBody] FlagSolutionSuggestionRequest request, [FromServices] TicketSolutionSuggestionService suggestionService, CancellationToken ct)
    {
        await suggestionService.FlagIrrelevantAsync(id, request, ct);
        return NoContent();
    }
```

---

## Edge Cases & Failure Modes

- **Ticket with no messages yet, subject/description blank** — `conversationText` could theoretically be empty; `KbSearchService.SearchAsync`'s own guard (`normalizedQuery.Length == 0`) returns an empty result set rather than matching every row, so this endpoint just returns an empty list, not an error.
- **Zero KB Articles/Guides published** — `Where(r => r.ContentType is "Article" or "Guide")` naturally yields an empty list when only FAQs exist (or nothing does).
- **Flagging feedback for a suggestion that's no longer returned** (content archived/unpublished since) — `FlagIrrelevantAsync` doesn't validate that `ContentId` currently appears in the suggestions list; the feedback is still recorded, since the agent's signal about that piece of content is still meaningful even if it's since changed status.
- **Unknown ticket id on `GET .../solution-suggestions`** — `TicketNotFoundForAiException` → `404`.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Ai/TicketSolutionSuggestionServiceTests.cs`**:
   - `GetSuggestionsAsync_ExcludesFaqResults`
   - `GetSuggestionsAsync_BuildsQueryFromSubjectAndRecentMessages`
2. **Integration — `tests/SupportCrm.Api.Tests/Controllers/TicketsControllerAiSolutionsTests.cs`**:
   - `Get_UnknownTicket_Returns404`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddSolutionSuggestionFeedback --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] `GET /api/tickets/{id}/solution-suggestions` returns top matching Articles/Guides, re-run fresh each call.
- [ ] `POST .../feedback` logs an irrelevant-suggestion flag.
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 34.**
