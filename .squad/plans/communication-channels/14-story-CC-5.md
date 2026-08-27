# Story 14 — Web forms (Story: CC-5)

---

## Prerequisites

- Story 10 completed: [`10-story-CC-1.md`](10-story-CC-1.md) — `TicketIngestionService`, `TicketAttachment`.
- Ticket Management Story 06 completed: [`../ticket-management/06-story-TM-2.md`](../ticket-management/06-story-TM-2.md) — `TicketCategory`.

---

## Story Goal

Unlike CC-1..CC-4, **this story needs no external provider and no mock seam** — a web form submission is a direct HTTP request from this app's own frontend to this app's own backend. This is a fully real feature.

1. An administrator configures which fields appear on the web form **per category** (name, field type, required, display order).
2. A customer picks a category, the form renders the fields configured for it, submits, and gets back a confirmation with the ticket's reference number.
3. Submission is validated server-side against the category's field definitions (required fields present, file types/sizes acceptable) before a ticket is created — the server is authoritative even though the client mirrors the same rules for UX.
4. Submission goes through CC-1's shared ingestion path (`Channel: WebForm`), so it participates in the same dedup-to-open-ticket behavior as every other channel.

---

## Context — Read These Files First

1. [`10-story-CC-1.md`](10-story-CC-1.md), `## Backend Tasks` → `### 2` (`TicketIngestionService`, `TicketAttachmentService`) — this story's submission endpoint calls both: ingestion for the ticket, then attachment upload for any submitted files.
2. `../ticket-management/06-story-TM-2.md`, `## Backend Tasks` → `### 1`/`### 3` (`TicketCategory`, `ITicketCategoryRepository`) — this story's `WebFormFieldDefinition` has a required `CategoryId` foreign key into this existing table; do not duplicate category storage.

---

## Backend Tasks

### 1 — Domain: `WebFormFieldDefinition`

**Create file: `src/SupportCrm.Domain/Entities/WebFormFieldType.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public enum WebFormFieldType
{
    Text,
    TextArea,
    Email,
    Phone,
    File
}
```

**Create file: `src/SupportCrm.Domain/Entities/WebFormFieldDefinition.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class WebFormFieldDefinition
{
    public Guid Id { get; private set; }
    public Guid CategoryId { get; private set; }
    public string FieldName { get; private set; } = default!;
    public WebFormFieldType FieldType { get; private set; }
    public bool IsRequired { get; private set; }
    public int DisplayOrder { get; private set; }

    private WebFormFieldDefinition() { } // EF Core

    public WebFormFieldDefinition(Guid categoryId, string fieldName, WebFormFieldType fieldType, bool isRequired, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(fieldName))
            throw new ArgumentException("Field name is required.", nameof(fieldName));

        Id = Guid.NewGuid();
        CategoryId = categoryId;
        FieldName = fieldName;
        FieldType = fieldType;
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IWebFormFieldDefinitionRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IWebFormFieldDefinitionRepository
{
    Task<IReadOnlyList<WebFormFieldDefinition>> GetByCategoryAsync(Guid categoryId, CancellationToken ct);
    Task AddAsync(WebFormFieldDefinition definition, CancellationToken ct);
    Task<WebFormFieldDefinition?> GetByIdAsync(Guid id, CancellationToken ct);
    Task DeleteAsync(WebFormFieldDefinition definition, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, field-definition service, submission service

**Create file: `src/SupportCrm.Application/Tickets/WebFormDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record CreateWebFormFieldDefinitionRequest(Guid CategoryId, string FieldName, WebFormFieldType FieldType, bool IsRequired, int DisplayOrder);
public record WebFormFieldDefinitionDto(Guid Id, Guid CategoryId, string FieldName, WebFormFieldType FieldType, bool IsRequired, int DisplayOrder);

public record SubmitWebFormRequest(Guid CategoryId, string RequesterName, string RequesterContactValue, Dictionary<string, string> FieldValues);
public record WebFormSubmissionResultDto(string TicketReferenceNumber);

public class WebFormValidationException(IReadOnlyList<string> errors) : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}
```

**Create file: `src/SupportCrm.Application/Tickets/WebFormFieldDefinitionService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class WebFormFieldDefinitionService(IWebFormFieldDefinitionRepository repository)
{
    public async Task<WebFormFieldDefinitionDto> CreateAsync(CreateWebFormFieldDefinitionRequest request, CancellationToken ct)
    {
        var definition = new WebFormFieldDefinition(request.CategoryId, request.FieldName.Trim(), request.FieldType, request.IsRequired, request.DisplayOrder);
        await repository.AddAsync(definition, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(definition);
    }

    public async Task<IReadOnlyList<WebFormFieldDefinitionDto>> GetByCategoryAsync(Guid categoryId, CancellationToken ct) =>
        (await repository.GetByCategoryAsync(categoryId, ct)).OrderBy(d => d.DisplayOrder).Select(ToDto).ToList();

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var definition = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Field definition '{id}' was not found.");
        await repository.DeleteAsync(definition, ct);
        await repository.SaveChangesAsync(ct);
    }

    private static WebFormFieldDefinitionDto ToDto(WebFormFieldDefinition d) => new(d.Id, d.CategoryId, d.FieldName, d.FieldType, d.IsRequired, d.DisplayOrder);
}
```

**Create file: `src/SupportCrm.Application/Tickets/WebFormSubmissionService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class WebFormSubmissionService(
    IWebFormFieldDefinitionRepository fieldDefinitionRepository,
    TicketIngestionService ingestionService)
{
    public async Task<WebFormSubmissionResultDto> SubmitAsync(SubmitWebFormRequest request, CancellationToken ct)
    {
        var definitions = await fieldDefinitionRepository.GetByCategoryAsync(request.CategoryId, ct);
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.RequesterName))
            errors.Add("Name is required.");
        if (string.IsNullOrWhiteSpace(request.RequesterContactValue))
            errors.Add("Contact value is required.");

        foreach (var field in definitions.Where(d => d.IsRequired))
        {
            if (!request.FieldValues.TryGetValue(field.FieldName, out var value) || string.IsNullOrWhiteSpace(value))
                errors.Add($"'{field.FieldName}' is required.");
        }

        if (errors.Count > 0)
            throw new WebFormValidationException(errors);

        var description = string.Join("\n", request.FieldValues.Select(kv => $"{kv.Key}: {kv.Value}"));
        var ticket = await ingestionService.IngestInboundMessageAsync(
            new IngestInboundMessageRequest(TicketChannel.WebForm, request.RequesterName, request.RequesterContactValue, "Web form submission", description), ct);

        return new WebFormSubmissionResultDto(ticket.ReferenceNumber);
    }
}
```

**Note for the executor on file-field validation:** the AC requires validating "file types/size" too, but this DTO shape (`Dictionary<string,string> FieldValues`) has no room for actual file uploads — those must travel as a separate `multipart/form-data` part alongside the JSON-ish field values, similar to CC-1's inbound-webhook attachment handling. Design the controller action (below) to accept `[FromForm]` fields plus an `IFormFileCollection? files`, validate each file's content type/size against a configured allow-list (reuse `AttachmentOptions.MaxSizeBytes` from Customer Management CM-4 for the size half; add a small allow-listed content-type set for the type half — do not accept arbitrary file types unchecked), and upload accepted files via `TicketAttachmentService.AddAsync` against the resulting ticket, mirroring CC-1's inbound-webhook attachment flow exactly.

### 3 — Infrastructure: EF config, repository, DI

**File: `SupportCrmDbContext.cs`** — add `DbSet<WebFormFieldDefinition>` + `OnModelCreating` block (standard style, `FieldType` via `HasConversion<string>()`).

**Create file: `src/SupportCrm.Infrastructure/Persistence/WebFormFieldDefinitionRepository.cs`** — straightforward EF implementation.

**File: `DependencyInjection.cs`** — add `IWebFormFieldDefinitionRepository/WebFormFieldDefinitionRepository`, `WebFormFieldDefinitionService`, `WebFormSubmissionService`.

- After creating these files, run `dotnet ef migrations add AddWebForms --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: controllers

**Create file: `src/SupportCrm.Api/Controllers/WebFormFieldDefinitionsController.cs`** — `[Route("api/web-form-fields")]`: `GET ?categoryId=` (list), `POST` (create), `DELETE {id:guid}`.

**Create file: `src/SupportCrm.Api/Controllers/WebFormSubmissionsController.cs`** — `[Route("api/web-form-submissions")]`, `POST`, `[Consumes("multipart/form-data")]` per the executor note above; catches `WebFormValidationException` → `400` with `ex.Errors`.

---

## Edge Cases & Failure Modes

- **Submission for a category with zero field definitions** — every field is effectively optional (no definitions to check `IsRequired` against); only the base `RequesterName`/`RequesterContactValue` checks apply. Not an error — a category simply hasn't been configured yet.
- **Field value provided for a field name that isn't in the category's definitions** — silently ignored by `SubmitAsync` (it only iterates `definitions`, not `request.FieldValues`), rather than rejected — an acceptable simplification; extra/stale client-side fields don't break submission.
- **Multiple validation errors at once** — all required-field checks run before throwing, so `WebFormValidationException.Errors` can report every missing field in one response, not just the first.
- **Deleting a field definition that's referenced by a category still actively in use** — no historical link exists between a submission and the field definitions active at submission time (the description is a flattened string snapshot), so deleting a definition afterward cannot retroactively invalidate anything — flag as an accepted simplification, not a data-integrity bug.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/WebFormSubmissionServiceTests.cs`**:
   - `SubmitAsync_MissingRequiredField_ThrowsWithAllMissingFieldsListed`
   - `SubmitAsync_ValidSubmission_ReturnsTicketReferenceNumber`
   - `SubmitAsync_ExtraUnknownField_DoesNotFail`
2. **Integration — `tests/SupportCrm.Api.Tests/Controllers/WebFormSubmissionsControllerTests.cs`**:
   - `Post_SubmissionMissingRequiredFields_Returns400WithErrorList`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddWebForms --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] Field definitions can be created/listed/deleted per category (`/api/web-form-fields`).
- [ ] Submission creates a ticket and returns its reference number (`/api/web-form-submissions`).
- [ ] Server-side validation rejects missing required fields and disallowed file types/sizes.
- [ ] Submission goes through the shared ingestion path (dedups to an existing open ticket for the same customer).
- [ ] `dotnet build SupportCrm.slnx` succeeds.
