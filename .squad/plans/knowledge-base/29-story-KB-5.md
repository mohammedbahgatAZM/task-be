# Story 29 — Content authoring (Story: KB-5)

---

## Prerequisites

- Story 26 completed: [`26-story-KB-2.md`](26-story-KB-2.md) — `Article`, `KbContentStatus` (`Draft`/`Published`/`Archived`).
- Story 27 completed: [`27-story-KB-3.md`](27-story-KB-3.md) — `Guide` (same `KbContentStatus`), `Agent.IsKnowledgeBaseEditor`.

---

## Story Goal

1. Extend `KbContentStatus` with `UnderReview`, and add authorized transition actions (submit-for-review, publish, unpublish, archive) to both `Article` and `Guide` — replacing any ad-hoc status mutation with a checked state machine, gated by `Agent.IsKnowledgeBaseEditor` (Story 27).
2. Publishing (or scheduling) sets an optional `ReviewDueAtUtc`; a query surfaces content past its review date for a manager to act on — no automatic background job, discoverable on demand only.
3. Every edit to content that has ever been published snapshots its prior title/body into a queryable `ContentVersionEntry` history row before the edit is applied. Never-published drafts are not snapshotted (nothing public changed yet).
4. Archiving/unpublishing is the only way content stops being live — no delete path exists anywhere in this feature.

**Not in scope:** FAQs (Story 25) — no formal workflow applies to them. Automatic background flagging of overdue-for-review content (unlike SLA & Automation's escalation service, there's no AC requiring automatic action here). Version rollback/restore — history is tracked and queryable, not automatically revertible.

---

## Context — Read These Files First

1. [`26-story-KB-2.md`](26-story-KB-2.md), `## Backend Tasks` → `### 1` — `Article`'s current shape (no transition methods yet, just `RecordUpdate`/`IncrementViewCount`/`MarkHelpful`/`MarkNotHelpful`) and its `KbContentStatus` doc comment, which explicitly defers this story's work.
2. [`27-story-KB-3.md`](27-story-KB-3.md), `## Backend Tasks` → `### 1`/`### 2` — `Guide`'s equivalent shape, and `GuideService.RequireEditorAsync`'s exact editor-check pattern this story's `ContentWorkflowService` reuses (do not duplicate a second editor-check helper — extract or call the same logic).
3. `src/SupportCrm.Domain/Entities/TicketEscalationEntry.cs` (Ticket Management Story 08, all ~30 lines) — the closest precedent in this codebase for an insert-only audit/snapshot row; `ContentVersionEntry` follows the same "immutable row, no update method" shape.
4. `src/SupportCrm.Api/Controllers/TicketsController.cs`, lines 123–140 (`SetStatus`/`Escalate`/`GetEscalations`) — precedent for adding lifecycle-transition actions directly onto an existing resource's controller (`ArticlesController`/`GuidesController`) rather than a separate controller.

---

## Backend Tasks

### 1 — Domain: extend `KbContentStatus`, transition methods, `ContentVersionEntry`

**File: `src/SupportCrm.Domain/Entities/KbContentStatus.cs`** — replace the enum body:

```csharp
public enum KbContentStatus
{
    Draft,
    UnderReview,
    Published,
    Archived
}
```

**File: `src/SupportCrm.Domain/Entities/Article.cs`** — add properties after `CreatedAtUtc`:

```csharp
    public bool HasBeenPublished { get; private set; }
    public DateTimeOffset? ReviewDueAtUtc { get; private set; }
```

and transition methods after `MarkNotHelpful` (replacing the `// Story 29 adds...` comment):

```csharp
    public void SubmitForReview()
    {
        if (Status is not (KbContentStatus.Draft or KbContentStatus.UnderReview))
            throw new InvalidOperationException($"Cannot submit for review from status '{Status}'.");
        Status = KbContentStatus.UnderReview;
    }

    public void Publish(DateTimeOffset? reviewDueAtUtc)
    {
        Status = KbContentStatus.Published;
        HasBeenPublished = true;
        ReviewDueAtUtc = reviewDueAtUtc;
    }

    public void Unpublish() => Status = KbContentStatus.Draft;

    public void Archive() => Status = KbContentStatus.Archived;
```

**File: `src/SupportCrm.Domain/Entities/Guide.cs`** — identical additions (`HasBeenPublished`, `ReviewDueAtUtc`, and the same four transition methods), replacing its own `// Story 29 adds...` comment.

**Create file: `src/SupportCrm.Domain/Entities/ContentVersionEntry.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// Immutable snapshot row — one per edit to content that has ever been Published. ContentType
// is "Article" | "Guide"; ContentId is that content's own Id. Never-published Drafts are not
// snapshotted (see ContentWorkflowService) — nothing public changed yet.
public class ContentVersionEntry
{
    public Guid Id { get; private set; }
    public string ContentType { get; private set; } = default!;
    public Guid ContentId { get; private set; }
    public int VersionNumber { get; private set; }
    public string? TitleEnSnapshot { get; private set; }
    public string? TitleArSnapshot { get; private set; }
    public string? BodyEnSnapshot { get; private set; }
    public string? BodyArSnapshot { get; private set; }
    public string ChangedBy { get; private set; } = default!;
    public DateTimeOffset ChangedAtUtc { get; private set; }

    private ContentVersionEntry() { } // EF Core

    public ContentVersionEntry(string contentType, Guid contentId, int versionNumber, string? titleEnSnapshot, string? titleArSnapshot, string? bodyEnSnapshot, string? bodyArSnapshot, string changedBy, DateTimeOffset changedAtUtc)
    {
        Id = Guid.NewGuid();
        ContentType = contentType;
        ContentId = contentId;
        VersionNumber = versionNumber;
        TitleEnSnapshot = titleEnSnapshot;
        TitleArSnapshot = titleArSnapshot;
        BodyEnSnapshot = bodyEnSnapshot;
        BodyArSnapshot = bodyArSnapshot;
        ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        ChangedAtUtc = changedAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IContentVersionRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IContentVersionRepository
{
    Task<IReadOnlyList<ContentVersionEntry>> GetForContentAsync(string contentType, Guid contentId, CancellationToken ct);
    Task<int> GetNextVersionNumberAsync(string contentType, Guid contentId, CancellationToken ct);
    Task AddAsync(ContentVersionEntry entry, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Extend file: `src/SupportCrm.Domain/Repositories/IArticleRepository.cs`** — add:

```csharp
    Task<IReadOnlyList<Article>> GetDueForReviewAsync(DateTimeOffset asOfUtc, CancellationToken ct);
```

**Extend file: `src/SupportCrm.Domain/Repositories/IGuideRepository.cs`** — add the equivalent `GetDueForReviewAsync`.

### 2 — Application: `ContentWorkflowService`, wiring into `ArticleService`/`GuideService`

**Create file: `src/SupportCrm.Application/KnowledgeBase/ContentWorkflowDtos.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

public record PublishContentRequest(Guid EditorAgentId, DateTimeOffset? ReviewDueAtUtc);
public record TransitionContentRequest(Guid EditorAgentId);
public record ContentVersionDto(int VersionNumber, string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string ChangedBy, DateTimeOffset ChangedAtUtc);
public record DueForReviewItemDto(string ContentType, Guid ContentId, string? TitleEn, string? TitleAr, DateTimeOffset ReviewDueAtUtc);
```

**Create file: `src/SupportCrm.Application/KnowledgeBase/ContentWorkflowService.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ContentWorkflowService(
    IArticleRepository articleRepository,
    IGuideRepository guideRepository,
    IContentVersionRepository versionRepository,
    IAgentRepository agentRepository,
    TimeProvider timeProvider)
{
    public async Task SubmitForReviewAsync(string contentType, Guid contentId, TransitionContentRequest request, CancellationToken ct)
    {
        await RequireEditorAsync(request.EditorAgentId, ct);
        if (contentType == "Article")
        {
            var article = await articleRepository.GetByIdAsync(contentId, ct) ?? throw new ArticleNotFoundException(contentId.ToString());
            article.SubmitForReview();
            await articleRepository.SaveChangesAsync(ct);
        }
        else
        {
            var guide = await guideRepository.GetByIdAsync(contentId, ct) ?? throw new GuideNotFoundException(contentId.ToString());
            guide.SubmitForReview();
            await guideRepository.SaveChangesAsync(ct);
        }
    }

    public async Task PublishAsync(string contentType, Guid contentId, PublishContentRequest request, CancellationToken ct)
    {
        await RequireEditorAsync(request.EditorAgentId, ct);
        if (contentType == "Article")
        {
            var article = await articleRepository.GetByIdAsync(contentId, ct) ?? throw new ArticleNotFoundException(contentId.ToString());
            article.Publish(request.ReviewDueAtUtc);
            await articleRepository.SaveChangesAsync(ct);
        }
        else
        {
            var guide = await guideRepository.GetByIdAsync(contentId, ct) ?? throw new GuideNotFoundException(contentId.ToString());
            guide.Publish(request.ReviewDueAtUtc);
            await guideRepository.SaveChangesAsync(ct);
        }
    }

    public async Task UnpublishAsync(string contentType, Guid contentId, TransitionContentRequest request, CancellationToken ct) =>
        await ApplyTransitionAsync(contentType, contentId, request.EditorAgentId, a => a.Unpublish(), g => g.Unpublish(), ct);

    public async Task ArchiveAsync(string contentType, Guid contentId, TransitionContentRequest request, CancellationToken ct) =>
        await ApplyTransitionAsync(contentType, contentId, request.EditorAgentId, a => a.Archive(), g => g.Archive(), ct);

    private async Task ApplyTransitionAsync(string contentType, Guid contentId, Guid editorAgentId, Action<Article> onArticle, Action<Guide> onGuide, CancellationToken ct)
    {
        await RequireEditorAsync(editorAgentId, ct);
        if (contentType == "Article")
        {
            var article = await articleRepository.GetByIdAsync(contentId, ct) ?? throw new ArticleNotFoundException(contentId.ToString());
            onArticle(article);
            await articleRepository.SaveChangesAsync(ct);
        }
        else
        {
            var guide = await guideRepository.GetByIdAsync(contentId, ct) ?? throw new GuideNotFoundException(contentId.ToString());
            onGuide(guide);
            await guideRepository.SaveChangesAsync(ct);
        }
    }

    // Snapshots the CURRENT (pre-edit) state if the content has ever been published, then
    // applies the edit. Call this instead of ArticleService.UpdateAsync/GuideService.UpdateAsync
    // directly once versioning matters — those two still exist and still work for never-published
    // drafts, this wraps them for the versioned path.
    public async Task SnapshotIfPublishedThenUpdateArticleAsync(Guid articleId, UpdateArticleRequest request, CancellationToken ct)
    {
        var article = await articleRepository.GetByIdAsync(articleId, ct) ?? throw new ArticleNotFoundException(articleId.ToString());
        if (article.HasBeenPublished)
        {
            var versionNumber = await versionRepository.GetNextVersionNumberAsync("Article", articleId, ct);
            await versionRepository.AddAsync(new ContentVersionEntry("Article", articleId, versionNumber,
                article.TitleEn, article.TitleAr, article.BodyEn, article.BodyAr, request.ChangedBy, timeProvider.GetUtcNow()), ct);
            await versionRepository.SaveChangesAsync(ct);
        }
        article.RecordUpdate(request.TitleEn?.Trim(), request.TitleAr?.Trim(), request.BodyEn?.Trim(), request.BodyAr?.Trim(), request.ChangedBy, timeProvider.GetUtcNow());
        await articleRepository.SaveChangesAsync(ct);
    }

    // Equivalent for Guide — snapshots via GuideService's own editor check first (guide edits are
    // already editor-gated at GuideService.UpdateAsync, called by the caller before this).
    public async Task SnapshotIfPublishedAsync(string contentType, Guid contentId, CancellationToken ct)
    {
        if (contentType == "Article")
        {
            var article = await articleRepository.GetByIdAsync(contentId, ct);
            if (article is { HasBeenPublished: true })
            {
                var versionNumber = await versionRepository.GetNextVersionNumberAsync("Article", contentId, ct);
                await versionRepository.AddAsync(new ContentVersionEntry("Article", contentId, versionNumber,
                    article.TitleEn, article.TitleAr, article.BodyEn, article.BodyAr, article.LastUpdatedByName, timeProvider.GetUtcNow()), ct);
                await versionRepository.SaveChangesAsync(ct);
            }
        }
        else
        {
            var guide = await guideRepository.GetByIdAsync(contentId, ct);
            if (guide is { HasBeenPublished: true })
            {
                var versionNumber = await versionRepository.GetNextVersionNumberAsync("Guide", contentId, ct);
                await versionRepository.AddAsync(new ContentVersionEntry("Guide", contentId, versionNumber,
                    guide.TitleEn, guide.TitleAr, guide.BodyEn, guide.BodyAr, guide.LastUpdatedByName, timeProvider.GetUtcNow()), ct);
                await versionRepository.SaveChangesAsync(ct);
            }
        }
    }

    public async Task<IReadOnlyList<ContentVersionDto>> GetVersionHistoryAsync(string contentType, Guid contentId, CancellationToken ct) =>
        (await versionRepository.GetForContentAsync(contentType, contentId, ct))
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new ContentVersionDto(v.VersionNumber, v.TitleEnSnapshot, v.TitleArSnapshot, v.BodyEnSnapshot, v.BodyArSnapshot, v.ChangedBy, v.ChangedAtUtc))
            .ToList();

    public async Task<IReadOnlyList<DueForReviewItemDto>> GetDueForReviewAsync(CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var articles = (await articleRepository.GetDueForReviewAsync(now, ct))
            .Select(a => new DueForReviewItemDto("Article", a.Id, a.TitleEn, a.TitleAr, a.ReviewDueAtUtc!.Value));
        var guides = (await guideRepository.GetDueForReviewAsync(now, ct))
            .Select(g => new DueForReviewItemDto("Guide", g.Id, g.TitleEn, g.TitleAr, g.ReviewDueAtUtc!.Value));
        return articles.Concat(guides).OrderBy(d => d.ReviewDueAtUtc).ToList();
    }

    private async Task RequireEditorAsync(Guid agentId, CancellationToken ct)
    {
        var agent = await agentRepository.GetByIdAsync(agentId, ct);
        if (agent is null || !agent.IsKnowledgeBaseEditor)
            throw new KbEditorRequiredException(agentId);
    }
}
```

**Note for the executor on the two snapshot methods:** `SnapshotIfPublishedThenUpdateArticleAsync` is the simplest correct path for Articles (mirrors `ArticleService.UpdateAsync` but adds the snapshot first) — prefer wiring `ArticlesController`'s `PUT {id}` to call this method instead of `ArticleService.UpdateAsync` directly, once this story lands (a controller change, not an `ArticleService` change, to avoid a circular `ArticleService` ↔ `ContentWorkflowService` dependency). `SnapshotIfPublishedAsync` (the generic, snapshot-only variant) exists for `Guide`, since `GuideService.UpdateAsync` already owns the editor check and the actual field update — call `contentWorkflowService.SnapshotIfPublishedAsync("Guide", id, ct)` from `GuidesController`'s `Update` action **before** calling `guideService.UpdateAsync(...)`, in the same request.

### 3 — Infrastructure: EF config, repositories, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add a `DbSet` after Story 28's:

```csharp
    public DbSet<ContentVersionEntry> ContentVersionEntries => Set<ContentVersionEntry>();
```

Extend the `Article`/`Guide` blocks with one property line each:

```csharp
            entity.Property(a => a.HasBeenPublished).IsRequired(); // in the Article block
```
```csharp
            entity.Property(g => g.HasBeenPublished).IsRequired(); // in the Guide block
```

Add an `OnModelCreating` block after Story 28's:

```csharp

        modelBuilder.Entity<ContentVersionEntry>(entity =>
        {
            entity.ToTable("ContentVersions");
            entity.HasKey(v => v.Id);
            entity.Property(v => v.ContentType).IsRequired().HasMaxLength(16);
            entity.Property(v => v.ChangedBy).IsRequired().HasMaxLength(256);
            entity.HasIndex(v => new { v.ContentType, v.ContentId, v.VersionNumber }).IsUnique();
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/ContentVersionRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ContentVersionRepository(SupportCrmDbContext dbContext) : IContentVersionRepository
{
    public async Task<IReadOnlyList<ContentVersionEntry>> GetForContentAsync(string contentType, Guid contentId, CancellationToken ct) =>
        await dbContext.ContentVersionEntries.Where(v => v.ContentType == contentType && v.ContentId == contentId).ToListAsync(ct);

    public async Task<int> GetNextVersionNumberAsync(string contentType, Guid contentId, CancellationToken ct)
    {
        var max = await dbContext.ContentVersionEntries
            .Where(v => v.ContentType == contentType && v.ContentId == contentId)
            .Select(v => (int?)v.VersionNumber)
            .MaxAsync(ct);
        return (max ?? 0) + 1;
    }

    public Task AddAsync(ContentVersionEntry entry, CancellationToken ct)
    {
        dbContext.ContentVersionEntries.Add(entry);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/Persistence/ArticleRepository.cs`** — add:

```csharp
    public async Task<IReadOnlyList<Article>> GetDueForReviewAsync(DateTimeOffset asOfUtc, CancellationToken ct) =>
        await dbContext.Articles.Where(a => a.ReviewDueAtUtc != null && a.ReviewDueAtUtc <= asOfUtc).ToListAsync(ct);
```

**File: `src/SupportCrm.Infrastructure/Persistence/GuideRepository.cs`** — add the equivalent `GetDueForReviewAsync`.

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<IContentVersionRepository, ContentVersionRepository>();
        services.AddScoped<ContentWorkflowService>();
```

- After creating these files, run `dotnet ef migrations add AddKbContentWorkflow --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: extend `ArticlesController`/`GuidesController`

**File: `src/SupportCrm.Api/Controllers/ArticlesController.cs`** — add, and add `[FromServices] ContentWorkflowService workflowService` parameters as shown:

```csharp

    [HttpPost("{id:guid}/submit-for-review")]
    public async Task<IActionResult> SubmitForReview(Guid id, [FromBody] TransitionContentRequest request, [FromServices] ContentWorkflowService workflowService, CancellationToken ct)
    {
        try { await workflowService.SubmitForReviewAsync("Article", id, request, ct); return NoContent(); }
        catch (ArticleNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, [FromBody] PublishContentRequest request, [FromServices] ContentWorkflowService workflowService, CancellationToken ct)
    {
        try { await workflowService.PublishAsync("Article", id, request, ct); return NoContent(); }
        catch (ArticleNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, [FromBody] TransitionContentRequest request, [FromServices] ContentWorkflowService workflowService, CancellationToken ct)
    {
        try { await workflowService.UnpublishAsync("Article", id, request, ct); return NoContent(); }
        catch (ArticleNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, [FromBody] TransitionContentRequest request, [FromServices] ContentWorkflowService workflowService, CancellationToken ct)
    {
        try { await workflowService.ArchiveAsync("Article", id, request, ct); return NoContent(); }
        catch (ArticleNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<IReadOnlyList<ContentVersionDto>>> GetVersions(Guid id, [FromServices] ContentWorkflowService workflowService, CancellationToken ct) =>
        Ok(await workflowService.GetVersionHistoryAsync("Article", id, ct));
```

**File: `src/SupportCrm.Api/Controllers/GuidesController.cs`** — add the equivalent four transition actions plus `GetVersions`, calling `ContentWorkflowService` with `"Guide"` as the content type.

**Create file: `src/SupportCrm.Api/Controllers/KbContentReviewController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.KnowledgeBase;

[ApiController]
[Route("api/kb/content")]
public class KbContentReviewController(ContentWorkflowService workflowService) : ControllerBase
{
    [HttpGet("due-for-review")]
    public async Task<ActionResult<IReadOnlyList<DueForReviewItemDto>>> GetDueForReview(CancellationToken ct) =>
        Ok(await workflowService.GetDueForReviewAsync(ct));
}
```

---

## Edge Cases & Failure Modes

- **`SubmitForReview` called on `Published` or `Archived` content** — `Article.SubmitForReview`/`Guide.SubmitForReview`'s guard rejects any status other than `Draft`/`UnderReview` (`InvalidOperationException` → `400`) — publishing does not need to be preceded by review in this state machine (see next point), so `Published` content is not blocked from being edited/unpublished through other actions, just not re-submitted for review from that state via this specific action.
- **`Publish` called directly from `Draft`** (skipping `UnderReview`) — allowed; the AC says a workflow "controls when content goes live," not that review is mandatory before every publish — this keeps the workflow flexible (an editor can fast-track a trivial fix) rather than rigidly enforcing every transition, and is a documented, deliberate choice.
- **Non-editor agent calls any transition endpoint** — `KbEditorRequiredException` → `403`, checked before any state change, same pattern as Story 27.
- **Editing content that has never been published** — `HasBeenPublished` is `false`; no `ContentVersionEntry` is written, only the plain field update happens — matches the AC's literal wording ("changes to a *published* article").
- **Editing previously-published-then-unpublished (`Draft` again via `Unpublish`) content** — `HasBeenPublished` stays `true` once set (never reset by `Unpublish`), so edits continue to be versioned even while the content is temporarily back in `Draft` — this is intentional: history should not have gaps just because content was briefly taken down.
- **Two concurrent edits to the same content racing on `GetNextVersionNumberAsync`** — both could compute the same "next" number under high concurrency (a classic read-then-insert race); the unique index on `(ContentType, ContentId, VersionNumber)` makes the second `SaveChangesAsync` throw rather than silently create a duplicate/incorrect version number — flagged as a known rare-race gap, not silently ignored, matching this codebase's existing "defense via unique index, no distributed lock" pattern (e.g. SLA & Automation's escalation tier uniqueness).
- **`GetDueForReviewAsync` when nothing has a `ReviewDueAtUtc` set** — both underlying queries return empty lists; the combined result is empty, not an error.
- **Attempting to delete an `Article`/`Guide`** — no delete endpoint exists anywhere in this feature (Stories 26/27/29 combined); `Archive` is confirmed as the only "make it stop being live" path — call this out explicitly if a future story is tempted to add one.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/ArticleWorkflowTests.cs`**:
   - `SubmitForReview_FromPublished_Throws`
   - `Publish_SetsHasBeenPublishedTrue`
   - `Unpublish_DoesNotResetHasBeenPublished`
2. **Unit — `tests/SupportCrm.Application.Tests/KnowledgeBase/ContentWorkflowServiceTests.cs`**:
   - `SnapshotIfPublishedThenUpdateArticleAsync_NeverPublished_NoVersionWritten`
   - `SnapshotIfPublishedThenUpdateArticleAsync_PreviouslyPublished_WritesVersionBeforeEdit`
   - `GetDueForReviewAsync_CombinesArticlesAndGuidesSortedByDueDate`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/ArticlesControllerWorkflowTests.cs`**:
   - `Post_PublishAsNonEditor_Returns403`
   - `Get_Versions_ReturnsDescendingByVersionNumber`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddKbContentWorkflow --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Frontend Tasks

**Implemented** (`d:\Code\selfAssessment\frontend\src\app`):

- **File: `features/knowledge-base/kb.model.ts`** — `ContentVersion`, `DueForReviewItem`.
- **File: `features/knowledge-base/kb.service.ts`** — `submitForReview`/`publish`/`unpublish`/`archive` (parameterized by `contentType: 'Article' | 'Guide'`, routing to the right controller segment internally), `getVersions`, `getDueForReview`.
- **File: `features/knowledge-base/articles/articles.component.{ts,html}`** (Story 26) and **`features/knowledge-base/guides/guides.component.{ts,html}`** (Story 27) — each card got a workflow button group (Submit for review / Publish / Unpublish / Archive) plus a "History" toggle that loads and lists `ContentVersion` rows inline. Every transition uses the navbar's "acting as" agent as `editorAgentId` and surfaces a specific message on `403` (not a KB editor) vs. any other failure (invalid transition for the current status).
- **Create file: `features/knowledge-base/kb-review/kb-review.component.{ts,html,scss}`** — flat table of everything due for review (both content types combined, sorted by due date), each row linking to the relevant admin page. Route: `/admin/kb-review`.
- **File: `app.routes.ts`**, **`layout/app-shell/app-shell.component.ts`** — route + sidebar nav entry ("Content review").

*Not built:* setting `reviewDueAtUtc` from the UI — `publish()` in `kb.service.ts` accepts it, but neither `ArticlesComponent`'s nor `GuidesComponent`'s Publish button collects a date yet (always publishes with no review deadline). Flagged as a follow-up: add a date input next to the Publish action.

---

## Done Criteria

- [ ] `Draft → UnderReview → Published → Archived` (and `→ Draft` via unpublish) transitions are authorized-only and exposed on `ArticlesController`/`GuidesController`.
- [ ] Publishing accepts an optional `ReviewDueAtUtc`; `GET /api/kb/content/due-for-review` surfaces overdue items on demand.
- [ ] Edits to ever-published content snapshot a `ContentVersionEntry` before applying (`GET /api/kb/articles/{id}/versions`, `/api/kb/guides/{id}/versions`).
- [ ] No delete endpoint exists anywhere in this feature — archive is the only way content stops being live.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
