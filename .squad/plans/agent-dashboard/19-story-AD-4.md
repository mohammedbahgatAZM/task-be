# Story 19 — Quick replies (Story: AD-4)

---

## Prerequisites

- Communication Channels Story 15 completed (`../communication-channels/15-story-CC-6.md`) — the unified reply compose box this story's rendered output feeds into (frontend concern; backend just renders plain text).

---

## Story Goal

1. `QuickReplyTemplate` entity (category, name, body with `{{Placeholder}}` tokens, retired flag) — global, shared across the team, not per-agent.
2. CRUD + retire (soft, not delete).
3. A render endpoint: given a template + a ticket id, substitute `{{CustomerName}}`, `{{TicketReferenceNumber}}`, `{{TicketSubject}}` and return plain text — resolution is a server round-trip, never done client-side.

**Explicit, team-approved scope decision (see intake):** this story does **not** add a permission check on template CRUD, consistent with Communication Channels CC-5's ungated web-form field admin. "Authorized users" from the AC is intentionally left ungated here — flagged, not silently decided.

---

## Context — Read These Files First

1. `src/SupportCrm.Domain/Entities/Ticket.cs`, `src/SupportCrm.Domain/Entities/Customer.cs` — the fields rendering reads (`ReferenceNumber`, `Subject`, `RequesterName`; `Customer.Name`).
2. `src/SupportCrm.Application/Tickets/TicketDtos.cs`'s `TicketNotFoundException` — reused as the render endpoint's "ticket not found" case.

---

## Backend Tasks

### 1 — Domain: one new entity, one repository

**Create file: `src/SupportCrm.Domain/Entities/QuickReplyTemplate.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class QuickReplyTemplate
{
    public Guid Id { get; private set; }
    public string Category { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public bool IsRetired { get; private set; }
    public string CreatedBy { get; private set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private QuickReplyTemplate() { } // EF Core

    public QuickReplyTemplate(string category, string name, string body, string createdBy, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Template body is required.", nameof(body));

        Id = Guid.NewGuid();
        Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
        Name = name;
        Body = body;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
    }

    public void Update(string category, string name, string body)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Template name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Template body is required.", nameof(body));

        Category = string.IsNullOrWhiteSpace(category) ? "General" : category;
        Name = name;
        Body = body;
    }

    public void Retire() => IsRetired = true;
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IQuickReplyTemplateRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IQuickReplyTemplateRepository
{
    Task<QuickReplyTemplate?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<QuickReplyTemplate>> GetAllAsync(CancellationToken ct);
    Task AddAsync(QuickReplyTemplate template, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, service

**Create file: `src/SupportCrm.Application/Tickets/QuickReplyTemplateDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

public record CreateQuickReplyTemplateRequest(string Category, string Name, string Body, string CreatedBy);
public record UpdateQuickReplyTemplateRequest(string Category, string Name, string Body);
public record QuickReplyTemplateDto(Guid Id, string Category, string Name, string Body, bool IsRetired, DateTimeOffset CreatedAtUtc);
public record RenderQuickReplyTemplateRequest(Guid TicketId);
public record RenderedQuickReplyDto(string Body);
```

**Create file: `src/SupportCrm.Application/Tickets/QuickReplyTemplateService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class QuickReplyTemplateService(
    IQuickReplyTemplateRepository repository,
    ITicketRepository ticketRepository,
    ICustomerRepository customerRepository,
    TimeProvider timeProvider)
{
    public async Task<QuickReplyTemplateDto> CreateAsync(CreateQuickReplyTemplateRequest request, CancellationToken ct)
    {
        var template = new QuickReplyTemplate(request.Category, request.Name, request.Body, request.CreatedBy, timeProvider.GetUtcNow());
        await repository.AddAsync(template, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task<IReadOnlyList<QuickReplyTemplateDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).OrderBy(t => t.Category).ThenBy(t => t.Name).Select(ToDto).ToList();

    public async Task<QuickReplyTemplateDto> UpdateAsync(Guid id, UpdateQuickReplyTemplateRequest request, CancellationToken ct)
    {
        var template = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Template '{id}' was not found.");
        template.Update(request.Category, request.Name, request.Body);
        await repository.SaveChangesAsync(ct);
        return ToDto(template);
    }

    public async Task RetireAsync(Guid id, CancellationToken ct)
    {
        var template = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Template '{id}' was not found.");
        template.Retire();
        await repository.SaveChangesAsync(ct);
    }

    public async Task<RenderedQuickReplyDto> RenderAsync(Guid templateId, Guid ticketId, CancellationToken ct)
    {
        var template = await repository.GetByIdAsync(templateId, ct) ?? throw new KeyNotFoundException($"Template '{templateId}' was not found.");
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var customer = await customerRepository.GetByIdAsync(ticket.CustomerId, ct);

        var rendered = template.Body
            .Replace("{{CustomerName}}", customer?.Name ?? ticket.RequesterName)
            .Replace("{{TicketReferenceNumber}}", ticket.ReferenceNumber)
            .Replace("{{TicketSubject}}", ticket.Subject);

        return new RenderedQuickReplyDto(rendered);
    }

    private static QuickReplyTemplateDto ToDto(QuickReplyTemplate t) => new(t.Id, t.Category, t.Name, t.Body, t.IsRetired, t.CreatedAtUtc);
}
```

### 3 — Infrastructure: EF config, repository, DI

**File: `SupportCrmDbContext.cs`** — add `DbSet<QuickReplyTemplate> QuickReplyTemplates` and:

```csharp
        modelBuilder.Entity<QuickReplyTemplate>(entity =>
        {
            entity.ToTable("QuickReplyTemplates");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Category).IsRequired().HasMaxLength(128);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(256);
            entity.Property(t => t.Body).IsRequired();
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/QuickReplyTemplateRepository.cs`** — straightforward EF implementation mirroring `TicketCategoryRepository`'s shape.

**File: `DependencyInjection.cs`** — add:

```csharp
        services.AddScoped<IQuickReplyTemplateRepository, QuickReplyTemplateRepository>();
        services.AddScoped<QuickReplyTemplateService>();
```

### 4 — Api: controller

**Create file: `src/SupportCrm.Api/Controllers/QuickReplyTemplatesController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/quick-reply-templates")]
public class QuickReplyTemplatesController(QuickReplyTemplateService templateService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuickReplyTemplateDto>>> GetAll(CancellationToken ct) =>
        Ok(await templateService.GetAllAsync(ct));

    [HttpPost]
    public async Task<ActionResult<QuickReplyTemplateDto>> Create([FromBody] CreateQuickReplyTemplateRequest request, CancellationToken ct) =>
        await templateService.CreateAsync(request, ct);

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<QuickReplyTemplateDto>> Update(Guid id, [FromBody] UpdateQuickReplyTemplateRequest request, CancellationToken ct)
    {
        try { return await templateService.UpdateAsync(id, request, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/retire")]
    public async Task<IActionResult> Retire(Guid id, CancellationToken ct)
    {
        try { await templateService.RetireAsync(id, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPost("{id:guid}/render")]
    public async Task<ActionResult<RenderedQuickReplyDto>> Render(Guid id, [FromBody] RenderQuickReplyTemplateRequest request, CancellationToken ct)
    {
        try { return await templateService.RenderAsync(id, request.TicketId, ct); }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (TicketNotFoundException) { return NotFound(); }
    }
}
```

---

## Edge Cases & Failure Modes

- **A placeholder token with no matching data** (e.g. a customer with no name — shouldn't happen since `Customer.Name` is required, but the ticket's `CustomerId` could point at nothing if data is inconsistent) — falls back to `ticket.RequesterName`, never leaves a raw `{{CustomerName}}` token or throws.
- **Retiring a template already in use in past rendered replies** — no effect on history; a rendered reply is already plain text saved as a `TicketMessage`, retiring only removes the template from future "insert template" pickers.
- **Category left blank on create/update** — defaults to `"General"` rather than an empty/null category (keeps the admin list's grouping meaningful).

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/QuickReplyTemplateServiceTests.cs`**:
   - `RenderAsync_SubstitutesAllKnownPlaceholders`
   - `RenderAsync_MissingCustomer_FallsBackToRequesterName`
   - `RetireAsync_SetsIsRetiredWithoutDeleting`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Manual smoke:** create a template with `{{CustomerName}}`/`{{TicketReferenceNumber}}`, render it against a real ticket, confirm substitution.

---

## Done Criteria

- [ ] Templates can be created, updated, retired (not deleted), and listed by category.
- [ ] Rendering a template against a ticket substitutes all documented placeholders.
- [ ] No permission gate on CRUD (explicit scope decision, not an oversight).
- [ ] `dotnet build SupportCrm.slnx` succeeds. Migration needed: new `QuickReplyTemplates` table.
