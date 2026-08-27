# Story 04 — Notes and attachments (Story: CM-4)

---

## Prerequisites

- Story 01 completed: [`01-story-CM-1.md`](01-story-CM-1.md) — provides the `Customer` aggregate.
- Story 03 completed: [`03-story-CM-3.md`](03-story-CM-3.md) — provides the `ICustomerInteractionSource` seam this story registers its first real implementation into. If Story 03 has not been implemented yet, skip `## Backend Tasks` → `### 4` (the timeline-source registration) and revisit it once Story 03 lands — everything else in this story is independent.

---

## Story Goal

Support agents can:

1. Add a free-text internal note to a customer profile (never visible to the customer — there is no customer-facing surface in this codebase to leak through, but keep notes on an agent-only endpoint regardless).
2. Pin a note so it sorts to the top of the notes list, independent of timestamp.
3. Attach files to a customer profile (up to a **configurable** size limit), and preview/download them later.
4. See the author's name and timestamp on every note and attachment.

**Assumption (no auth exists yet, same as Story 02):** author identity is accepted as a client-supplied `AuthorName` field, not resolved from a token. Flag this the same way Story 02 does.

**File storage (greenfield decision, per the intake):** store attachment bytes on local disk under a configured folder, behind an `IAttachmentStorage` seam so the backend can be swapped to blob storage later without touching `NoteAndAttachmentService` or the controller.

---

## Context — Read These Files First

1. [`01-story-CM-1.md`](01-story-CM-1.md), `## Backend Tasks` → `### 1`/`### 2` — entity and service patterns to follow (private setters, validating constructors, primary-constructor services).
2. [`03-story-CM-3.md`](03-story-CM-3.md), `## Backend Tasks` → `### 1` — the `ICustomerInteractionSource` interface this story's `NotesInteractionSource` implements, and `CustomerInteractionDto`'s shape (`Channel`, `OccurredAtUtc`, `Summary`, `AgentName`, `SourceUrl`).
3. `src/SupportCrm.Infrastructure/DependencyInjection.cs` (24 lines as of Story 01; extended by Stories 02–03) — registration style; this story adds `services.AddScoped<ICustomerInteractionSource, NotesInteractionSource>()` here, which is additive to whatever Story 03 already registered (or didn't).
4. `src/SupportCrm.Api/Controllers/CustomersController.cs` (51 lines, whole file) — controller pattern. This story adds a new `NotesController` and `AttachmentsController` rather than growing `CustomersController`, matching the one-controller-per-concern split from Stories 02–03.
5. `src/SupportCrm.Api/appsettings.json` (as extended by Story 01 with `ConnectionStrings:Default`) — add an `Attachments` section here for the configurable size limit and storage root.

---

## Backend Tasks

### 1 — Domain: `CustomerNote`, `CustomerAttachment`

**Create file: `src/SupportCrm.Domain/Entities/CustomerNote.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class CustomerNote
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Text { get; private set; } = default!;
    public string AuthorName { get; private set; } = default!;
    public bool IsPinned { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private CustomerNote() { } // EF Core

    public CustomerNote(Guid customerId, string text, string authorName, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Note text is required.", nameof(text));

        Id = Guid.NewGuid();
        CustomerId = customerId;
        Text = text;
        AuthorName = string.IsNullOrWhiteSpace(authorName) ? "unknown" : authorName;
        CreatedAtUtc = createdAtUtc;
    }

    public void SetPinned(bool isPinned) => IsPinned = isPinned;
}
```

**Create file: `src/SupportCrm.Domain/Entities/CustomerAttachment.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class CustomerAttachment
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = default!;
    public string UploadedByName { get; private set; } = default!;
    public DateTimeOffset UploadedAtUtc { get; private set; }

    private CustomerAttachment() { } // EF Core

    public CustomerAttachment(Guid customerId, string fileName, string contentType, long sizeBytes, string storageKey, string uploadedByName, DateTimeOffset uploadedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        if (sizeBytes <= 0)
            throw new ArgumentException("File size must be positive.", nameof(sizeBytes));

        Id = Guid.NewGuid();
        CustomerId = customerId;
        FileName = fileName;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        UploadedByName = string.IsNullOrWhiteSpace(uploadedByName) ? "unknown" : uploadedByName;
        UploadedAtUtc = uploadedAtUtc;
    }
}
```

### 2 — Application: storage seam, DTOs, service, timeline source

**Create file: `src/SupportCrm.Application/Customers/IAttachmentStorage.cs`**

```csharp
namespace SupportCrm.Application.Customers;

/// <summary>
/// Persists attachment bytes. The default registration (<see cref="LocalDiskAttachmentStorage"/>,
/// in SupportCrm.Infrastructure) writes to local disk — swap the DI registration for a blob-storage
/// implementation later without touching <see cref="NoteAndAttachmentService"/> or its controller.
/// </summary>
public interface IAttachmentStorage
{
    Task<string> SaveAsync(Guid customerId, Guid attachmentId, string fileName, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Customers/NoteAndAttachmentDtos.cs`**

```csharp
namespace SupportCrm.Application.Customers;

public record AddNoteRequest(string Text, string AuthorName, bool IsPinned);
public record SetNotePinnedRequest(bool IsPinned);

public record NoteDto(Guid Id, string Text, string AuthorName, bool IsPinned, DateTimeOffset CreatedAtUtc);

public record AttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string UploadedByName, DateTimeOffset UploadedAtUtc);

public class AttachmentTooLargeException(long sizeBytes, long maxSizeBytes)
    : Exception($"Attachment size {sizeBytes} bytes exceeds the configured limit of {maxSizeBytes} bytes.");
```

**Create file: `src/SupportCrm.Application/Customers/AttachmentOptions.cs`**

```csharp
namespace SupportCrm.Application.Customers;

public class AttachmentOptions
{
    public const string SectionName = "Attachments";

    public long MaxSizeBytes { get; set; } = 10 * 1024 * 1024; // 10 MB default
}
```

**Create file: `src/SupportCrm.Application/Customers/INoteAndAttachmentRepository.cs`**

```csharp
namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;

public interface INoteAndAttachmentRepository
{
    Task<IReadOnlyList<CustomerNote>> GetNotesAsync(Guid customerId, CancellationToken ct);
    Task<CustomerNote?> GetNoteByIdAsync(Guid noteId, CancellationToken ct);
    Task AddNoteAsync(CustomerNote note, CancellationToken ct);

    Task<IReadOnlyList<CustomerAttachment>> GetAttachmentsAsync(Guid customerId, CancellationToken ct);
    Task<CustomerAttachment?> GetAttachmentByIdAsync(Guid attachmentId, CancellationToken ct);
    Task AddAttachmentAsync(CustomerAttachment attachment, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Customers/NoteAndAttachmentService.cs`**

```csharp
namespace SupportCrm.Application.Customers;

using Microsoft.Extensions.Options;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class NoteAndAttachmentService(
    ICustomerRepository customerRepository,
    INoteAndAttachmentRepository repository,
    IAttachmentStorage attachmentStorage,
    IOptions<AttachmentOptions> attachmentOptions,
    TimeProvider timeProvider)
{
    public async Task<NoteDto> AddNoteAsync(Guid customerId, AddNoteRequest request, CancellationToken ct)
    {
        _ = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);

        var note = new CustomerNote(customerId, request.Text.Trim(), request.AuthorName, timeProvider.GetUtcNow());
        if (request.IsPinned) note.SetPinned(true);

        await repository.AddNoteAsync(note, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(note);
    }

    public async Task SetNotePinnedAsync(Guid noteId, SetNotePinnedRequest request, CancellationToken ct)
    {
        var note = await repository.GetNoteByIdAsync(noteId, ct) ?? throw new KeyNotFoundException($"Note '{noteId}' was not found.");
        note.SetPinned(request.IsPinned);
        await repository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<NoteDto>> GetNotesAsync(Guid customerId, CancellationToken ct) =>
        (await repository.GetNotesAsync(customerId, ct))
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.CreatedAtUtc)
            .Select(ToDto)
            .ToList();

    public async Task<AttachmentDto> AddAttachmentAsync(Guid customerId, string fileName, string contentType, long sizeBytes, Stream content, string uploadedByName, CancellationToken ct)
    {
        _ = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);

        var maxSize = attachmentOptions.Value.MaxSizeBytes;
        if (sizeBytes > maxSize)
            throw new AttachmentTooLargeException(sizeBytes, maxSize);

        var attachmentId = Guid.NewGuid();
        var storageKey = await attachmentStorage.SaveAsync(customerId, attachmentId, fileName, content, ct);

        var attachment = new CustomerAttachment(customerId, fileName, contentType, sizeBytes, storageKey, uploadedByName, timeProvider.GetUtcNow());
        await repository.AddAttachmentAsync(attachment, ct);
        await repository.SaveChangesAsync(ct);
        return ToAttachmentDto(attachment);
    }

    public async Task<IReadOnlyList<AttachmentDto>> GetAttachmentsAsync(Guid customerId, CancellationToken ct) =>
        (await repository.GetAttachmentsAsync(customerId, ct)).Select(ToAttachmentDto).ToList();

    public async Task<(Stream Content, CustomerAttachment Attachment)> OpenAttachmentAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await repository.GetAttachmentByIdAsync(attachmentId, ct) ?? throw new KeyNotFoundException($"Attachment '{attachmentId}' was not found.");
        var stream = await attachmentStorage.OpenReadAsync(attachment.StorageKey, ct);
        return (stream, attachment);
    }

    private static NoteDto ToDto(CustomerNote n) => new(n.Id, n.Text, n.AuthorName, n.IsPinned, n.CreatedAtUtc);
    private static AttachmentDto ToAttachmentDto(CustomerAttachment a) => new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByName, a.UploadedAtUtc);
}
```

### 3 — Infrastructure: EF config, repository, local-disk storage

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet` properties and `OnModelCreating` blocks for `CustomerNote` and `CustomerAttachment`, following the same fluent-config style as `Customer`/`ContactDetail` (Story 02).

**Create file: `src/SupportCrm.Infrastructure/Persistence/NoteAndAttachmentRepository.cs`** — straightforward EF Core implementation of `INoteAndAttachmentRepository`, mirroring `CustomerRepository`'s structure (`FirstOrDefaultAsync` for single lookups, `Where(...).ToListAsync` for lists, `Add` + shared `SaveChangesAsync`).

**Create file: `src/SupportCrm.Infrastructure/Storage/LocalDiskAttachmentStorage.cs`**

```csharp
namespace SupportCrm.Infrastructure.Storage;

using Microsoft.Extensions.Options;
using SupportCrm.Application.Customers;

public class LocalDiskAttachmentStorageOptions
{
    public const string SectionName = "Attachments";
    public string RootPath { get; set; } = "App_Data/attachments";
}

public class LocalDiskAttachmentStorage(IOptions<LocalDiskAttachmentStorageOptions> options) : IAttachmentStorage
{
    public async Task<string> SaveAsync(Guid customerId, Guid attachmentId, string fileName, Stream content, CancellationToken ct)
    {
        var customerDir = Path.Combine(options.Value.RootPath, customerId.ToString());
        Directory.CreateDirectory(customerDir);

        var storageKey = Path.Combine(customerId.ToString(), $"{attachmentId}_{Path.GetFileName(fileName)}");
        var fullPath = Path.Combine(options.Value.RootPath, $"{attachmentId}_{Path.GetFileName(fileName)}");
        fullPath = Path.Combine(customerDir, $"{attachmentId}_{Path.GetFileName(fileName)}");

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

**Note for the executor:** the `SaveAsync` draft above computes `fullPath` twice (a leftover from drafting) — collapse to a single assignment before committing; keep `storageKey` as the customer-relative path (`"{customerId}/{attachmentId}_{fileName}"`) so it stays storage-backend-agnostic (a blob-storage implementation would use the same key as a blob name).

### 4 — Register the first real `ICustomerInteractionSource` (requires Story 03)

**Create file: `src/SupportCrm.Application/Customers/NotesInteractionSource.cs`** (only after Story 03's `ICustomerInteractionSource`/`CustomerInteractionDto` exist):

```csharp
namespace SupportCrm.Application.Customers;

public class NotesInteractionSource(INoteAndAttachmentRepository repository) : ICustomerInteractionSource
{
    public async Task<IReadOnlyList<CustomerInteractionDto>> GetInteractionsAsync(
        Guid customerId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, string? agentName, CancellationToken ct)
    {
        var notes = await repository.GetNotesAsync(customerId, ct);

        return notes
            .Where(n => fromUtc is null || n.CreatedAtUtc >= fromUtc)
            .Where(n => toUtc is null || n.CreatedAtUtc <= toUtc)
            .Where(n => agentName is null || string.Equals(n.AuthorName, agentName, StringComparison.OrdinalIgnoreCase))
            .Select(n => new CustomerInteractionDto(n.Id, "Note", n.CreatedAtUtc, n.Text, n.AuthorName, SourceUrl: null))
            .ToList();
    }
}
```

`SourceUrl: null` because there's no note-detail page/route in this codebase to link to — the note text itself is the summary.

### 5 — Api: config, controllers

**File: `src/SupportCrm.Api/appsettings.json`** — add, alongside `ConnectionStrings` (from Story 01):

```json
"Attachments": {
  "MaxSizeBytes": 10485760,
  "RootPath": "App_Data/attachments"
}
```

**File: `src/SupportCrm.Api/Program.cs`** — after `builder.Services.AddInfrastructure(builder.Configuration);` add:

```csharp
builder.Services.Configure<AttachmentOptions>(builder.Configuration.GetSection(AttachmentOptions.SectionName));
builder.Services.Configure<LocalDiskAttachmentStorageOptions>(builder.Configuration.GetSection(LocalDiskAttachmentStorageOptions.SectionName));
```

with `using SupportCrm.Application.Customers;` and `using SupportCrm.Infrastructure.Storage;` added at the top.

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — inside `AddInfrastructure`:

```csharp
        services.AddScoped<INoteAndAttachmentRepository, NoteAndAttachmentRepository>();
        services.AddScoped<IAttachmentStorage, LocalDiskAttachmentStorage>();
        services.AddScoped<NoteAndAttachmentService>();
        services.AddScoped<ICustomerInteractionSource, NotesInteractionSource>(); // requires Story 03's seam
```

**Create file: `src/SupportCrm.Api/Controllers/NotesController.cs`** — `[Route("api/customers/{customerId:guid}/notes")]`: `GET` (list, pinned-first), `POST` (add), `PUT("{noteId:guid}/pin")` (set pinned), following `CustomersController`'s try/catch-to-404 pattern for `CustomerNotFoundException`/`KeyNotFoundException`.

**Create file: `src/SupportCrm.Api/Controllers/AttachmentsController.cs`** — `[Route("api/customers/{customerId:guid}/attachments")]`:
- `GET` — list (`AttachmentDto[]`).
- `POST` — `[Consumes("multipart/form-data")]`, accepts `IFormFile file`, calls `NoteAndAttachmentService.AddAttachmentAsync(customerId, file.FileName, file.ContentType, file.Length, file.OpenReadStream(), authorName, ct)`; catch `AttachmentTooLargeException` → `413 Payload Too Large` (`StatusCode(StatusCodes.Status413PayloadTooLarge, ex.Message)`).
- `GET("{attachmentId:guid}/download")` — call `OpenAttachmentAsync`, return `File(stream, attachment.ContentType, attachment.FileName)`.

---

## Edge Cases & Failure Modes

- **Empty/whitespace note text** — `CustomerNote`'s constructor throws `ArgumentException`, surfaced as `400` by the controller.
- **Attachment exceeding the configured size limit** — `NoteAndAttachmentService.AddAttachmentAsync` checks `sizeBytes` against `AttachmentOptions.MaxSizeBytes` **before** calling storage, throwing `AttachmentTooLargeException` mapped to `413`; the file is never written to disk in this case.
- **Missing `AuthorName`/`UploadedByName`** — both entities coerce blank values to `"unknown"` rather than throwing, matching Story 02's `ChangedBy` handling (no auth yet).
- **Pinning a note that doesn't exist** — `KeyNotFoundException` → `404`.
- **Downloading an attachment whose on-disk file was deleted out-of-band** — `LocalDiskAttachmentStorage.OpenReadAsync` calls `File.OpenRead`, which throws `FileNotFoundException` if the file is gone; not caught explicitly in this story — surfaces as a `500`. Documented as a known gap since the intake doesn't call for reconciling storage drift.
- **Two attachments with the same original file name for the same customer** — `StorageKey` is prefixed with the attachment's own `Guid`, so no on-disk collision is possible even with identical file names.
- **Customer not found on any notes/attachments endpoint** — `CustomerNotFoundException` → `404`.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Customers/NoteAndAttachmentServiceTests.cs`**:
   - `AddNoteAsync_WithBlankText_ThrowsArgumentException`
   - `GetNotesAsync_OrdersPinnedFirstThenByCreatedAtDescending`
   - `AddAttachmentAsync_OverSizeLimit_ThrowsAttachmentTooLargeException`
   - `AddAttachmentAsync_WithinLimit_CallsStorageAndPersistsRecord` (fake `IAttachmentStorage`)
2. **Unit — `tests/SupportCrm.Application.Tests/Customers/NotesInteractionSourceTests.cs`**:
   - `GetInteractionsAsync_FiltersByDateRangeAndAgent`
   - `GetInteractionsAsync_MapsNoteToChannelNote`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/AttachmentsControllerTests.cs`**:
   - `Post_UploadOversizedFile_Returns413`
   - `Get_DownloadUnknownAttachment_Returns404`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Backend tests:** `dotnet test SupportCrm.slnx` (once test projects exist).
3. **Migration generation:** `dotnet ef migrations add AddNotesAndAttachments --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from `d:\Code\selfAssessment\backend`.
4. **Manual smoke:** upload a small file via `POST /api/customers/{id}/attachments` (multipart), confirm it appears under `App_Data/attachments/{customerId}/` and downloads correctly via `GET .../download`.

---

## Done Criteria

- [ ] An agent can add a free-text internal note (`POST .../notes`).
- [ ] Files up to the configured size limit can be attached and downloaded (`POST`/`GET .../attachments`); oversized files are rejected with `413`.
- [ ] Notes and attachments show author name and timestamp (`AuthorName`/`UploadedByName` + `CreatedAtUtc`/`UploadedAtUtc` on every DTO).
- [ ] Notes can be pinned and sort to the top (`PUT .../notes/{id}/pin`, `GetNotesAsync`'s ordering).
- [ ] If Story 03 exists, notes appear in the customer's interaction timeline via `NotesInteractionSource`.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
