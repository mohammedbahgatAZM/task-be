# Story 27 — Solutions and guides (Story: KB-3)

---

## Prerequisites

- Story 26 completed: [`26-story-KB-2.md`](26-story-KB-2.md) — `Guide` mirrors `Article`'s rich-body/`KbContentStatus`/attachment pattern closely; this story does not reuse `Article`'s table (separate entity, no inheritance, consistent with this codebase never using entity inheritance elsewhere).
- Ticket Management Story 06 (`TicketCategory`, `src/SupportCrm.Domain/Entities/TicketCategory.cs`) — guides link to it for discovery.

---

## Story Goal

1. `Guide` holds rich-body content (numbered steps as formatted text) plus `GuideAttachment` screenshots and an external `VideoUrl` — no video upload/hosting is built.
2. A guide can be linked to one or more `TicketCategory` rows via a join table, for ticket-context discovery.
3. Any agent can flag a guide as outdated (`IsFlaggedOutdated` + reason) without changing its published visibility — a separate, lighter-weight signal than Story 29's formal review workflow.
4. Only agents with a new `Agent.IsKnowledgeBaseEditor` flag can create, modify, or publish a guide — reading is unrestricted.

**Not in scope:** FAQs (Story 25) and help articles (Story 26, done). Search (Story 28). Video upload/hosting — only an external URL field. The formal draft/review/publish/scheduled-review workflow (Story 29) — outdated-flagging here is a simple flag, not a status transition.

---

## Context — Read These Files First

1. [`26-story-KB-2.md`](26-story-KB-2.md), `## Backend Tasks` → `### 1`/`### 2`/`### 3` — `Article`/`ArticleAttachment`/`ArticleService`/`ArticleAttachmentService`/`LocalDiskArticleAttachmentStorage`, the exact shapes `Guide`/`GuideAttachment`/`GuideService`/`GuideAttachmentService`/`LocalDiskGuideAttachmentStorage` mirror.
2. `src/SupportCrm.Domain/Entities/Agent.cs`, lines 1–27 (whole file, post SLA & Automation Story 23) — `IsSupervisor`/`SetSupervisor` is the exact precedent for `IsKnowledgeBaseEditor`/`SetKnowledgeBaseEditor`.
3. `src/SupportCrm.Domain/Entities/TicketCategory.cs` (all 24 lines) — the entity `GuideTicketCategory` links to; do not modify this file.
4. `src/SupportCrm.Application/Tickets/TicketAssignmentChangeEntry` pattern via `src/SupportCrm.Domain/Entities/TicketAssignmentChangeEntry.cs` — precedent for a simple two-FK join/audit row, adapted for `GuideTicketCategory`'s `(GuideId, TicketCategoryId)` link table (no audit fields needed here, just the link).

---

## Backend Tasks

### 1 — Domain: `Guide`, `GuideAttachment`, `GuideTicketCategory`, `Agent.IsKnowledgeBaseEditor`

**File: `src/SupportCrm.Domain/Entities/Agent.cs`** — add a property after `IsSupervisor`:

```csharp
    public bool IsKnowledgeBaseEditor { get; private set; }
```

and a setter after `SetSupervisor`:

```csharp

    public void SetKnowledgeBaseEditor(bool isEditor) => IsKnowledgeBaseEditor = isEditor;
```

**Create file: `src/SupportCrm.Domain/Entities/Guide.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class Guide
{
    public Guid Id { get; private set; }
    public string? TitleEn { get; private set; }
    public string? TitleAr { get; private set; }
    public string? BodyEn { get; private set; }
    public string? BodyAr { get; private set; }
    public string? VideoUrl { get; private set; }
    public KbContentStatus Status { get; private set; } = KbContentStatus.Draft;
    public string AuthorName { get; private set; } = default!;
    public string LastUpdatedByName { get; private set; } = default!;
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }
    public bool IsFlaggedOutdated { get; private set; }
    public string? FlaggedReason { get; private set; }
    public DateTimeOffset? FlaggedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Guide() { } // EF Core

    public Guide(string? titleEn, string? titleAr, string? bodyEn, string? bodyAr, string? videoUrl, string authorName, DateTimeOffset createdAtUtc)
    {
        var hasEnglish = !string.IsNullOrWhiteSpace(titleEn) && !string.IsNullOrWhiteSpace(bodyEn);
        var hasArabic = !string.IsNullOrWhiteSpace(titleAr) && !string.IsNullOrWhiteSpace(bodyAr);
        if (!hasEnglish && !hasArabic)
            throw new ArgumentException("A title+body pair is required in at least one language.", nameof(titleEn));
        if (string.IsNullOrWhiteSpace(authorName))
            throw new ArgumentException("Author name is required.", nameof(authorName));

        Id = Guid.NewGuid();
        TitleEn = titleEn;
        TitleAr = titleAr;
        BodyEn = bodyEn;
        BodyAr = bodyAr;
        VideoUrl = videoUrl;
        AuthorName = authorName;
        LastUpdatedByName = authorName;
        LastUpdatedAtUtc = createdAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public void RecordUpdate(string? titleEn, string? titleAr, string? bodyEn, string? bodyAr, string? videoUrl, string changedBy, DateTimeOffset atUtc)
    {
        TitleEn = titleEn;
        TitleAr = titleAr;
        BodyEn = bodyEn;
        BodyAr = bodyAr;
        VideoUrl = videoUrl;
        LastUpdatedByName = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        LastUpdatedAtUtc = atUtc;
    }

    public void FlagOutdated(string reason, DateTimeOffset atUtc)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("A reason is required to flag a guide as outdated.", nameof(reason));
        IsFlaggedOutdated = true;
        FlaggedReason = reason;
        FlaggedAtUtc = atUtc;
    }

    public void ClearOutdatedFlag()
    {
        IsFlaggedOutdated = false;
        FlaggedReason = null;
        FlaggedAtUtc = null;
    }

    // Story 29 adds SubmitForReview()/Publish()/Unpublish()/Archive() transition methods here.
}
```

**Create file: `src/SupportCrm.Domain/Entities/GuideAttachment.cs`** — identical shape to `ArticleAttachment` (Story 26), `GuideId` instead of `ArticleId`.

**Create file: `src/SupportCrm.Domain/Entities/GuideTicketCategory.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class GuideTicketCategory
{
    public Guid Id { get; private set; }
    public Guid GuideId { get; private set; }
    public Guid TicketCategoryId { get; private set; }

    private GuideTicketCategory() { } // EF Core

    public GuideTicketCategory(Guid guideId, Guid ticketCategoryId)
    {
        Id = Guid.NewGuid();
        GuideId = guideId;
        TicketCategoryId = ticketCategoryId;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IGuideRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IGuideRepository
{
    Task<Guide?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Guide>> GetAllAsync(bool includeUnpublished, CancellationToken ct);
    Task<IReadOnlyList<Guide>> GetByTicketCategoryAsync(Guid ticketCategoryId, bool includeUnpublished, CancellationToken ct);
    Task AddAsync(Guide guide, CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetLinkedTicketCategoryIdsAsync(Guid guideId, CancellationToken ct);
    Task AddCategoryLinkAsync(GuideTicketCategory link, CancellationToken ct);
    Task RemoveCategoryLinkAsync(Guid guideId, Guid ticketCategoryId, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IGuideAttachmentRepository.cs`** — identical shape to `IArticleAttachmentRepository` (Story 26), `Guide`/`GuideAttachment` instead of `Article`/`ArticleAttachment`.

**Create file: `src/SupportCrm.Application/KnowledgeBase/IGuideAttachmentStorage.cs`** — identical shape to `IArticleAttachmentStorage` (Story 26), `guideId` instead of `articleId`.

### 2 — Application: DTOs, exceptions, `GuideService`, `GuideAttachmentService`

**Create file: `src/SupportCrm.Application/KnowledgeBase/GuideDtos.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;

public record CreateGuideRequest(string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string? VideoUrl, string AuthorName, Guid EditorAgentId);
public record UpdateGuideRequest(string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string? VideoUrl, string ChangedBy, Guid EditorAgentId);
public record FlagGuideOutdatedRequest(string Reason);
public record GuideDto(Guid Id, string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string? VideoUrl, KbContentStatus Status, string AuthorName, string LastUpdatedByName, DateTimeOffset LastUpdatedAtUtc, bool IsFlaggedOutdated, string? FlaggedReason);
public record GuideAttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string UploadedByName, DateTimeOffset UploadedAtUtc);

public class GuideNotFoundException(string id) : Exception($"Guide '{id}' was not found.");
public class KbEditorRequiredException(Guid agentId) : Exception($"Agent '{agentId}' is not an authorized knowledge base editor.");
```

**Create file: `src/SupportCrm.Application/KnowledgeBase/GuideService.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class GuideService(IGuideRepository repository, IAgentRepository agentRepository, TimeProvider timeProvider)
{
    public async Task<GuideDto> CreateAsync(CreateGuideRequest request, CancellationToken ct)
    {
        await RequireEditorAsync(request.EditorAgentId, ct);
        var guide = new Guide(request.TitleEn?.Trim(), request.TitleAr?.Trim(), request.BodyEn?.Trim(), request.BodyAr?.Trim(),
            request.VideoUrl?.Trim(), request.AuthorName.Trim(), timeProvider.GetUtcNow());
        await repository.AddAsync(guide, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(guide);
    }

    public async Task<GuideDto> GetByIdAsync(Guid id, CancellationToken ct) =>
        ToDto(await repository.GetByIdAsync(id, ct) ?? throw new GuideNotFoundException(id.ToString()));

    public async Task<IReadOnlyList<GuideDto>> GetAllAsync(bool includeUnpublished, CancellationToken ct) =>
        (await repository.GetAllAsync(includeUnpublished, ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<GuideDto>> GetByTicketCategoryAsync(Guid ticketCategoryId, bool includeUnpublished, CancellationToken ct) =>
        (await repository.GetByTicketCategoryAsync(ticketCategoryId, includeUnpublished, ct)).Select(ToDto).ToList();

    public async Task<GuideDto> UpdateAsync(Guid id, UpdateGuideRequest request, CancellationToken ct)
    {
        await RequireEditorAsync(request.EditorAgentId, ct);
        var guide = await repository.GetByIdAsync(id, ct) ?? throw new GuideNotFoundException(id.ToString());
        guide.RecordUpdate(request.TitleEn?.Trim(), request.TitleAr?.Trim(), request.BodyEn?.Trim(), request.BodyAr?.Trim(), request.VideoUrl?.Trim(), request.ChangedBy, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(ct);
        return ToDto(guide);
    }

    // Flagging outdated is intentionally NOT editor-gated — any agent can raise the concern;
    // only an editor can act on it (via Story 29's workflow or a future un-flag/publish action).
    public async Task FlagOutdatedAsync(Guid id, FlagGuideOutdatedRequest request, CancellationToken ct)
    {
        var guide = await repository.GetByIdAsync(id, ct) ?? throw new GuideNotFoundException(id.ToString());
        guide.FlagOutdated(request.Reason, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(ct);
    }

    public async Task LinkCategoryAsync(Guid guideId, Guid ticketCategoryId, Guid editorAgentId, CancellationToken ct)
    {
        await RequireEditorAsync(editorAgentId, ct);
        _ = await repository.GetByIdAsync(guideId, ct) ?? throw new GuideNotFoundException(guideId.ToString());
        await repository.AddCategoryLinkAsync(new GuideTicketCategory(guideId, ticketCategoryId), ct);
        await repository.SaveChangesAsync(ct);
    }

    public async Task UnlinkCategoryAsync(Guid guideId, Guid ticketCategoryId, Guid editorAgentId, CancellationToken ct)
    {
        await RequireEditorAsync(editorAgentId, ct);
        await repository.RemoveCategoryLinkAsync(guideId, ticketCategoryId, ct);
        await repository.SaveChangesAsync(ct);
    }

    public Task<IReadOnlyList<Guid>> GetLinkedCategoriesAsync(Guid guideId, CancellationToken ct) =>
        repository.GetLinkedTicketCategoryIdsAsync(guideId, ct);

    private async Task RequireEditorAsync(Guid agentId, CancellationToken ct)
    {
        var agent = await agentRepository.GetByIdAsync(agentId, ct);
        if (agent is null || !agent.IsKnowledgeBaseEditor)
            throw new KbEditorRequiredException(agentId);
    }

    internal static GuideDto ToDto(Guide g) => new(g.Id, g.TitleEn, g.TitleAr, g.BodyEn, g.BodyAr, g.VideoUrl, g.Status, g.AuthorName, g.LastUpdatedByName, g.LastUpdatedAtUtc, g.IsFlaggedOutdated, g.FlaggedReason);
}
```

**Create file: `src/SupportCrm.Application/KnowledgeBase/GuideAttachmentService.cs`** — identical shape to `ArticleAttachmentService` (Story 26), operating on `IGuideRepository`/`IGuideAttachmentRepository`/`IGuideAttachmentStorage` and throwing `GuideNotFoundException`.

### 3 — Infrastructure: EF config, repositories, storage, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet`s after Story 26's:

```csharp
    public DbSet<Guide> Guides => Set<Guide>();
    public DbSet<GuideAttachment> GuideAttachments => Set<GuideAttachment>();
    public DbSet<GuideTicketCategory> GuideTicketCategories => Set<GuideTicketCategory>();
```

Extend the `Agent` block with one property line:

```csharp
            entity.Property(a => a.IsKnowledgeBaseEditor).IsRequired();
```

Add `OnModelCreating` blocks after Story 26's:

```csharp

        modelBuilder.Entity<Guide>(entity =>
        {
            entity.ToTable("Guides");
            entity.HasKey(g => g.Id);
            entity.Property(g => g.TitleEn).HasMaxLength(512);
            entity.Property(g => g.TitleAr).HasMaxLength(512);
            entity.Property(g => g.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(g => g.AuthorName).IsRequired().HasMaxLength(256);
            entity.Property(g => g.LastUpdatedByName).IsRequired().HasMaxLength(256);
            entity.Property(g => g.VideoUrl).HasMaxLength(1024);
            entity.HasIndex(g => g.Status);
        });

        modelBuilder.Entity<GuideAttachment>(entity =>
        {
            entity.ToTable("GuideAttachments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FileName).IsRequired().HasMaxLength(512);
            entity.Property(a => a.ContentType).IsRequired().HasMaxLength(128);
            entity.Property(a => a.StorageKey).IsRequired().HasMaxLength(1024);
            entity.Property(a => a.UploadedByName).IsRequired().HasMaxLength(256);
            entity.HasIndex(a => a.GuideId);
        });

        modelBuilder.Entity<GuideTicketCategory>(entity =>
        {
            entity.ToTable("GuideTicketCategories");
            entity.HasKey(l => l.Id);
            entity.HasIndex(l => new { l.GuideId, l.TicketCategoryId }).IsUnique();
        });
```

**Create files: `ArticleRepository`-equivalent `GuideRepository.cs`, `GuideAttachmentRepository.cs`** — mirror Story 26's `ArticleRepository`/`ArticleAttachmentRepository` exactly, plus in `GuideRepository`:

```csharp
    public async Task<IReadOnlyList<Guide>> GetByTicketCategoryAsync(Guid ticketCategoryId, bool includeUnpublished, CancellationToken ct)
    {
        var guideIds = dbContext.GuideTicketCategories.Where(l => l.TicketCategoryId == ticketCategoryId).Select(l => l.GuideId);
        var query = dbContext.Guides.Where(g => guideIds.Contains(g.Id));
        return await (includeUnpublished ? query : query.Where(g => g.Status == KbContentStatus.Published)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetLinkedTicketCategoryIdsAsync(Guid guideId, CancellationToken ct) =>
        await dbContext.GuideTicketCategories.Where(l => l.GuideId == guideId).Select(l => l.TicketCategoryId).ToListAsync(ct);

    public Task AddCategoryLinkAsync(GuideTicketCategory link, CancellationToken ct)
    {
        dbContext.GuideTicketCategories.Add(link);
        return Task.CompletedTask;
    }

    public async Task RemoveCategoryLinkAsync(Guid guideId, Guid ticketCategoryId, CancellationToken ct)
    {
        var link = await dbContext.GuideTicketCategories.FirstOrDefaultAsync(l => l.GuideId == guideId && l.TicketCategoryId == ticketCategoryId, ct);
        if (link is not null) dbContext.GuideTicketCategories.Remove(link);
    }
```

**Create file: `src/SupportCrm.Infrastructure/Storage/LocalDiskGuideAttachmentStorage.cs`** — mirror `LocalDiskArticleAttachmentStorage` (Story 26) exactly, `guideId`/`SectionName = "GuideAttachments"`/`RootPath = "App_Data/guide-attachments"`.

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<IGuideRepository, GuideRepository>();
        services.AddScoped<GuideService>();
        services.AddScoped<IGuideAttachmentRepository, GuideAttachmentRepository>();
        services.AddScoped<IGuideAttachmentStorage, LocalDiskGuideAttachmentStorage>();
        services.AddScoped<GuideAttachmentService>();
```

**File: `src/SupportCrm.Api/Program.cs`** — add, mirroring Story 26's registration:

```csharp
builder.Services.Configure<LocalDiskGuideAttachmentStorageOptions>(builder.Configuration.GetSection(LocalDiskGuideAttachmentStorageOptions.SectionName));
```

- After creating these files, run `dotnet ef migrations add AddGuides --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: `GuidesController`

**Create file: `src/SupportCrm.Api/Controllers/GuidesController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.KnowledgeBase;

[ApiController]
[Route("api/kb/guides")]
public class GuidesController(GuideService guideService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GuideDto>>> GetAll([FromQuery] Guid? ticketCategoryId, [FromQuery] bool includeUnpublished, CancellationToken ct) =>
        Ok(ticketCategoryId is null
            ? await guideService.GetAllAsync(includeUnpublished, ct)
            : await guideService.GetByTicketCategoryAsync(ticketCategoryId.Value, includeUnpublished, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GuideDto>> GetById(Guid id, CancellationToken ct)
    {
        try { return await guideService.GetByIdAsync(id, ct); }
        catch (GuideNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    public async Task<ActionResult<GuideDto>> Create([FromBody] CreateGuideRequest request, CancellationToken ct)
    {
        try { return await guideService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GuideDto>> Update(Guid id, [FromBody] UpdateGuideRequest request, CancellationToken ct)
    {
        try { return await guideService.UpdateAsync(id, request, ct); }
        catch (GuideNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/flag-outdated")]
    public async Task<IActionResult> FlagOutdated(Guid id, [FromBody] FlagGuideOutdatedRequest request, CancellationToken ct)
    {
        try { await guideService.FlagOutdatedAsync(id, request, ct); return NoContent(); }
        catch (GuideNotFoundException) { return NotFound(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:guid}/categories")]
    public async Task<ActionResult<IReadOnlyList<Guid>>> GetLinkedCategories(Guid id, CancellationToken ct) =>
        Ok(await guideService.GetLinkedCategoriesAsync(id, ct));

    [HttpPost("{id:guid}/categories/{ticketCategoryId:guid}")]
    public async Task<IActionResult> LinkCategory(Guid id, Guid ticketCategoryId, [FromQuery] Guid editorAgentId, CancellationToken ct)
    {
        try { await guideService.LinkCategoryAsync(id, ticketCategoryId, editorAgentId, ct); return NoContent(); }
        catch (GuideNotFoundException) { return NotFound(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpDelete("{id:guid}/categories/{ticketCategoryId:guid}")]
    public async Task<IActionResult> UnlinkCategory(Guid id, Guid ticketCategoryId, [FromQuery] Guid editorAgentId, CancellationToken ct)
    {
        try { await guideService.UnlinkCategoryAsync(id, ticketCategoryId, editorAgentId, ct); return NoContent(); }
        catch (KbEditorRequiredException) { return Forbid(); }
    }

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<GuideAttachmentDto>> UploadAttachment(Guid id, IFormFile file, [FromQuery] string? uploadedByName, [FromServices] GuideAttachmentService attachmentService, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("A file is required.");
        try
        {
            await using var stream = file.OpenReadStream();
            return await attachmentService.AddAsync(id, file.FileName, file.ContentType, file.Length, stream, uploadedByName ?? "unknown", ct);
        }
        catch (GuideNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IReadOnlyList<GuideAttachmentDto>>> GetAttachments(Guid id, [FromServices] GuideAttachmentService attachmentService, CancellationToken ct) =>
        Ok(await attachmentService.GetForArticleAsync(id, ct));

    [HttpGet("attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, [FromServices] GuideAttachmentService attachmentService, CancellationToken ct)
    {
        try
        {
            var (content, attachment) = await attachmentService.OpenAsync(attachmentId, ct);
            return File(content, attachment.ContentType, attachment.FileName);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
```

---

## Frontend Tasks

**Implemented** (`d:\Code\selfAssessment\frontend\src\app`):

- **File: `features/knowledge-base/kb.model.ts`** — `Guide`, `CreateGuideRequest`.
- **File: `features/knowledge-base/kb.service.ts`** — `getGuides`/`createGuide`, `flagGuideOutdated`, `getLinkedTicketCategories`/`linkTicketCategory`/`unlinkTicketCategory`.
- **Create file: `features/knowledge-base/guides/guides.component.{ts,html,scss}`** — ticket-category filter + "include drafts/archived" toggle, guide cards (status badge, video link, outdated badge, linked-category chips with inline link/unlink, flag-outdated input), bilingual create form. Editor actions (create, link/unlink, and Story 29's workflow buttons) require an "acting as" agent selected via the existing navbar picker and surface a 403-specific error message if that agent isn't `IsKnowledgeBaseEditor`. Route: `/admin/kb-guides`.
- **File: `features/tickets/ticket.model.ts`** — added `Agent.isKnowledgeBaseEditor`.
- **File: `features/tickets/ticket.service.ts`** — added `setAgentKnowledgeBaseEditor`.
- **File: `features/agent-dashboard/agent-admin/agent-admin.component.{ts,html}`** — added a "KB editor" toggle per agent.
- **File: `app.routes.ts`**, **`layout/app-shell/app-shell.component.ts`** — route + sidebar nav entry ("Solution guides").

*Not built:* a single-guide detail/edit view (`GET`/`PUT /api/kb/guides/{id}`) and `GuideAttachment` upload/download (`POST`/`GET /api/kb/guides/{id}/attachments`, `GET /api/kb/guides/attachments/{attachmentId}/download`) — the list page covers create/flag/link, but editing an existing guide's title/body/video URL and attaching screenshots have no UI yet, same gap as Story 26's article attachments. Flagged as a follow-up.

---

## Edge Cases & Failure Modes

- **`CreateAsync`/`UpdateAsync`/`LinkCategoryAsync`/`UnlinkCategoryAsync` called with an agent id that doesn't exist, or exists but `IsKnowledgeBaseEditor == false`** — `RequireEditorAsync` throws `KbEditorRequiredException` → `403` via each controller action's catch, before any write happens.
- **`FlagOutdatedAsync` with a blank reason** — rejected by `Guide.FlagOutdated`'s guard (`ArgumentException` → `400`); flagging itself is not editor-gated (see `GuideService`'s doc comment) — any agent can flag, only an editor can subsequently act.
- **Linking the same `(GuideId, TicketCategoryId)` pair twice** — rejected at the database level by the unique index; not caught explicitly at the service layer in this story (same "not handled here, flagged" pattern as SLA & Automation's `AddSkillAsync` duplicate case).
- **Unlinking a category that was never linked** — `RemoveCategoryLinkAsync`'s `if (link is not null)` guard makes this a silent no-op, not an error.
- **`GetByTicketCategoryAsync` for a `TicketCategoryId` with zero linked guides** — returns an empty list, not an error (no existence check on the category id itself — this endpoint tolerates an unknown/deleted category id gracefully).

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/GuideTests.cs`**:
   - `FlagOutdated_BlankReason_Throws`
2. **Unit — `tests/SupportCrm.Application.Tests/KnowledgeBase/GuideServiceTests.cs`**:
   - `CreateAsync_NonEditorAgent_ThrowsKbEditorRequired`
   - `FlagOutdatedAsync_NonEditorAgent_Succeeds` (flagging is unrestricted)
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/GuidesControllerTests.cs`**:
   - `Post_AsNonEditor_Returns403`
   - `Post_LinkCategory_Twice_ReturnsErrorOnSecondCall`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddGuides --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] Guides hold bilingual title/body, screenshots, and an external video URL.
- [ ] Guides link to one or more `TicketCategory` rows (`POST`/`DELETE /api/kb/guides/{id}/categories/{ticketCategoryId}`).
- [ ] Any agent can flag a guide outdated; only `IsKnowledgeBaseEditor` agents can create/modify/link.
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 28.**
