# Story 26 — Help articles (Story: KB-2)

---

## Prerequisites

- Story 25 completed: [`25-story-KB-1.md`](25-story-KB-1.md) — provides `KbCategory`, the bilingual `*En`/`*Ar` field pattern, and the helpful/not-helpful counter pattern, all reused here.

---

## Story Goal

1. `Article`s hold rich-text body content (text + step-by-step instructions as ordinary formatted text) plus separate `ArticleAttachment` image uploads.
2. Every article tracks `LastUpdatedAtUtc`/`LastUpdatedByName` and an `AuthorName`, and a shared `KbContentStatus` (`Draft`/`Published`/`Archived`) gates public visibility — only `Published` articles are returned to unauthenticated/customer-facing reads.
3. View counts increment on single-article reads (not list reads); helpful/not-helpful counters reuse Story 25's pattern.
4. Agents can reference a stable article URL in a ticket reply — no new reply-side endpoint is needed, since Communication Channels' reply endpoints already accept free-text bodies.

**Not in scope:** FAQs (Story 25, done) and solution guides (Story 27). Search across content types (Story 28). Full version-snapshot history and authorized draft/review/publish transitions — this story only defines the `KbContentStatus` enum and gates visibility by it; Story 29 adds the transition endpoints and version history.

---

## Context — Read These Files First

1. `src/SupportCrm.Domain/Entities/TicketAttachment.cs` (all 33 lines) — the exact shape `ArticleAttachment` mirrors (`Id`, owner FK, `FileName`, `ContentType`, `SizeBytes`, `StorageKey`, `UploadedByName`, `UploadedAtUtc`).
2. `src/SupportCrm.Application/Tickets/TicketAttachmentService.cs` (all 37 lines) — the exact service shape `ArticleAttachmentService` mirrors (`AddAsync` saves to storage then persists the row; `OpenAsync` re-opens by stored key).
3. `src/SupportCrm.Infrastructure/Storage/LocalDiskTicketAttachmentStorage.cs` (all 35 lines) — the exact per-owner-folder storage shape `LocalDiskArticleAttachmentStorage` mirrors (`{RootPath}/{articleId}/{attachmentId}_{fileName}`).
4. `src/SupportCrm.Api/Controllers/TicketsController.cs`, lines 220–246 (`UploadAttachment`/`GetAttachments`/`DownloadAttachment`) — the exact controller-action shape to mirror on `ArticlesController` for `ArticleAttachment`.
5. `src/SupportCrm.Api/Program.cs`, lines 1–30 — `builder.Services.Configure<LocalDiskTicketAttachmentStorageOptions>(...)` (near line 29) is the precedent for registering `LocalDiskArticleAttachmentStorageOptions`'s config section the same way.
6. [`25-story-KB-1.md`](25-story-KB-1.md), `## Backend Tasks` → `### 1`/`### 2` — `KbCategory`, and the `KnowledgeBase` Application folder convention this story continues.

---

## Backend Tasks

### 1 — Domain: `KbContentStatus`, `Article`, `ArticleAttachment`

**Create file: `src/SupportCrm.Domain/Entities/KbContentStatus.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// Shared by Article (this story) and Guide (Story 27). Story 29 adds UnderReview and the
// authorized transition endpoints; this story only needs the tri-state to exist and to gate
// public visibility (only Published is publicly readable).
public enum KbContentStatus
{
    Draft,
    Published,
    Archived
}
```

**Create file: `src/SupportCrm.Domain/Entities/Article.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class Article
{
    public Guid Id { get; private set; }
    public Guid? KbCategoryId { get; private set; }
    public string? TitleEn { get; private set; }
    public string? TitleAr { get; private set; }
    public string? BodyEn { get; private set; }
    public string? BodyAr { get; private set; }
    public KbContentStatus Status { get; private set; } = KbContentStatus.Draft;
    public string AuthorName { get; private set; } = default!;
    public string LastUpdatedByName { get; private set; } = default!;
    public DateTimeOffset LastUpdatedAtUtc { get; private set; }
    public int ViewCount { get; private set; }
    public int HelpfulCount { get; private set; }
    public int NotHelpfulCount { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Article() { } // EF Core

    public Article(Guid? kbCategoryId, string? titleEn, string? titleAr, string? bodyEn, string? bodyAr, string authorName, DateTimeOffset createdAtUtc)
    {
        var hasEnglish = !string.IsNullOrWhiteSpace(titleEn) && !string.IsNullOrWhiteSpace(bodyEn);
        var hasArabic = !string.IsNullOrWhiteSpace(titleAr) && !string.IsNullOrWhiteSpace(bodyAr);
        if (!hasEnglish && !hasArabic)
            throw new ArgumentException("A title+body pair is required in at least one language.", nameof(titleEn));
        if (string.IsNullOrWhiteSpace(authorName))
            throw new ArgumentException("Author name is required.", nameof(authorName));

        Id = Guid.NewGuid();
        KbCategoryId = kbCategoryId;
        TitleEn = titleEn;
        TitleAr = titleAr;
        BodyEn = bodyEn;
        BodyAr = bodyAr;
        AuthorName = authorName;
        LastUpdatedByName = authorName;
        LastUpdatedAtUtc = createdAtUtc;
        CreatedAtUtc = createdAtUtc;
    }

    public void RecordUpdate(string? titleEn, string? titleAr, string? bodyEn, string? bodyAr, string changedBy, DateTimeOffset atUtc)
    {
        TitleEn = titleEn;
        TitleAr = titleAr;
        BodyEn = bodyEn;
        BodyAr = bodyAr;
        LastUpdatedByName = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        LastUpdatedAtUtc = atUtc;
    }

    public void IncrementViewCount() => ViewCount++;
    public void MarkHelpful() => HelpfulCount++;
    public void MarkNotHelpful() => NotHelpfulCount++;

    // Story 29 adds SubmitForReview()/Publish()/Unpublish()/Archive() transition methods here.
}
```

**Create file: `src/SupportCrm.Domain/Entities/ArticleAttachment.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class ArticleAttachment
{
    public Guid Id { get; private set; }
    public Guid ArticleId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = default!;
    public string UploadedByName { get; private set; } = default!;
    public DateTimeOffset UploadedAtUtc { get; private set; }

    private ArticleAttachment() { } // EF Core

    public ArticleAttachment(Guid articleId, string fileName, string contentType, long sizeBytes, string storageKey, string uploadedByName, DateTimeOffset uploadedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        if (sizeBytes <= 0)
            throw new ArgumentException("File size must be positive.", nameof(sizeBytes));

        Id = Guid.NewGuid();
        ArticleId = articleId;
        FileName = fileName;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        UploadedByName = string.IsNullOrWhiteSpace(uploadedByName) ? "unknown" : uploadedByName;
        UploadedAtUtc = uploadedAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IArticleRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IArticleRepository
{
    Task<Article?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Article>> GetAllAsync(bool includeUnpublished, CancellationToken ct);
    Task<IReadOnlyList<Article>> GetByCategoryAsync(Guid kbCategoryId, bool includeUnpublished, CancellationToken ct);
    Task AddAsync(Article article, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IArticleAttachmentRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IArticleAttachmentRepository
{
    Task<ArticleAttachment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ArticleAttachment>> GetByArticleAsync(Guid articleId, CancellationToken ct);
    Task AddAsync(ArticleAttachment attachment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/KnowledgeBase/IArticleAttachmentStorage.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

public interface IArticleAttachmentStorage
{
    Task<string> SaveAsync(Guid articleId, Guid attachmentId, string fileName, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
}
```

### 2 — Application: DTOs, `ArticleService`, `ArticleAttachmentService`

**Create file: `src/SupportCrm.Application/KnowledgeBase/ArticleDtos.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;

public record CreateArticleRequest(Guid? KbCategoryId, string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string AuthorName);
public record UpdateArticleRequest(string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, string ChangedBy);
public record ArticleDto(Guid Id, Guid? KbCategoryId, string? TitleEn, string? TitleAr, string? BodyEn, string? BodyAr, KbContentStatus Status, string AuthorName, string LastUpdatedByName, DateTimeOffset LastUpdatedAtUtc, int ViewCount, int HelpfulCount, int NotHelpfulCount);
public record ArticleAttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string UploadedByName, DateTimeOffset UploadedAtUtc);

public class ArticleNotFoundException(string id) : Exception($"Article '{id}' was not found.");
```

**Create file: `src/SupportCrm.Application/KnowledgeBase/ArticleService.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ArticleService(IArticleRepository repository, TimeProvider timeProvider)
{
    public async Task<ArticleDto> CreateAsync(CreateArticleRequest request, CancellationToken ct)
    {
        var article = new Article(request.KbCategoryId, request.TitleEn?.Trim(), request.TitleAr?.Trim(),
            request.BodyEn?.Trim(), request.BodyAr?.Trim(), request.AuthorName.Trim(), timeProvider.GetUtcNow());
        await repository.AddAsync(article, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(article);
    }

    // Increments the view count — call only from the single-article read, not list reads.
    public async Task<ArticleDto> GetByIdAndTrackViewAsync(Guid id, CancellationToken ct)
    {
        var article = await repository.GetByIdAsync(id, ct) ?? throw new ArticleNotFoundException(id.ToString());
        article.IncrementViewCount();
        await repository.SaveChangesAsync(ct);
        return ToDto(article);
    }

    public async Task<IReadOnlyList<ArticleDto>> GetAllAsync(bool includeUnpublished, CancellationToken ct) =>
        (await repository.GetAllAsync(includeUnpublished, ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<ArticleDto>> GetByCategoryAsync(Guid kbCategoryId, bool includeUnpublished, CancellationToken ct) =>
        (await repository.GetByCategoryAsync(kbCategoryId, includeUnpublished, ct)).Select(ToDto).ToList();

    public async Task<ArticleDto> UpdateAsync(Guid id, UpdateArticleRequest request, CancellationToken ct)
    {
        var article = await repository.GetByIdAsync(id, ct) ?? throw new ArticleNotFoundException(id.ToString());
        article.RecordUpdate(request.TitleEn?.Trim(), request.TitleAr?.Trim(), request.BodyEn?.Trim(), request.BodyAr?.Trim(), request.ChangedBy, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(ct);
        return ToDto(article);
    }

    public async Task MarkHelpfulAsync(Guid id, CancellationToken ct)
    {
        var article = await repository.GetByIdAsync(id, ct) ?? throw new ArticleNotFoundException(id.ToString());
        article.MarkHelpful();
        await repository.SaveChangesAsync(ct);
    }

    public async Task MarkNotHelpfulAsync(Guid id, CancellationToken ct)
    {
        var article = await repository.GetByIdAsync(id, ct) ?? throw new ArticleNotFoundException(id.ToString());
        article.MarkNotHelpful();
        await repository.SaveChangesAsync(ct);
    }

    internal static ArticleDto ToDto(Article a) => new(a.Id, a.KbCategoryId, a.TitleEn, a.TitleAr, a.BodyEn, a.BodyAr, a.Status, a.AuthorName, a.LastUpdatedByName, a.LastUpdatedAtUtc, a.ViewCount, a.HelpfulCount, a.NotHelpfulCount);
}
```

**Create file: `src/SupportCrm.Application/KnowledgeBase/ArticleAttachmentService.cs`**

```csharp
namespace SupportCrm.Application.KnowledgeBase;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ArticleAttachmentService(
    IArticleRepository articleRepository,
    IArticleAttachmentRepository attachmentRepository,
    IArticleAttachmentStorage storage,
    TimeProvider timeProvider)
{
    public async Task<ArticleAttachmentDto> AddAsync(Guid articleId, string fileName, string contentType, long sizeBytes, Stream content, string uploadedByName, CancellationToken ct)
    {
        _ = await articleRepository.GetByIdAsync(articleId, ct) ?? throw new ArticleNotFoundException(articleId.ToString());

        var attachmentId = Guid.NewGuid();
        var storageKey = await storage.SaveAsync(articleId, attachmentId, fileName, content, ct);

        var attachment = new ArticleAttachment(articleId, fileName, contentType, sizeBytes, storageKey, uploadedByName, timeProvider.GetUtcNow());
        await attachmentRepository.AddAsync(attachment, ct);
        await attachmentRepository.SaveChangesAsync(ct);
        return ToDto(attachment);
    }

    public async Task<IReadOnlyList<ArticleAttachmentDto>> GetForArticleAsync(Guid articleId, CancellationToken ct) =>
        (await attachmentRepository.GetByArticleAsync(articleId, ct)).Select(ToDto).ToList();

    public async Task<(Stream Content, ArticleAttachment Attachment)> OpenAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await attachmentRepository.GetByIdAsync(attachmentId, ct) ?? throw new KeyNotFoundException($"Attachment '{attachmentId}' was not found.");
        var stream = await storage.OpenReadAsync(attachment.StorageKey, ct);
        return (stream, attachment);
    }

    private static ArticleAttachmentDto ToDto(ArticleAttachment a) => new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByName, a.UploadedAtUtc);
}
```

### 3 — Infrastructure: EF config, repositories, storage, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet`s after Story 25's:

```csharp
    public DbSet<Article> Articles => Set<Article>();
    public DbSet<ArticleAttachment> ArticleAttachments => Set<ArticleAttachment>();
```

Add `OnModelCreating` blocks after Story 25's:

```csharp

        modelBuilder.Entity<Article>(entity =>
        {
            entity.ToTable("Articles");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.TitleEn).HasMaxLength(512);
            entity.Property(a => a.TitleAr).HasMaxLength(512);
            entity.Property(a => a.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(a => a.AuthorName).IsRequired().HasMaxLength(256);
            entity.Property(a => a.LastUpdatedByName).IsRequired().HasMaxLength(256);
            entity.HasIndex(a => a.KbCategoryId);
            entity.HasIndex(a => a.Status);
        });

        modelBuilder.Entity<ArticleAttachment>(entity =>
        {
            entity.ToTable("ArticleAttachments");
            entity.HasKey(a => a.Id);
            entity.Property(a => a.FileName).IsRequired().HasMaxLength(512);
            entity.Property(a => a.ContentType).IsRequired().HasMaxLength(128);
            entity.Property(a => a.StorageKey).IsRequired().HasMaxLength(1024);
            entity.Property(a => a.UploadedByName).IsRequired().HasMaxLength(256);
            entity.HasIndex(a => a.ArticleId);
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/ArticleRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ArticleRepository(SupportCrmDbContext dbContext) : IArticleRepository
{
    public Task<Article?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Articles.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<Article>> GetAllAsync(bool includeUnpublished, CancellationToken ct) =>
        await Filter(dbContext.Articles, includeUnpublished).ToListAsync(ct);

    public async Task<IReadOnlyList<Article>> GetByCategoryAsync(Guid kbCategoryId, bool includeUnpublished, CancellationToken ct) =>
        await Filter(dbContext.Articles.Where(a => a.KbCategoryId == kbCategoryId), includeUnpublished).ToListAsync(ct);

    private static IQueryable<Article> Filter(IQueryable<Article> query, bool includeUnpublished) =>
        includeUnpublished ? query : query.Where(a => a.Status == KbContentStatus.Published);

    public Task AddAsync(Article article, CancellationToken ct)
    {
        dbContext.Articles.Add(article);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/ArticleAttachmentRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ArticleAttachmentRepository(SupportCrmDbContext dbContext) : IArticleAttachmentRepository
{
    public Task<ArticleAttachment?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.ArticleAttachments.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<IReadOnlyList<ArticleAttachment>> GetByArticleAsync(Guid articleId, CancellationToken ct) =>
        await dbContext.ArticleAttachments.Where(a => a.ArticleId == articleId).ToListAsync(ct);

    public Task AddAsync(ArticleAttachment attachment, CancellationToken ct)
    {
        dbContext.ArticleAttachments.Add(attachment);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**Create file: `src/SupportCrm.Infrastructure/Storage/LocalDiskArticleAttachmentStorage.cs`**

```csharp
namespace SupportCrm.Infrastructure.Storage;

using Microsoft.Extensions.Options;
using SupportCrm.Application.KnowledgeBase;

public class LocalDiskArticleAttachmentStorageOptions
{
    public const string SectionName = "ArticleAttachments";
    public string RootPath { get; set; } = "App_Data/article-attachments";
}

public class LocalDiskArticleAttachmentStorage(IOptions<LocalDiskArticleAttachmentStorageOptions> options) : IArticleAttachmentStorage
{
    public async Task<string> SaveAsync(Guid articleId, Guid attachmentId, string fileName, Stream content, CancellationToken ct)
    {
        var articleDir = Path.Combine(options.Value.RootPath, articleId.ToString());
        Directory.CreateDirectory(articleDir);

        var storageFileName = $"{attachmentId}_{Path.GetFileName(fileName)}";
        var storageKey = Path.Combine(articleId.ToString(), storageFileName);
        var fullPath = Path.Combine(articleDir, storageFileName);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct)
    {
        var fullPath = Path.Combine(options.Value.RootPath, storageKey);
        return Task.FromResult<Stream>(File.OpenRead(fullPath));
    }
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<IArticleRepository, ArticleRepository>();
        services.AddScoped<ArticleService>();
        services.AddScoped<IArticleAttachmentRepository, ArticleAttachmentRepository>();
        services.AddScoped<IArticleAttachmentStorage, LocalDiskArticleAttachmentStorage>();
        services.AddScoped<ArticleAttachmentService>();
```

**File: `src/SupportCrm.Api/Program.cs`** — add near the existing `Configure<LocalDiskTicketAttachmentStorageOptions>` call:

```csharp
builder.Services.Configure<LocalDiskArticleAttachmentStorageOptions>(builder.Configuration.GetSection(LocalDiskArticleAttachmentStorageOptions.SectionName));
```

(Add `using SupportCrm.Infrastructure.Storage;` — already present in this file for the other storage options types.)

- After creating these files, run `dotnet ef migrations add AddArticles --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: `ArticlesController`

**Create file: `src/SupportCrm.Api/Controllers/ArticlesController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.KnowledgeBase;

[ApiController]
[Route("api/kb/articles")]
public class ArticlesController(ArticleService articleService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ArticleDto>>> GetAll([FromQuery] Guid? categoryId, [FromQuery] bool includeUnpublished, CancellationToken ct) =>
        Ok(categoryId is null
            ? await articleService.GetAllAsync(includeUnpublished, ct)
            : await articleService.GetByCategoryAsync(categoryId.Value, includeUnpublished, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ArticleDto>> GetById(Guid id, CancellationToken ct)
    {
        try { return await articleService.GetByIdAndTrackViewAsync(id, ct); }
        catch (ArticleNotFoundException) { return NotFound(); }
    }

    [HttpPost]
    public async Task<ActionResult<ArticleDto>> Create([FromBody] CreateArticleRequest request, CancellationToken ct)
    {
        try { return await articleService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ArticleDto>> Update(Guid id, [FromBody] UpdateArticleRequest request, CancellationToken ct)
    {
        try { return await articleService.UpdateAsync(id, request, ct); }
        catch (ArticleNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/helpful")]
    public async Task<IActionResult> MarkHelpful(Guid id, CancellationToken ct)
    {
        try { await articleService.MarkHelpfulAsync(id, ct); return NoContent(); }
        catch (ArticleNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/not-helpful")]
    public async Task<IActionResult> MarkNotHelpful(Guid id, CancellationToken ct)
    {
        try { await articleService.MarkNotHelpfulAsync(id, ct); return NoContent(); }
        catch (ArticleNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ArticleAttachmentDto>> UploadAttachment(Guid id, IFormFile file, [FromQuery] string? uploadedByName, [FromServices] ArticleAttachmentService attachmentService, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("A file is required.");
        try
        {
            await using var stream = file.OpenReadStream();
            return await attachmentService.AddAsync(id, file.FileName, file.ContentType, file.Length, stream, uploadedByName ?? "unknown", ct);
        }
        catch (ArticleNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IReadOnlyList<ArticleAttachmentDto>>> GetAttachments(Guid id, [FromServices] ArticleAttachmentService attachmentService, CancellationToken ct) =>
        Ok(await attachmentService.GetForArticleAsync(id, ct));

    [HttpGet("attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, [FromServices] ArticleAttachmentService attachmentService, CancellationToken ct)
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

- **File: `features/knowledge-base/kb.model.ts`** — `Article`, `CreateArticleRequest`, `UpdateArticleRequest`, `KbContentStatus`.
- **File: `features/knowledge-base/kb.service.ts`** — `getArticles`/`getArticle`/`createArticle`/`updateArticle`, `markArticleHelpful`/`markArticleNotHelpful`.
- **Create file: `features/knowledge-base/articles/articles.component.{ts,html,scss}`** — category filter + "include drafts/archived" toggle, article cards (status badge, view count, helpful/not-helpful buttons, bilingual body), bilingual create form. Route: `/admin/kb-articles`. Story 29's workflow buttons and version history are on this same component (see that story's Frontend Tasks) rather than a separate page — they act on the article the card already displays.
- **File: `app.routes.ts`**, **`layout/app-shell/app-shell.component.ts`** — route + sidebar nav entry ("Help articles").

*Not built:* an image-upload UI for `ArticleAttachment` (`POST /api/kb/articles/{id}/attachments`) — the backend endpoint exists (mirrors `TicketAttachmentsComponent`'s file-input pattern) but no frontend control calls it yet. Flagged as a follow-up; body text can still reference an externally-hosted image URL in the meantime. "Insert a link into a ticket reply" needs no frontend work per this story's own intake note — agents copy the article's `/admin/kb-articles` URL into the existing ticket reply composer.

---

## Edge Cases & Failure Modes

- **`Article` created with no complete title+body pair in either language** — rejected by the constructor (`ArgumentException` → `400`).
- **`GetByIdAndTrackViewAsync` called on an unknown id** — `ArticleNotFoundException` → `404`; view count is not incremented (the guard runs before `IncrementViewCount`).
- **`GetAll`/`GetByCategory` without `includeUnpublished`** — only `Published` articles return, per `ArticleRepository.Filter`; `Draft`/`Archived` articles are invisible to the default (customer-facing) read.
- **Uploading an attachment to an unknown article** — `ArticleNotFoundException` → `404`, same as `TicketAttachmentService`'s precedent; no orphaned file is written since the existence check runs before `storage.SaveAsync`.
- **Downloading an attachment whose backing file was deleted from disk out-of-band** — `File.OpenRead` throws `FileNotFoundException`, unhandled by this story (matches `TicketAttachmentService`'s existing behavior — not a new gap introduced here).
- **`UpdateAsync` clearing both languages' title/body** — not rejected at this layer (unlike `Create`'s dual-language guard); this story keeps `Update` permissive to allow incremental edits (e.g. clearing Arabic content temporarily while it's rewritten) — flagged as an intentional asymmetry, not an oversight.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/ArticleTests.cs`**:
   - `Constructor_NoLanguagePairComplete_Throws`
   - `IncrementViewCount_IncrementsCounter`
2. **Unit — `tests/SupportCrm.Application.Tests/KnowledgeBase/ArticleServiceTests.cs`**:
   - `GetByIdAndTrackViewAsync_UnknownId_DoesNotIncrementAnything`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/ArticlesControllerTests.cs`**:
   - `Get_All_DefaultExcludesUnpublished`
   - `Post_ArticleWithNoContent_Returns400`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddArticles --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] Articles hold bilingual title/body text plus image attachments (`POST /api/kb/articles/{id}/attachments`).
- [ ] `LastUpdatedAtUtc`/`LastUpdatedByName`/`AuthorName` are tracked; `KbContentStatus` gates public visibility.
- [ ] View counts increment on single-article reads only; helpful/not-helpful counters work.
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 27.**
