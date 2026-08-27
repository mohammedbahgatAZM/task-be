# Story 38 — Access FAQs (Story: CP-4)

---

## Prerequisites

- Story 35 completed: [`35-story-CP-1.md`](35-story-CP-1.md) — the `CustomerPortal` bounded concern this story's analytics service joins.
- Knowledge Base Stories 25/28 completed ([`../knowledge-base/25-story-KB-1.md`](../knowledge-base/25-story-KB-1.md), [`28-story-KB-4.md`](../knowledge-base/28-story-KB-4.md)) — `Faq`, `KbSearchService` — both reused entirely unmodified.

---

## Story Goal

1. Confirm (no new code) that "visible from the portal home page, searchable, bilingual" are already fully satisfied by Knowledge Base KB-1/KB-4.
2. `POST /api/kb/faqs/{id}/impression` — log that an FAQ was shown as a suggestion during a ticket draft, tagged with a client-generated `draftSessionId`.
3. `POST /api/kb/faqs/deflection/mark-converted` — flip every impression in a session to "led to a ticket" when the customer submits anyway.
4. `GET /api/kb/faqs/deflection-report` — per-FAQ deflection rate, explicitly labeled as a proxy metric.

---

## Context — Read These Files First

1. `src/SupportCrm.Api/Controllers/FaqsController.cs` (all 40 lines) — every new endpoint in this story is added here, not a new controller.
2. `src/SupportCrm.Application/KnowledgeBase/FaqService.cs` — precedent for this bounded concern's service shape; this story's own service lives in `CustomerPortal`, not `KnowledgeBase`, since the analytics are a Customer Portal concern layered on top of KB-1's `Faq`, not a KB feature itself.

---

## Backend Tasks

### 1 — Domain: `FaqPortalImpression`

**Create file: `src/SupportCrm.Domain/Entities/FaqPortalImpression.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// One row per FAQ shown as a suggestion during one ticket-draft attempt. DraftSessionId is a
// client-generated, unauthenticated correlation id — not a real session/auth concept. Flipping
// LedToTicketSubmission is the only mutation; nothing else about an impression ever changes.
public class FaqPortalImpression
{
    public Guid Id { get; private set; }
    public Guid FaqId { get; private set; }
    public string DraftSessionId { get; private set; } = default!;
    public bool LedToTicketSubmission { get; private set; }
    public DateTimeOffset ShownAtUtc { get; private set; }

    private FaqPortalImpression() { } // EF Core

    public FaqPortalImpression(Guid faqId, string draftSessionId, DateTimeOffset shownAtUtc)
    {
        if (string.IsNullOrWhiteSpace(draftSessionId))
            throw new ArgumentException("Draft session id is required.", nameof(draftSessionId));

        Id = Guid.NewGuid();
        FaqId = faqId;
        DraftSessionId = draftSessionId;
        ShownAtUtc = shownAtUtc;
    }

    public void MarkLedToTicketSubmission() => LedToTicketSubmission = true;
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IFaqPortalImpressionRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IFaqPortalImpressionRepository
{
    Task AddAsync(FaqPortalImpression impression, CancellationToken ct);
    Task<IReadOnlyList<FaqPortalImpression>> GetBySessionAsync(string draftSessionId, CancellationToken ct);
    Task<IReadOnlyList<FaqPortalImpression>> GetAllAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: `FaqPortalAnalyticsService`

**File: `src/SupportCrm.Application/CustomerPortal/CustomerPortalDtos.cs`** — append:

```csharp
public record FaqDeflectionReportItemDto(Guid FaqId, int TotalImpressions, int LedToTicketCount, double DeflectionRatePercentage);
```

**Create file: `src/SupportCrm.Application/CustomerPortal/FaqPortalAnalyticsService.cs`**

```csharp
namespace SupportCrm.Application.CustomerPortal;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.KnowledgeBase;

// "Deflection rate" is an honest proxy, not a causal measurement: the share of impressions
// where the customer did NOT go on to submit a ticket in that same draft session. See this
// story's intake for why a stronger claim isn't achievable without real session/auth infra.
public class FaqPortalAnalyticsService(
    IFaqRepository faqRepository,
    IFaqPortalImpressionRepository impressionRepository,
    TimeProvider timeProvider)
{
    public async Task LogImpressionAsync(Guid faqId, string draftSessionId, CancellationToken ct)
    {
        _ = await faqRepository.GetByIdAsync(faqId, ct) ?? throw new KeyNotFoundException($"FAQ '{faqId}' was not found.");
        await impressionRepository.AddAsync(new FaqPortalImpression(faqId, draftSessionId, timeProvider.GetUtcNow()), ct);
        await impressionRepository.SaveChangesAsync(ct);
    }

    public async Task MarkSessionConvertedAsync(string draftSessionId, CancellationToken ct)
    {
        var impressions = await impressionRepository.GetBySessionAsync(draftSessionId, ct);
        foreach (var impression in impressions)
            impression.MarkLedToTicketSubmission();
        await impressionRepository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<FaqDeflectionReportItemDto>> GetDeflectionReportAsync(CancellationToken ct)
    {
        var impressions = await impressionRepository.GetAllAsync(ct);
        return impressions
            .GroupBy(i => i.FaqId)
            .Select(g => new FaqDeflectionReportItemDto(
                g.Key,
                g.Count(),
                g.Count(i => i.LedToTicketSubmission),
                Math.Round(100.0 * g.Count(i => !i.LedToTicketSubmission) / g.Count(), 1)))
            .OrderByDescending(r => r.DeflectionRatePercentage)
            .ToList();
    }
}
```

### 3 — Infrastructure: EF config, repository, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add a `DbSet` after the last existing one:

```csharp
    public DbSet<FaqPortalImpression> FaqPortalImpressions => Set<FaqPortalImpression>();
```

Add an `OnModelCreating` block after the last existing one:

```csharp

        modelBuilder.Entity<FaqPortalImpression>(entity =>
        {
            entity.ToTable("FaqPortalImpressions");
            entity.HasKey(i => i.Id);
            entity.Property(i => i.DraftSessionId).IsRequired().HasMaxLength(64);
            entity.HasIndex(i => i.FaqId);
            entity.HasIndex(i => i.DraftSessionId);
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/FaqPortalImpressionRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class FaqPortalImpressionRepository(SupportCrmDbContext dbContext) : IFaqPortalImpressionRepository
{
    public Task AddAsync(FaqPortalImpression impression, CancellationToken ct)
    {
        dbContext.FaqPortalImpressions.Add(impression);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<FaqPortalImpression>> GetBySessionAsync(string draftSessionId, CancellationToken ct) =>
        await dbContext.FaqPortalImpressions.Where(i => i.DraftSessionId == draftSessionId).ToListAsync(ct);

    public async Task<IReadOnlyList<FaqPortalImpression>> GetAllAsync(CancellationToken ct) =>
        await dbContext.FaqPortalImpressions.ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<IFaqPortalImpressionRepository, FaqPortalImpressionRepository>();
        services.AddScoped<FaqPortalAnalyticsService>();
```

- After creating these files, run `dotnet ef migrations add AddFaqPortalImpressions --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: `FaqsController` additions

**File: `src/SupportCrm.Api/Controllers/FaqsController.cs`** — add, and add `using SupportCrm.Application.CustomerPortal;`:

```csharp

    [HttpPost("{id:guid}/impression")]
    public async Task<IActionResult> LogImpression(Guid id, [FromBody] LogFaqImpressionRequest request, [FromServices] FaqPortalAnalyticsService analyticsService, CancellationToken ct)
    {
        try { await analyticsService.LogImpressionAsync(id, request.DraftSessionId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("deflection/mark-converted")]
    public async Task<IActionResult> MarkConverted([FromBody] MarkDraftSessionConvertedRequest request, [FromServices] FaqPortalAnalyticsService analyticsService, CancellationToken ct)
    {
        await analyticsService.MarkSessionConvertedAsync(request.DraftSessionId, ct);
        return NoContent();
    }

    [HttpGet("deflection-report")]
    public async Task<ActionResult<IReadOnlyList<FaqDeflectionReportItemDto>>> GetDeflectionReport([FromServices] FaqPortalAnalyticsService analyticsService, CancellationToken ct) =>
        Ok(await analyticsService.GetDeflectionReportAsync(ct));
```

**File: `src/SupportCrm.Application/CustomerPortal/CustomerPortalDtos.cs`** — append:

```csharp
public record LogFaqImpressionRequest(string DraftSessionId);
public record MarkDraftSessionConvertedRequest(string DraftSessionId);
```

---

## Edge Cases & Failure Modes

- **Logging an impression for an unknown FAQ id** — `KeyNotFoundException` → `404`, no orphaned impression row written.
- **`mark-converted` called for a `draftSessionId` with zero impressions** — `GetBySessionAsync` returns an empty list; the `foreach` loop is a no-op — not an error (a customer could submit a ticket having never viewed any FAQ suggestion).
- **`mark-converted` called twice for the same session** — idempotent; `MarkLedToTicketSubmission` just sets an already-`true` flag to `true` again.
- **Deflection report with zero impressions for any FAQ** — that FAQ simply doesn't appear in the grouped result (no zero-row placeholder) — the report only ever lists FAQs that have been shown at least once.
- **A single impression counted in both `TotalImpressions` and (if converted) `LedToTicketCount`** — intentional; `DeflectionRatePercentage` is computed from the *not*-converted share of the same total, so the two numbers are consistent by construction, not double-counted against each other.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/FaqPortalImpressionTests.cs`**:
   - `Constructor_BlankDraftSessionId_Throws`
2. **Unit — `tests/SupportCrm.Application.Tests/CustomerPortal/FaqPortalAnalyticsServiceTests.cs`**:
   - `MarkSessionConvertedAsync_FlipsAllImpressionsInSession`
   - `GetDeflectionReportAsync_ComputesRateFromNotConvertedShare`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddFaqPortalImpressions --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] Portal FAQ browsing/search/bilingual content confirmed already working via Knowledge Base KB-1/KB-4 (no code change needed).
- [ ] Impressions logged per draft session; conversion flips them; a deflection report is queryable, explicitly labeled as a proxy.
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 39.**
