# Story 32 — Automatic categorization (Story: AI-3)

---

## Prerequisites

- Story 30 completed: [`30-story-AI-1.md`](30-story-AI-1.md) — `AiFeaturesOptions` (this story adds `CategorizationConfidenceThresholdPercentage`, already stubbed onto that class).
- Ticket Management Stories 05–06 completed ([`../ticket-management/05-story-TM-1.md`](../ticket-management/05-story-TM-1.md), [`06-story-TM-2.md`](../ticket-management/06-story-TM-2.md)) — `Ticket.SetCategory`/`SetPriority`, `TicketCategory`, `TicketFieldChangeEntry`, `TicketService.CreateAsync` (the hook point).

---

## Story Goal

1. On every ticket creation, a mock AI categorizer suggests a category + priority with a confidence score; if confidence meets the configured threshold, it's applied automatically (logged with `ChangedBy: "AI"`) — otherwise the ticket is left uncategorized for manual review.
2. Every suggestion is recorded (`TicketCategorizationSuggestion`) regardless of whether it was applied, feeding a pending-manual-categorization list and an accuracy-over-time report.
3. Overriding the category and having that correction logged needs **zero new backend work** — reuses Ticket Management TM-2's existing `PUT /api/tickets/{id}/category` unchanged.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Tickets/TicketService.cs`, lines 12–32 (`CreateAsync`, post SLA & Automation Story 22's edits) — the exact insertion point for this story's categorization call, before the existing single `SaveChangesAsync`.
2. `src/SupportCrm.Application/Tickets/TicketService.cs`, `SetCategoryAsync` — the `TicketFieldChangeEntry` construction pattern (`fieldName`, old/new as `.ToString()`, `changedBy`, `timeProvider.GetUtcNow()`) this story's AI-driven field changes mirror exactly, with `changedBy: "AI"`.
3. `src/SupportCrm.Domain/Repositories/ITicketCategoryRepository.cs` / `TicketCategoryService.GetActiveAsync` — reused as-is for the list of categories the mock provider scores against.
4. `src/SupportCrm.Application/Ai/AiFeaturesOptions.cs` (Story 30) — already has a `CategorizationConfidenceThresholdPercentage` property stubbed for this story.

---

## Backend Tasks

### 1 — Domain: `TicketCategorizationSuggestion`

**Create file: `src/SupportCrm.Domain/Entities/TicketCategorizationSuggestion.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// One row per ticket, written at creation time regardless of whether the suggestion was
// applied — the record itself is what powers the pending-review list and accuracy report.
public class TicketCategorizationSuggestion
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid? SuggestedCategoryId { get; private set; }
    public TicketPriority SuggestedPriority { get; private set; }
    public int ConfidencePercentage { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private TicketCategorizationSuggestion() { } // EF Core

    public TicketCategorizationSuggestion(Guid ticketId, Guid? suggestedCategoryId, TicketPriority suggestedPriority, int confidencePercentage, DateTimeOffset createdAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        SuggestedCategoryId = suggestedCategoryId;
        SuggestedPriority = suggestedPriority;
        ConfidencePercentage = confidencePercentage;
        CreatedAtUtc = createdAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ITicketCategorizationSuggestionRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketCategorizationSuggestionRepository
{
    Task AddAsync(TicketCategorizationSuggestion suggestion, CancellationToken ct);
    Task<TicketCategorizationSuggestion?> GetByTicketAsync(Guid ticketId, CancellationToken ct);
    Task<IReadOnlyList<TicketCategorizationSuggestion>> GetAllAsync(CancellationToken ct);
    // Joins against Tickets in the Infrastructure implementation rather than loading every
    // ticket into application code — avoids an N+1/full-table-scan pattern for what could
    // otherwise be a large list.
    Task<IReadOnlyList<Guid>> GetPendingManualCategorizationTicketIdsAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: `IAiCategorizationProvider`, `MockAiCategorizationProvider`, `TicketCategorizationService`

**File: `src/SupportCrm.Application/Ai/AiDtos.cs`** — append:

```csharp
public record AiCategorizationResult(Guid? CategoryId, TicketPriority Priority, int ConfidencePercentage);
public record TicketCategorizationSuggestionDto(Guid TicketId, Guid? SuggestedCategoryId, TicketPriority SuggestedPriority, int ConfidencePercentage, bool WasApplied);
public record CategorizationAccuracyPointDto(DateOnly Day, int TotalSuggestions, int MatchingCount, double AccuracyPercentage);
```

**Create file: `src/SupportCrm.Application/Ai/IAiCategorizationProvider.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;

/// <summary>
/// Suggests a category + priority for a new ticket. No real classifier exists in this
/// codebase — register <see cref="MockAiCategorizationProvider"/> until one does. Its
/// "confidence" is a normalized keyword-overlap score (0-100), not a calibrated probability.
/// </summary>
public interface IAiCategorizationProvider
{
    AiCategorizationResult Categorize(string subject, string? description, IReadOnlyList<TicketCategory> activeCategories);
}
```

**Create file: `src/SupportCrm.Application/Ai/MockAiCategorizationProvider.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;

public class MockAiCategorizationProvider : IAiCategorizationProvider
{
    private static readonly (string Keyword, TicketPriority Priority)[] PriorityHints =
    {
        ("urgent", TicketPriority.Urgent), ("down", TicketPriority.Urgent), ("asap", TicketPriority.Urgent), ("critical", TicketPriority.Urgent),
        ("error", TicketPriority.High), ("broken", TicketPriority.High), ("not working", TicketPriority.High)
    };

    public AiCategorizationResult Categorize(string subject, string? description, IReadOnlyList<TicketCategory> activeCategories)
    {
        var text = $"{subject} {description}".ToLowerInvariant();

        TicketCategory? best = null;
        var bestScore = 0;
        foreach (var category in activeCategories)
        {
            var categoryWords = category.Name.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var overlap = categoryWords.Count(w => text.Contains(w));
            if (overlap > bestScore)
            {
                bestScore = overlap;
                best = category;
            }
        }

        // Floor of 0 when nothing matched at all; otherwise 40 + 25 per matched word, capped at
        // 95 — a rough, explicitly-not-calibrated stand-in for a real classifier's probability.
        var confidence = best is null ? 0 : Math.Min(95, 40 + bestScore * 25);

        var priority = TicketPriority.Medium;
        foreach (var (keyword, hintedPriority) in PriorityHints)
        {
            if (text.Contains(keyword)) { priority = hintedPriority; break; }
        }

        return new AiCategorizationResult(best?.Id, priority, confidence);
    }
}
```

**Create file: `src/SupportCrm.Application/Ai/TicketCategorizationService.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using Microsoft.Extensions.Options;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketCategorizationService(
    ITicketRepository ticketRepository,
    ITicketCategoryRepository categoryRepository,
    ITicketCategorizationSuggestionRepository suggestionRepository,
    IAiCategorizationProvider categorizationProvider,
    IOptions<AiFeaturesOptions> options,
    TimeProvider timeProvider)
{
    // Applies directly onto the in-memory `ticket` and returns field-change entries to append —
    // called from TicketService.CreateAsync BEFORE that method's own SaveChangesAsync, so ticket
    // creation and AI categorization commit in one round-trip, not two.
    public async Task<IReadOnlyList<TicketFieldChangeEntry>> CategorizeOnCreateAsync(Ticket ticket, CancellationToken ct)
    {
        var categories = await categoryRepository.GetActiveAsync(ct);
        var result = categorizationProvider.Categorize(ticket.Subject, ticket.Description, categories);
        var now = timeProvider.GetUtcNow();

        await suggestionRepository.AddAsync(new TicketCategorizationSuggestion(ticket.Id, result.CategoryId, result.Priority, result.ConfidencePercentage, now), ct);

        var fieldChanges = new List<TicketFieldChangeEntry>();
        if (result.CategoryId is not null && result.ConfidencePercentage >= options.Value.CategorizationConfidenceThresholdPercentage)
        {
            var oldCategoryId = ticket.CategoryId;
            var oldPriority = ticket.Priority;
            ticket.SetCategory(result.CategoryId);
            ticket.SetPriority(result.Priority);
            fieldChanges.Add(new TicketFieldChangeEntry(ticket.Id, "Category", oldCategoryId?.ToString(), result.CategoryId?.ToString(), "AI", now));
            fieldChanges.Add(new TicketFieldChangeEntry(ticket.Id, "Priority", oldPriority.ToString(), result.Priority.ToString(), "AI", now));
        }

        return fieldChanges;
    }

    public async Task<TicketCategorizationSuggestionDto?> GetSuggestionAsync(Guid ticketId, CancellationToken ct)
    {
        var suggestion = await suggestionRepository.GetByTicketAsync(ticketId, ct);
        if (suggestion is null) return null;
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct);
        var wasApplied = ticket is not null && ticket.CategoryId == suggestion.SuggestedCategoryId && suggestion.SuggestedCategoryId is not null;
        return new TicketCategorizationSuggestionDto(suggestion.TicketId, suggestion.SuggestedCategoryId, suggestion.SuggestedPriority, suggestion.ConfidencePercentage, wasApplied);
    }

    public Task<IReadOnlyList<Guid>> GetPendingManualCategorizationTicketIdsAsync(CancellationToken ct) =>
        suggestionRepository.GetPendingManualCategorizationTicketIdsAsync(ct);

    // Design note for the executor: this loads one ticket per suggestion (N+1) to compare
    // current vs. suggested category. Acceptable at this app's demo scale (a report endpoint,
    // not a hot path); if the suggestion table grows large, replace with a single SQL join in
    // the repository, same idea as GetPendingManualCategorizationTicketIdsAsync above.
    public async Task<IReadOnlyList<CategorizationAccuracyPointDto>> GetAccuracyReportAsync(CancellationToken ct)
    {
        var suggestions = await suggestionRepository.GetAllAsync(ct);
        var points = new List<(DateOnly Day, bool Matched)>();
        foreach (var s in suggestions)
        {
            var ticket = await ticketRepository.GetByIdAsync(s.TicketId, ct);
            if (ticket is null) continue;
            points.Add((DateOnly.FromDateTime(s.CreatedAtUtc.UtcDateTime), ticket.CategoryId == s.SuggestedCategoryId));
        }

        return points
            .GroupBy(p => p.Day)
            .OrderBy(g => g.Key)
            .Select(g => new CategorizationAccuracyPointDto(g.Key, g.Count(), g.Count(p => p.Matched), g.Count() == 0 ? 0 : Math.Round(100.0 * g.Count(p => p.Matched) / g.Count(), 1)))
            .ToList();
    }
}
```

**File: `src/SupportCrm.Application/Tickets/TicketService.cs`** — add `Ai.TicketCategorizationService categorizationService` to the primary constructor's parameter list, and insert a call in `CreateAsync` right after `ticket.SetLanguage(...)`, before `AddAsync`:

```csharp
        var aiFieldChanges = await categorizationService.CategorizeOnCreateAsync(ticket, ct);

        await ticketRepository.AddAsync(ticket, ct);
        await ticketRepository.AddStatusChangeAsync(
            new TicketStatusChangeEntry(ticket.Id, null, TicketStatus.New, request.CreatedBy, "Agent", null, now), ct);
        foreach (var change in aiFieldChanges)
            await ticketRepository.AddFieldChangeAsync(change, ct);
        await ticketRepository.SaveChangesAsync(ct);
```

(Replaces the existing `AddAsync`/`AddStatusChangeAsync`/`SaveChangesAsync` block; the `EvaluateAndAssignAsync` call afterward is unchanged — auto-assignment (SLA & Automation Story 22) already runs after save and is unaffected by this story, though it now sees a possibly-already-categorized ticket, which lets category-based assignment rules match immediately on creation.)

### 3 — Infrastructure: EF config, repository, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add a `DbSet` after Story 30's:

```csharp
    public DbSet<TicketCategorizationSuggestion> TicketCategorizationSuggestions => Set<TicketCategorizationSuggestion>();
```

Add an `OnModelCreating` block after Story 30's:

```csharp

        modelBuilder.Entity<TicketCategorizationSuggestion>(entity =>
        {
            entity.ToTable("TicketCategorizationSuggestions");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.SuggestedPriority).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.HasIndex(s => s.TicketId).IsUnique();
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/TicketCategorizationSuggestionRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketCategorizationSuggestionRepository(SupportCrmDbContext dbContext) : ITicketCategorizationSuggestionRepository
{
    public Task AddAsync(TicketCategorizationSuggestion suggestion, CancellationToken ct)
    {
        dbContext.TicketCategorizationSuggestions.Add(suggestion);
        return Task.CompletedTask;
    }

    public Task<TicketCategorizationSuggestion?> GetByTicketAsync(Guid ticketId, CancellationToken ct) =>
        dbContext.TicketCategorizationSuggestions.FirstOrDefaultAsync(s => s.TicketId == ticketId, ct);

    public async Task<IReadOnlyList<TicketCategorizationSuggestion>> GetAllAsync(CancellationToken ct) =>
        await dbContext.TicketCategorizationSuggestions.ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetPendingManualCategorizationTicketIdsAsync(CancellationToken ct) =>
        await dbContext.TicketCategorizationSuggestions
            .Where(s => dbContext.Tickets.Any(t => t.Id == s.TicketId && t.CategoryId == null))
            .Select(s => s.TicketId)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<ITicketCategorizationSuggestionRepository, TicketCategorizationSuggestionRepository>();
        services.AddScoped<IAiCategorizationProvider, MockAiCategorizationProvider>();
        services.AddScoped<TicketCategorizationService>();
```

- After creating these files, run `dotnet ef migrations add AddAiCategorization --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: `TicketsController` addition, new `AiController`

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add:

```csharp

    [HttpGet("{id:guid}/ai-categorization-suggestion")]
    public async Task<ActionResult<TicketCategorizationSuggestionDto>> GetAiCategorizationSuggestion(Guid id, [FromServices] TicketCategorizationService categorizationService, CancellationToken ct)
    {
        var suggestion = await categorizationService.GetSuggestionAsync(id, ct);
        return suggestion is null ? NotFound() : suggestion;
    }
```

**Create file: `src/SupportCrm.Api/Controllers/AiController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Ai;

[ApiController]
[Route("api/ai")]
public class AiController(TicketCategorizationService categorizationService) : ControllerBase
{
    [HttpGet("categorization/pending")]
    public async Task<ActionResult<IReadOnlyList<Guid>>> GetPendingCategorization(CancellationToken ct) =>
        Ok(await categorizationService.GetPendingManualCategorizationTicketIdsAsync(ct));

    [HttpGet("categorization/accuracy-report")]
    public async Task<ActionResult<IReadOnlyList<CategorizationAccuracyPointDto>>> GetAccuracyReport(CancellationToken ct) =>
        Ok(await categorizationService.GetAccuracyReportAsync(ct));
}
```

---

## Edge Cases & Failure Modes

- **No active categories exist yet** — `Categorize` returns `CategoryId: null`, `ConfidencePercentage: 0`; `CategorizeOnCreateAsync` records the (null-category) suggestion but never applies it — the ticket is created uncategorized, same as before this story existed.
- **Confidence exactly equals the threshold** — `>=` applies it (inclusive), consistent with how most threshold-style checks in this codebase (e.g. SLA & Automation's escalation-tier trigger) are written inclusive.
- **An agent overrides the AI-applied category via `PUT /api/tickets/{id}/category`** — `GetSuggestionAsync`'s `wasApplied` flag flips to `false` on the next read (it compares the *current* category to the *original* suggestion live, not a stored boolean), and the accuracy report's next run counts that ticket as a mismatch — both derive from live state, so nothing needs to be "updated" when an override happens elsewhere.
- **A ticket deleted or otherwise missing when the accuracy report runs** — `GetAccuracyReportAsync`'s `if (ticket is null) continue;` skips it rather than throwing (no delete path exists in this codebase currently, but the guard costs nothing and protects against a future one).
- **`GetPendingManualCategorizationTicketIdsAsync` when every ticket has since been manually categorized** — returns an empty list; the underlying `EXISTS`-style query re-evaluates `CategoryId == null` live, so a ticket drops off this list automatically the moment someone applies a category, no explicit "resolve" action needed.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Ai/MockAiCategorizationProviderTests.cs`**:
   - `Categorize_NoMatchingCategory_ReturnsZeroConfidence`
   - `Categorize_UrgentKeyword_ReturnsUrgentPriority`
2. **Unit — `tests/SupportCrm.Application.Tests/Ai/TicketCategorizationServiceTests.cs`**:
   - `CategorizeOnCreateAsync_ConfidenceBelowThreshold_DoesNotApplyCategory`
   - `CategorizeOnCreateAsync_ConfidenceAtOrAboveThreshold_AppliesAndLogsAiChangedBy`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/TicketsControllerTests.cs`** (extend Story 05's tests):
   - `Post_CreateTicket_HighConfidenceMatch_AutoCategorizes`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddAiCategorization --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.
3. **Regression:** confirm `PUT /api/tickets/{id}/category` (Ticket Management TM-2) still writes a `TicketFieldChangeEntry` unmodified.

---

## Done Criteria

- [ ] New tickets receive an AI category/priority suggestion; applied automatically when confidence meets the threshold, logged with `ChangedBy: "AI"`.
- [ ] Below-threshold tickets are discoverable via `GET /api/ai/categorization/pending`.
- [ ] `GET /api/ai/categorization/accuracy-report` shows accuracy over time.
- [ ] Overriding a category still logs via the existing, unmodified `PUT /api/tickets/{id}/category`.
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 33.**
