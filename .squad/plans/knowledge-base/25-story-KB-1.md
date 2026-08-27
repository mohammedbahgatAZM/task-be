# Story 25 — FAQs (Story: KB-1)

---

## Prerequisites

- None. First, foundational story in the `knowledge-base` feature.

---

## Story Goal

1. FAQs are organized under a new `KbCategory` taxonomy (distinct from Ticket Management's `TicketCategory`, TM-2) and publicly readable.
2. Each FAQ question/answer pair supports Arabic and/or English content via parallel fields, not a generic i18n system.
3. Any caller can mark an FAQ helpful or not-helpful; no duplicate-vote prevention (no identity system to key it on).
4. Knowledge base managers can view FAQs sorted by unhelpful votes to find content that needs attention.

**Not in scope:** help articles (Story 26), solution guides (Story 27), cross-content search (Story 28), and the draft/review/publish workflow (Story 29) — FAQs are explicitly excluded from that workflow (see Story 29). Duplicate-vote prevention.

---

## Context — Read These Files First

1. `src/SupportCrm.Domain/Entities/TicketCategory.cs` (all 24 lines) — the `(Id, Name, IsActive)` shape `KbCategory` adapts, with `NameEn`/`NameAr` instead of a single `Name`. Do not reuse `TicketCategory` itself — this is a separate taxonomy for knowledge content.
2. `src/SupportCrm.Application/Tickets/TicketCategoryService.cs` (all 21 lines) and `src/SupportCrm.Infrastructure/Persistence/TicketCategoryRepository.cs` (all 22 lines) — precedent for `KbCategoryService`/`KbCategoryRepository`'s minimal CRUD shape.
3. `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`, lines 1–32 (`DbSet`s) and lines 116–129 (`TicketCategory`'s `OnModelCreating` block) — insertion points and seeding-block precedent for `KbCategory`/`Faq`.
4. `src/SupportCrm.Infrastructure/DependencyInjection.cs`, lines 1–95 (whole file) — registration list to extend.

---

## Backend Tasks

### 1 — Domain: `KbCategory`, `Faq`

**Create file: `src/SupportCrm.Domain/Entities/KbCategory.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// Distinct from TicketCategory (Ticket Management TM-2) — this taxonomy organizes knowledge
// content (FAQs/Articles/Guides), TicketCategory organizes ticket routing/reporting.
public class KbCategory
{
    public Guid Id { get; private set; }
    public string? NameEn { get; private set; }
    public string? NameAr { get; private set; }
    public bool IsActive { get; private set; } = true;

    private KbCategory() { } // EF Core

    public KbCategory(string? nameEn, string? nameAr)
    {
        if (string.IsNullOrWhiteSpace(nameEn) && string.IsNullOrWhiteSpace(nameAr))
            throw new ArgumentException("At least one of NameEn/NameAr is required.", nameof(nameEn));
        Id = Guid.NewGuid();
        NameEn = nameEn;
        NameAr = nameAr;
    }

    public void Deactivate() => IsActive = false;
}
```

**Create file: `src/SupportCrm.Domain/Entities/Faq.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class Faq
{
    public Guid Id { get; private set; }
    public Guid? KbCategoryId { get; private set; }
    public string? QuestionEn { get; private set; }
    public string? QuestionAr { get; private set; }
    public string? AnswerEn { get; private set; }
    public string? AnswerAr { get; private set; }
    public int HelpfulCount { get; private set; }
    public int NotHelpfulCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Faq() { } // EF Core

    public Faq(Guid? kbCategoryId, string? questionEn, string? questionAr, string? answerEn, string? answerAr, DateTimeOffset createdAtUtc)
    {
        var hasEnglish = !string.IsNullOrWhiteSpace(questionEn) && !string.IsNullOrWhiteSpace(answerEn);
        var hasArabic = !string.IsNullOrWhiteSpace(questionAr) && !string.IsNullOrWhiteSpace(answerAr);
        if (!hasEnglish && !hasArabic)
            throw new ArgumentException("A question+answer pair is required in at least one language.", nameof(questionEn));

        Id = Guid.NewGuid();
        KbCategoryId = kbCategoryId;
        QuestionEn = questionEn;
        QuestionAr = questionAr;
        AnswerEn = answerEn;
        AnswerAr = answerAr;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkHelpful() => HelpfulCount++;
    public void MarkNotHelpful() => NotHelpfulCount++;
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IKbCategoryRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IKbCategoryRepository
{
    Task<IReadOnlyList<KbCategory>> GetActiveAsync(CancellationToken ct);
    Task AddAsync(KbCategory category, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IFaqRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IFaqRepository
{
    Task<Faq?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Faq>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<Faq>> GetByCategoryAsync(Guid kbCategoryId, CancellationToken ct);
    Task<IReadOnlyList<Faq>> GetMostUnhelpfulAsync(int take, CancellationToken ct);
    Task AddAsync(Faq faq, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, `KbCategoryService`, `FaqService`

**Create file: `src/SupportCrm.Application/KnowledgeBase/KbCategoryDtos.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

public record CreateKbCategoryRequest(string? NameEn, string? NameAr);
public record KbCategoryDto(Guid Id, string? NameEn, string? NameAr);
```

**Create file: `src/SupportCrm.Application/KnowledgeBase/FaqDtos.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

public record CreateFaqRequest(Guid? KbCategoryId, string? QuestionEn, string? QuestionAr, string? AnswerEn, string? AnswerAr);
public record FaqDto(Guid Id, Guid? KbCategoryId, string? QuestionEn, string? QuestionAr, string? AnswerEn, string? AnswerAr, int HelpfulCount, int NotHelpfulCount);
```

**Create file: `src/SupportCrm.Application/KnowledgeBase/KbCategoryService.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class KbCategoryService(IKbCategoryRepository repository)
{
    public async Task<KbCategoryDto> CreateAsync(CreateKbCategoryRequest request, CancellationToken ct)
    {
        var category = new KbCategory(request.NameEn?.Trim(), request.NameAr?.Trim());
        await repository.AddAsync(category, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<IReadOnlyList<KbCategoryDto>> GetActiveAsync(CancellationToken ct) =>
        (await repository.GetActiveAsync(ct)).Select(ToDto).ToList();

    private static KbCategoryDto ToDto(KbCategory c) => new(c.Id, c.NameEn, c.NameAr);
}
```

**Create file: `src/SupportCrm.Application/KnowledgeBase/FaqService.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class FaqService(IFaqRepository repository, TimeProvider timeProvider)
{
    public async Task<FaqDto> CreateAsync(CreateFaqRequest request, CancellationToken ct)
    {
        var faq = new Faq(request.KbCategoryId, request.QuestionEn?.Trim(), request.QuestionAr?.Trim(),
            request.AnswerEn?.Trim(), request.AnswerAr?.Trim(), timeProvider.GetUtcNow());
        await repository.AddAsync(faq, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(faq);
    }

    public async Task<IReadOnlyList<FaqDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<FaqDto>> GetByCategoryAsync(Guid kbCategoryId, CancellationToken ct) =>
        (await repository.GetByCategoryAsync(kbCategoryId, ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<FaqDto>> GetMostUnhelpfulAsync(int take, CancellationToken ct) =>
        (await repository.GetMostUnhelpfulAsync(take, ct)).Select(ToDto).ToList();

    public async Task MarkHelpfulAsync(Guid id, CancellationToken ct)
    {
        var faq = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"FAQ '{id}' was not found.");
        faq.MarkHelpful();
        await repository.SaveChangesAsync(ct);
    }

    public async Task MarkNotHelpfulAsync(Guid id, CancellationToken ct)
    {
        var faq = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"FAQ '{id}' was not found.");
        faq.MarkNotHelpful();
        await repository.SaveChangesAsync(ct);
    }

    private static FaqDto ToDto(Faq f) => new(f.Id, f.KbCategoryId, f.QuestionEn, f.QuestionAr, f.AnswerEn, f.AnswerAr, f.HelpfulCount, f.NotHelpfulCount);
}
```

### 3 — Infrastructure: EF config, repositories, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet`s after the last existing one:

```csharp
    public DbSet<KbCategory> KbCategories => Set<KbCategory>();
    public DbSet<Faq> Faqs => Set<Faq>();
```

Add new `OnModelCreating` blocks after the last existing one:

```csharp

        modelBuilder.Entity<KbCategory>(entity =>
        {
            entity.ToTable("KbCategories");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.NameEn).HasMaxLength(256);
            entity.Property(c => c.NameAr).HasMaxLength(256);
        });

        modelBuilder.Entity<Faq>(entity =>
        {
            entity.ToTable("Faqs");
            entity.HasKey(f => f.Id);
            entity.Property(f => f.QuestionEn).HasMaxLength(512);
            entity.Property(f => f.QuestionAr).HasMaxLength(512);
            entity.HasIndex(f => f.KbCategoryId);
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/KbCategoryRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class KbCategoryRepository(SupportCrmDbContext dbContext) : IKbCategoryRepository
{
    public async Task<IReadOnlyList<KbCategory>> GetActiveAsync(CancellationToken ct) =>
        await dbContext.KbCategories.Where(c => c.IsActive).ToListAsync(ct);

    public Task AddAsync(KbCategory category, CancellationToken ct)
    {
        dbContext.KbCategories.Add(category);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/FaqRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class FaqRepository(SupportCrmDbContext dbContext) : IFaqRepository
{
    public Task<Faq?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Faqs.FirstOrDefaultAsync(f => f.Id == id, ct);

    public async Task<IReadOnlyList<Faq>> GetAllAsync(CancellationToken ct) =>
        await dbContext.Faqs.ToListAsync(ct);

    public async Task<IReadOnlyList<Faq>> GetByCategoryAsync(Guid kbCategoryId, CancellationToken ct) =>
        await dbContext.Faqs.Where(f => f.KbCategoryId == kbCategoryId).ToListAsync(ct);

    public async Task<IReadOnlyList<Faq>> GetMostUnhelpfulAsync(int take, CancellationToken ct) =>
        await dbContext.Faqs
            .Where(f => f.NotHelpfulCount > 0)
            .OrderByDescending(f => f.NotHelpfulCount)
            .Take(take)
            .ToListAsync(ct);

    public Task AddAsync(Faq faq, CancellationToken ct)
    {
        dbContext.Faqs.Add(faq);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`, and add `using SupportCrm.Application.KnowledgeBase;` to the `using` block:

```csharp
        services.AddScoped<IKbCategoryRepository, KbCategoryRepository>();
        services.AddScoped<KbCategoryService>();
        services.AddScoped<IFaqRepository, FaqRepository>();
        services.AddScoped<FaqService>();
```

- After creating these files, run `dotnet ef migrations add AddFaqsAndKbCategories --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: `KbCategoriesController`, `FaqsController`

**Create file: `src/SupportCrm.Api/Controllers/KbCategoriesController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.KnowledgeBase;

[ApiController]
[Route("api/kb/categories")]
public class KbCategoriesController(KbCategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KbCategoryDto>>> GetAll(CancellationToken ct) =>
        Ok(await categoryService.GetActiveAsync(ct));

    [HttpPost]
    public async Task<ActionResult<KbCategoryDto>> Create([FromBody] CreateKbCategoryRequest request, CancellationToken ct)
    {
        try { return await categoryService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}
```

**Create file: `src/SupportCrm.Api/Controllers/FaqsController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.KnowledgeBase;

[ApiController]
[Route("api/kb/faqs")]
public class FaqsController(FaqService faqService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FaqDto>>> GetAll([FromQuery] Guid? categoryId, CancellationToken ct) =>
        Ok(categoryId is null ? await faqService.GetAllAsync(ct) : await faqService.GetByCategoryAsync(categoryId.Value, ct));

    [HttpPost]
    public async Task<ActionResult<FaqDto>> Create([FromBody] CreateFaqRequest request, CancellationToken ct)
    {
        try { return await faqService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:guid}/helpful")]
    public async Task<IActionResult> MarkHelpful(Guid id, CancellationToken ct)
    {
        try { await faqService.MarkHelpfulAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/not-helpful")]
    public async Task<IActionResult> MarkNotHelpful(Guid id, CancellationToken ct)
    {
        try { await faqService.MarkNotHelpfulAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    // Manager review view — no RBAC exists yet, so this is unrestricted like everything else,
    // but conceptually the "unhelpful ratings visible to knowledge base managers" AC.
    [HttpGet("most-unhelpful")]
    public async Task<ActionResult<IReadOnlyList<FaqDto>>> GetMostUnhelpful([FromQuery] int take = 20, CancellationToken ct = default) =>
        Ok(await faqService.GetMostUnhelpfulAsync(take, ct));
}
```

---

## Frontend Tasks

**Implemented** (`d:\Code\selfAssessment\frontend\src\app`):

- **Create file: `features/knowledge-base/kb.model.ts`** — `KbCategory`, `CreateKbCategoryRequest`, `Faq`, `CreateFaqRequest` (plus every other Knowledge Base story's types, added together in this one shared model file — mirrors how `ticket.model.ts` holds all of Ticket Management's types).
- **Create file: `features/knowledge-base/kb.service.ts`** — `getCategories`/`createCategory`, `getFaqs`/`createFaq`, `markFaqHelpful`/`markFaqNotHelpful`, `getMostUnhelpfulFaqs`.
- **Create file: `features/knowledge-base/kb-public/kb-public.component.{ts,html,scss}`** — the public, no-shell page: FAQ accordion grouped/filterable by category, helpful/not-helpful buttons, inline category- and FAQ-create forms (bilingual EN/AR inputs, `dir="rtl"` on the Arabic fields). Sibling route `/kb` (outside the agent shell, same as `/support`/`/chat`). Also hosts Story 28's search box (see that story's Frontend Tasks).
- **File: `app.routes.ts`** — added `{ path: 'kb', component: KbPublicComponent }` as a shell sibling.
- **File: `layout/app-shell/app-shell.component.ts`** — added a "Knowledge Base" sidebar link to `/kb` (agents can jump to the public page too, per the story's dual Customer/Agent role).

*Manager review view* ("unhelpful ratings visible to knowledge base managers") — `getMostUnhelpfulFaqs` is wired in the service but not yet surfaced in its own admin widget; flagged as a small follow-up (the data is one API call away, e.g. a card on `kb-review`, Story 29's page).

---

## Edge Cases & Failure Modes

- **`Faq` created with no question/answer pair in either language** — rejected by the constructor (`ArgumentException` → `400` via `FaqsController.Create`'s catch); a question with no matching-language answer (e.g. `QuestionEn` set but `AnswerEn` blank) also fails the `hasEnglish`/`hasArabic` check.
- **`KbCategory` created with both names blank** — rejected the same way.
- **Marking an unknown FAQ id helpful/not-helpful** — `KeyNotFoundException` → `404`.
- **`GetMostUnhelpfulAsync` with zero FAQs ever rated not-helpful** — `Where(f => f.NotHelpfulCount > 0)` returns an empty list, not every FAQ — an unrated FAQ never appears in the manager review view.
- **Same person votes helpful/not-helpful repeatedly on one FAQ** — both counters simply increment every call; no dedup exists (documented gap, not a bug — see intake's Extra notes).

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/FaqTests.cs`**:
   - `Constructor_NoLanguagePairComplete_Throws`
   - `MarkHelpful_IncrementsCounter`
2. **Unit — `tests/SupportCrm.Application.Tests/KnowledgeBase/FaqServiceTests.cs`**:
   - `GetMostUnhelpfulAsync_ExcludesFaqsWithZeroNotHelpful`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/FaqsControllerTests.cs`**:
   - `Post_FaqWithNoContent_Returns400`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddFaqsAndKbCategories --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] FAQs are organized by `KbCategory` and readable via `GET /api/kb/faqs`.
- [ ] Each FAQ can be marked helpful/not-helpful (`PUT /api/kb/faqs/{id}/helpful`, `/not-helpful`).
- [ ] FAQ question/answer supports Arabic and/or English via parallel fields.
- [ ] Most-unhelpful FAQs are queryable for manager review (`GET /api/kb/faqs/most-unhelpful`).
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 26.**
