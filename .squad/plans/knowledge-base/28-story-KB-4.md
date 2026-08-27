# Story 28 — Search (Story: KB-4)

---

## Prerequisites

- Story 25 completed: [`25-story-KB-1.md`](25-story-KB-1.md) — `Faq`.
- Story 26 completed: [`26-story-KB-2.md`](26-story-KB-2.md) — `Article`, `KbContentStatus`.
- Story 27 completed: [`27-story-KB-3.md`](27-story-KB-3.md) — `Guide`.

---

## Story Goal

1. One search endpoint queries FAQs, Articles, and Guides together and returns a combined, relevance-ranked result list.
2. Matching is partial/substring-tolerant via case-insensitive `ILIKE` across both `*En`/`*Ar` fields per content type; a best-effort fuzzy layer is added via PostgreSQL's `pg_trgm` trigram similarity if the installed Npgsql EF provider supports it (verified at implementation time — falls back to `ILIKE`-only matching otherwise, with the gap noted explicitly rather than silently downgraded).
3. Each result includes a snippet with the matched keyword marked, extracted from whichever field (title/body, English/Arabic) matched.
4. Only `Published` `Article`/`Guide` rows are searchable; all `Faq` rows are searchable (no status gate). Every search call — including zero-result ones — is logged.

**Not in scope:** a dedicated search engine/index (Elasticsearch, etc.) — PostgreSQL-native matching only. True linguistic stemming/tokenization for Arabic — trigram similarity approximates fuzzy matching without language-aware tokenization, a known limitation, not solved here.

---

## Context — Read These Files First

1. `src/SupportCrm.Infrastructure/Persistence/CustomerRepository.cs`, lines 15–20 — this codebase's only existing search precedent (a plain `.Contains()` predicate); this story extends the idea with `EF.Functions.ILike` and, if available, trigram similarity, but keeps the same "simple LINQ predicate, no separate service" shape.
2. [`25-story-KB-1.md`](25-story-KB-1.md) → `Faq`, [`26-story-KB-2.md`](26-story-KB-2.md) → `Article`/`KbContentStatus`, [`27-story-KB-3.md`](27-story-KB-3.md) → `Guide` — the three entities this story's repositories query against; read each entity's field list (all fields already bilingual `*En`/`*Ar` pairs) before writing the match predicates.
3. `src/SupportCrm.Infrastructure/DependencyInjection.cs` — `UseNpgsql` registration (`AddInfrastructure`, near the top) confirms the Postgres provider this story's `pg_trgm` usage depends on.

---

## Backend Tasks

### 1 — Domain: `SearchLog`

**Create file: `src/SupportCrm.Domain/Entities/SearchLog.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class SearchLog
{
    public Guid Id { get; private set; }
    public string Query { get; private set; } = default!;
    public int ResultCount { get; private set; }
    public DateTimeOffset SearchedAtUtc { get; private set; }

    private SearchLog() { } // EF Core

    public SearchLog(string query, int resultCount, DateTimeOffset searchedAtUtc)
    {
        Id = Guid.NewGuid();
        Query = query;
        ResultCount = resultCount;
        SearchedAtUtc = searchedAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ISearchLogRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ISearchLogRepository
{
    Task AddAsync(SearchLog entry, CancellationToken ct);
    Task<IReadOnlyList<SearchLog>> GetZeroResultLogsAsync(int take, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Extend file: `src/SupportCrm.Domain/Repositories/IFaqRepository.cs`** — add:

```csharp
    Task<IReadOnlyList<Faq>> SearchAsync(string query, CancellationToken ct);
```

**Extend file: `src/SupportCrm.Domain/Repositories/IArticleRepository.cs`** — add:

```csharp
    Task<IReadOnlyList<Article>> SearchPublishedAsync(string query, CancellationToken ct);
```

**Extend file: `src/SupportCrm.Domain/Repositories/IGuideRepository.cs`** — add:

```csharp
    Task<IReadOnlyList<Guide>> SearchPublishedAsync(string query, CancellationToken ct);
```

### 2 — Application: DTOs, `KbSearchService`

**Create file: `src/SupportCrm.Application/KnowledgeBase/KbSearchDtos.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

public record KbSearchResultDto(string ContentType, Guid ContentId, string Title, string Snippet, double Score);
public record KbSearchResponseDto(IReadOnlyList<KbSearchResultDto> Results, int TotalCount);
```

**Create file: `src/SupportCrm.Application/KnowledgeBase/KbSearchService.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class KbSearchService(
    IFaqRepository faqRepository,
    IArticleRepository articleRepository,
    IGuideRepository guideRepository,
    ISearchLogRepository searchLogRepository,
    TimeProvider timeProvider)
{
    private const int SnippetContextChars = 80;

    public async Task<KbSearchResponseDto> SearchAsync(string query, int take, CancellationToken ct)
    {
        var normalizedQuery = query.Trim();
        if (normalizedQuery.Length == 0)
            return new KbSearchResponseDto(Array.Empty<KbSearchResultDto>(), 0);

        var faqs = (await faqRepository.SearchAsync(normalizedQuery, ct))
            .Select(f => Score("Faq", f.Id, PickField(f.QuestionEn, f.QuestionAr, normalizedQuery), PickField(f.AnswerEn, f.AnswerAr, normalizedQuery), normalizedQuery));
        var articles = (await articleRepository.SearchPublishedAsync(normalizedQuery, ct))
            .Select(a => Score("Article", a.Id, PickField(a.TitleEn, a.TitleAr, normalizedQuery), PickField(a.BodyEn, a.BodyAr, normalizedQuery), normalizedQuery));
        var guides = (await guideRepository.SearchPublishedAsync(normalizedQuery, ct))
            .Select(g => Score("Guide", g.Id, PickField(g.TitleEn, g.TitleAr, normalizedQuery), PickField(g.BodyEn, g.BodyAr, normalizedQuery), normalizedQuery));

        var combined = faqs.Concat(articles).Concat(guides)
            .OrderByDescending(r => r.Score)
            .Take(take)
            .ToList();

        await searchLogRepository.AddAsync(new SearchLog(normalizedQuery, combined.Count, timeProvider.GetUtcNow()), ct);
        await searchLogRepository.SaveChangesAsync(ct);

        return new KbSearchResponseDto(combined, combined.Count);
    }

    // Picks whichever language field actually matched the query (English preferred on a tie),
    // so a caller searching in Arabic gets an Arabic title/snippet back, not a blank English one.
    private static string PickField(string? en, string? ar, string query) =>
        (en is not null && en.Contains(query, StringComparison.OrdinalIgnoreCase)) || ar is null ? en ?? ar ?? "" : ar;

    private static KbSearchResultDto Score(string contentType, Guid id, string title, string body, string query)
    {
        var titleMatch = title.Contains(query, StringComparison.OrdinalIgnoreCase);
        var bodyMatch = body.Contains(query, StringComparison.OrdinalIgnoreCase);
        var score = (titleMatch ? 2.0 : 0.0) + (bodyMatch ? 1.0 : 0.0);
        var snippet = ExtractSnippet(bodyMatch ? body : title, query);
        return new KbSearchResultDto(contentType, id, title, snippet, score);
    }

    private static string ExtractSnippet(string text, string query)
    {
        var index = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return text.Length <= SnippetContextChars * 2 ? text : text[..(SnippetContextChars * 2)];

        var start = Math.Max(0, index - SnippetContextChars);
        var end = Math.Min(text.Length, index + query.Length + SnippetContextChars);
        var match = text.Substring(index, query.Length);
        var before = text[start..index];
        var after = text[(index + query.Length)..end];
        return $"{(start > 0 ? "…" : "")}{before}**{match}**{after}{(end < text.Length ? "…" : "")}";
    }
}
```

**Design note for the executor:** `PickField`/`Score` re-run a C# `.Contains` check that duplicates the DB-side `ILIKE`/trigram predicate used to *find* each row in `SearchAsync`/`SearchPublishedAsync` — this is deliberate, not redundant: the repository query decides *whether* a row is a candidate (DB-side, using whatever matching mechanism is available), while this in-memory pass decides *which language field* to display and how to score/snippet it, which needs the actual string content already loaded into memory regardless.

### 3 — Infrastructure: repository search methods, EF config, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add a `DbSet` after Story 27's:

```csharp
    public DbSet<SearchLog> SearchLogs => Set<SearchLog>();
```

Add an `OnModelCreating` block after Story 27's:

```csharp

        modelBuilder.Entity<SearchLog>(entity =>
        {
            entity.ToTable("SearchLogs");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Query).IsRequired().HasMaxLength(512);
            entity.HasIndex(s => s.ResultCount);
        });
```

**Enable `pg_trgm` in a migration** (see step below) — the migration this story generates should include, in its `Up` method:

```csharp
migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");
```

**File: `src/SupportCrm.Infrastructure/Persistence/FaqRepository.cs`** — add:

```csharp
    public async Task<IReadOnlyList<Faq>> SearchAsync(string query, CancellationToken ct) =>
        await dbContext.Faqs
            .Where(f =>
                EF.Functions.ILike(f.QuestionEn ?? "", $"%{query}%") || EF.Functions.ILike(f.QuestionAr ?? "", $"%{query}%") ||
                EF.Functions.ILike(f.AnswerEn ?? "", $"%{query}%") || EF.Functions.ILike(f.AnswerAr ?? "", $"%{query}%"))
            .ToListAsync(ct);
```

**File: `src/SupportCrm.Infrastructure/Persistence/ArticleRepository.cs`** — add:

```csharp
    public async Task<IReadOnlyList<Article>> SearchPublishedAsync(string query, CancellationToken ct) =>
        await dbContext.Articles
            .Where(a => a.Status == KbContentStatus.Published)
            .Where(a =>
                EF.Functions.ILike(a.TitleEn ?? "", $"%{query}%") || EF.Functions.ILike(a.TitleAr ?? "", $"%{query}%") ||
                EF.Functions.ILike(a.BodyEn ?? "", $"%{query}%") || EF.Functions.ILike(a.BodyAr ?? "", $"%{query}%"))
            .ToListAsync(ct);
```

**File: `src/SupportCrm.Infrastructure/Persistence/GuideRepository.cs`** — add the equivalent `SearchPublishedAsync`, same shape, over `Guide`'s fields.

**Verify before relying on it — `EF.Functions.ILike` availability:** this is a standard, long-supported `Npgsql.EntityFrameworkCore.PostgreSQL` function mapping; confirm the package version in `src/SupportCrm.Infrastructure/SupportCrm.Infrastructure.csproj` supports it (it has since early v3.x) before writing the queries above. If a stronger fuzzy match is wanted beyond `ILIKE`'s substring matching, add `EF.Functions.TrigramsSimilarity(field, query) > 0.3` as an additional `||` condition in each predicate above **only if** that function resolves against the installed Npgsql EF provider version — if it does not, skip it and leave `ILIKE`-only matching, noting the gap in this story's `## Edge Cases` when implemented (do not silently claim fuzzy matching that isn't actually there).

**Create file: `src/SupportCrm.Infrastructure/Persistence/SearchLogRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SearchLogRepository(SupportCrmDbContext dbContext) : ISearchLogRepository
{
    public Task AddAsync(SearchLog entry, CancellationToken ct)
    {
        dbContext.SearchLogs.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<SearchLog>> GetZeroResultLogsAsync(int take, CancellationToken ct) =>
        await dbContext.SearchLogs
            .Where(s => s.ResultCount == 0)
            .OrderByDescending(s => s.SearchedAtUtc)
            .Take(take)
            .ToListAsync(ct);

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<ISearchLogRepository, SearchLogRepository>();
        services.AddScoped<KbSearchService>();
```

- After creating these files, run `dotnet ef migrations add AddKbSearch --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`, then **manually add** the `CREATE EXTENSION IF NOT EXISTS pg_trgm;` line to the generated migration's `Up` method (EF's `Sql()` call, shown above) — `dotnet ef migrations add` does not generate raw SQL on its own.

### 4 — Api: `KbSearchController`

**Create file: `src/SupportCrm.Api/Controllers/KbSearchController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.KnowledgeBase;

[ApiController]
[Route("api/kb/search")]
public class KbSearchController(KbSearchService searchService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<KbSearchResponseDto>> Search([FromQuery] string q, [FromQuery] int take, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q)) return BadRequest("A query is required.");
        return Ok(await searchService.SearchAsync(q, take <= 0 ? 20 : take, ct));
    }
}
```

---

## Frontend Tasks

**Implemented** (`d:\Code\selfAssessment\frontend\src\app`):

- **File: `features/knowledge-base/kb.model.ts`** — `KbSearchResult`, `KbSearchResponse`.
- **File: `features/knowledge-base/kb.service.ts`** — `search(query, take)`.
- **File: `features/knowledge-base/kb-public/kb-public.component.{ts,html}`** (Story 25) — the search box + result list lives at the top of the public `/kb` page: each result shows a content-type badge, title, and the highlighted snippet (rendered via `[innerText]`, so the `**match**` markers from `KbSearchService.ExtractSnippet` show as literal asterisks rather than bold text — a deliberate no-`innerHTML` choice to avoid any injection risk; upgrading to real bold rendering would need a small sanitized-markdown pass, flagged as a follow-up, not a correctness bug).

Zero-result search logging (`SearchLog`) needs no frontend work — it's written automatically by every backend `SearchAsync` call regardless of what the UI does with the response.

---

## Edge Cases & Failure Modes

- **Blank/whitespace-only query** — the controller rejects it with `400` before calling the service; `SearchAsync`'s own `normalizedQuery.Length == 0` guard is a second line of defense (e.g. if called directly from another service later) returning an empty response rather than querying every row.
- **Query matches zero results across all three content types** — `combined` is an empty list; `SearchLog` is still written with `ResultCount = 0` (every search is logged, not just zero-result ones, per the intake's explicit simplification) — this is exactly the "content gap" signal `GetZeroResultLogsAsync` surfaces.
- **A query matches an `Article`/`Guide` that is `Draft` or `Archived`** — excluded by `SearchPublishedAsync`'s `Status == Published` filter; never appears in results, consistent with each content type's own read-endpoint visibility rule.
- **`pg_trgm` extension creation fails** (e.g. insufficient DB privileges in some hosting environments) — the migration's `Up` method throws, blocking the whole migration; if this happens in practice, remove the `CREATE EXTENSION` line and fall back to `ILIKE`-only matching (see the Backend Tasks note above) rather than blocking all of Knowledge Base on it.
- **`PickField` when both `en`/`ar` are null** (a content row somehow has neither field populated — should be prevented by each entity's constructor, but defensive here) — returns `""`, not a `NullReferenceException`.
- **Very long body text** — `ExtractSnippet` always bounds its output to roughly `SnippetContextChars * 2` characters plus the match, regardless of source length.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/KnowledgeBase/KbSearchServiceTests.cs`**:
   - `SearchAsync_BlankQuery_ReturnsEmptyWithoutLogging`
   - `SearchAsync_ZeroResults_StillWritesSearchLog`
   - `SearchAsync_RanksTitleMatchAboveBodyOnlyMatch`
   - `ExtractSnippet_LongBody_BoundsOutputLength` (via a public wrapper or `InternalsVisibleTo`, matching this codebase's existing unit-test access pattern)
2. **Integration — `tests/SupportCrm.Api.Tests/Controllers/KbSearchControllerTests.cs`**:
   - `Get_BlankQuery_Returns400`
   - `Get_MatchesAcrossAllThreeContentTypes_ReturnsCombinedResults`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddKbSearch --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`, then manually add the `pg_trgm` extension SQL to the generated migration.
3. **Regression:** confirm `GET /api/kb/search?q=<term-only-in-a-Draft-article>` returns no result for that article.

---

## Done Criteria

- [ ] `GET /api/kb/search?q=...` returns combined, relevance-ranked results across FAQs, Articles, and Guides.
- [ ] Matching is case-insensitive and partial (`ILIKE`); trigram fuzzy matching added if the installed Npgsql EF version supports it.
- [ ] Each result includes a snippet with the match marked.
- [ ] Only `Published` Articles/Guides are searchable; all FAQs are searchable.
- [ ] Every search call is logged, including zero-result ones.
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 29.**
