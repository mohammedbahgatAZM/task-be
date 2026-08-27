# Story 06 — Categories and priorities (Story: TM-2)

---

## Prerequisites

- Story 05 completed: [`05-story-TM-1.md`](05-story-TM-1.md) — provides the `Ticket` aggregate and `TicketService` this story extends.

---

## Story Goal

1. Tickets can be assigned a category/sub-category drawn from a **configurable** list (a real table an admin can add rows to later, not a hardcoded enum) and a priority level (Low/Medium/High/Urgent).
2. Both can be changed after creation, with every change logged (who/what/when), reusing Customer Management's change-log pattern (CM-2's `ContactDetailChangeLogEntry`) rather than inventing a new shape.
3. A grouped-count endpoint supports filtering/grouping tickets by category and priority — a basic report, not a BI dashboard.

**Not in scope:** an admin UI for curating the category list (reading it is in scope; managing it is not, per the intake); a full reporting/analytics view.

---

## Context — Read These Files First

1. [`05-story-TM-1.md`](05-story-TM-1.md), `## Backend Tasks` → `### 1`/`### 2` — the `Ticket` entity and `TicketService` this story extends directly (not replaces).
2. `../customer-management/02-story-CM-2.md`, `## Backend Tasks` → `### 1` (the `ContactDetailChangeLogEntry` shape) — the audit-log precedent this story's `TicketFieldChangeEntry` follows: one generic entry type with a `FieldName` discriminator, rather than a separate table per changeable field.
3. `src/SupportCrm.Domain/Entities/Ticket.cs` (from TM-1) — private-setter entity pattern; add `CategoryId`/`Priority` properties and `SetCategory`/`SetPriority` methods in the same style as the existing `SetStatus`.
4. `src/SupportCrm.Application/Tickets/TicketService.cs` (from TM-1) — add new methods here; do not create a second ticket-mutation service (mirrors Customer Management's one-service-per-aggregate convention).
5. `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs` and `DependencyInjection.cs` (as extended by TM-1) — same fluent-config and registration style to follow for the new `TicketCategory`/`TicketFieldChangeEntry` types and `ITicketCategoryRepository`.

---

## Backend Tasks

### 1 — Domain: `TicketPriority`, `TicketCategory`, generic field-change log, `Ticket` extensions

**Create file: `src/SupportCrm.Domain/Entities/TicketPriority.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public enum TicketPriority
{
    Low,
    Medium,
    High,
    Urgent
}
```

**Create file: `src/SupportCrm.Domain/Entities/TicketCategory.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class TicketCategory
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public Guid? ParentCategoryId { get; private set; }
    public bool IsActive { get; private set; } = true;

    private TicketCategory() { } // EF Core

    public TicketCategory(string name, Guid? parentCategoryId)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Category name is required.", nameof(name));

        Id = Guid.NewGuid();
        Name = name;
        ParentCategoryId = parentCategoryId;
    }

    public void Deactivate() => IsActive = false;
}
```

**Create file: `src/SupportCrm.Domain/Entities/TicketFieldChangeEntry.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class TicketFieldChangeEntry
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string FieldName { get; private set; } = default!; // "Category" | "Priority"
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public string ChangedBy { get; private set; } = default!;
    public DateTimeOffset ChangedAtUtc { get; private set; }

    private TicketFieldChangeEntry() { } // EF Core

    public TicketFieldChangeEntry(Guid ticketId, string fieldName, string? oldValue, string? newValue, string changedBy, DateTimeOffset changedAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        FieldName = fieldName;
        OldValue = oldValue;
        NewValue = newValue;
        ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        ChangedAtUtc = changedAtUtc;
    }
}
```

**File: `src/SupportCrm.Domain/Entities/Ticket.cs`** — add properties alongside the existing ones:

```csharp
    public Guid? CategoryId { get; private set; }
    public TicketPriority Priority { get; private set; } = TicketPriority.Medium;
```

and methods alongside `SetStatus`:

```csharp
    public void SetCategory(Guid? categoryId) => CategoryId = categoryId;

    public void SetPriority(TicketPriority priority) => Priority = priority;
```

**Create file: `src/SupportCrm.Domain/Repositories/ITicketCategoryRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketCategoryRepository
{
    Task<TicketCategory?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TicketCategory>> GetActiveAsync(CancellationToken ct);
    Task AddAsync(TicketCategory category, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Extend file: `src/SupportCrm.Domain/Repositories/ITicketRepository.cs`** — add:

```csharp
    Task<IReadOnlyList<TicketFieldChangeEntry>> GetFieldChangeLogAsync(Guid ticketId, CancellationToken ct);
    Task AddFieldChangeAsync(TicketFieldChangeEntry entry, CancellationToken ct);
    Task<IReadOnlyDictionary<Guid?, int>> CountGroupedByCategoryAsync(CancellationToken ct);
    Task<IReadOnlyDictionary<TicketPriority, int>> CountGroupedByPriorityAsync(CancellationToken ct);
```

### 2 — Application: DTOs, `TicketCategoryService`, `TicketService` extensions

**Create file: `src/SupportCrm.Application/Tickets/TicketCategoryDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

public record CreateTicketCategoryRequest(string Name, Guid? ParentCategoryId);
public record TicketCategoryDto(Guid Id, string Name, Guid? ParentCategoryId);
```

**Create file: `src/SupportCrm.Application/Tickets/TicketCategoryService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketCategoryService(ITicketCategoryRepository repository)
{
    public async Task<TicketCategoryDto> CreateAsync(CreateTicketCategoryRequest request, CancellationToken ct)
    {
        var category = new TicketCategory(request.Name.Trim(), request.ParentCategoryId);
        await repository.AddAsync(category, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<IReadOnlyList<TicketCategoryDto>> GetActiveAsync(CancellationToken ct) =>
        (await repository.GetActiveAsync(ct)).Select(ToDto).ToList();

    private static TicketCategoryDto ToDto(TicketCategory c) => new(c.Id, c.Name, c.ParentCategoryId);
}
```

**File: `src/SupportCrm.Application/Tickets/TicketDtos.cs`** — add:

```csharp
public record SetCategoryRequest(Guid? CategoryId, string ChangedBy);
public record SetPriorityRequest(TicketPriority Priority, string ChangedBy);
public record TicketFieldChangeDto(Guid Id, string FieldName, string? OldValue, string? NewValue, string ChangedBy, DateTimeOffset ChangedAtUtc);
public record TicketGroupedCountsDto(IReadOnlyDictionary<string, int> ByCategory, IReadOnlyDictionary<string, int> ByPriority);
```

(Add `using SupportCrm.Domain.Entities;` to this file if not already present, for `TicketPriority`.)

**File: `src/SupportCrm.Application/Tickets/TicketService.cs`** — add methods alongside `RecordStatusChangeAsync`:

```csharp
    public async Task SetCategoryAsync(Guid ticketId, SetCategoryRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var oldCategoryId = ticket.CategoryId;
        ticket.SetCategory(request.CategoryId);
        await ticketRepository.AddFieldChangeAsync(
            new TicketFieldChangeEntry(ticketId, "Category", oldCategoryId?.ToString(), request.CategoryId?.ToString(), request.ChangedBy, timeProvider.GetUtcNow()), ct);
        await ticketRepository.SaveChangesAsync(ct);
    }

    public async Task SetPriorityAsync(Guid ticketId, SetPriorityRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var oldPriority = ticket.Priority;
        ticket.SetPriority(request.Priority);
        await ticketRepository.AddFieldChangeAsync(
            new TicketFieldChangeEntry(ticketId, "Priority", oldPriority.ToString(), request.Priority.ToString(), request.ChangedBy, timeProvider.GetUtcNow()), ct);
        await ticketRepository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TicketFieldChangeDto>> GetFieldChangeLogAsync(Guid ticketId, CancellationToken ct) =>
        (await ticketRepository.GetFieldChangeLogAsync(ticketId, ct))
            .OrderByDescending(e => e.ChangedAtUtc)
            .Select(e => new TicketFieldChangeDto(e.Id, e.FieldName, e.OldValue, e.NewValue, e.ChangedBy, e.ChangedAtUtc))
            .ToList();

    public async Task<TicketGroupedCountsDto> GetGroupedCountsAsync(CancellationToken ct)
    {
        var byCategory = await ticketRepository.CountGroupedByCategoryAsync(ct);
        var byPriority = await ticketRepository.CountGroupedByPriorityAsync(ct);
        return new TicketGroupedCountsDto(
            byCategory.ToDictionary(kv => kv.Key?.ToString() ?? "Uncategorized", kv => kv.Value),
            byPriority.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value));
    }
```

### 3 — Infrastructure: EF config, repositories, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet<TicketCategory>` and `DbSet<TicketFieldChangeEntry>`, plus `OnModelCreating` blocks matching the existing style; extend the `Ticket` block with:

```csharp
    entity.Property(t => t.Priority).HasConversion<string>().HasMaxLength(16).IsRequired();
    entity.HasIndex(t => t.CategoryId);
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/TicketCategoryRepository.cs`** — straightforward EF implementation of `ITicketCategoryRepository`, mirroring `CustomerRepository`'s structure.

**File: `src/SupportCrm.Infrastructure/Persistence/TicketRepository.cs`** — implement the 3 new interface members:

```csharp
    public async Task<IReadOnlyList<TicketFieldChangeEntry>> GetFieldChangeLogAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketFieldChangeEntries.Where(e => e.TicketId == ticketId).ToListAsync(ct);

    public Task AddFieldChangeAsync(TicketFieldChangeEntry entry, CancellationToken ct)
    {
        dbContext.TicketFieldChangeEntries.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyDictionary<Guid?, int>> CountGroupedByCategoryAsync(CancellationToken ct) =>
        await dbContext.Tickets.GroupBy(t => t.CategoryId).ToDictionaryAsync(g => g.Key, g => g.Count(), ct);

    public async Task<IReadOnlyDictionary<TicketPriority, int>> CountGroupedByPriorityAsync(CancellationToken ct) =>
        await dbContext.Tickets.GroupBy(t => t.Priority).ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add:

```csharp
        services.AddScoped<Domain.Repositories.ITicketCategoryRepository, Persistence.TicketCategoryRepository>();
        services.AddScoped<Application.Tickets.TicketCategoryService>();
```

**Seed data:** add a small `TicketCategory` seed set (e.g., "Billing", "Technical Issue", "General Inquiry", "Account") via `modelBuilder.Entity<TicketCategory>().HasData(...)` in `OnModelCreating`, using fixed `Guid`s so the seed is migration-stable.

### 4 — Api: controller additions

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add:

```csharp
    [HttpPut("{id:guid}/category")]
    public async Task<IActionResult> SetCategory(Guid id, [FromBody] SetCategoryRequest request, CancellationToken ct)
    {
        try { await ticketService.SetCategoryAsync(id, request, ct); return NoContent(); }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/priority")]
    public async Task<IActionResult> SetPriority(Guid id, [FromBody] SetPriorityRequest request, CancellationToken ct)
    {
        try { await ticketService.SetPriorityAsync(id, request, ct); return NoContent(); }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/field-history")]
    public async Task<ActionResult<IReadOnlyList<TicketFieldChangeDto>>> GetFieldHistory(Guid id, CancellationToken ct) =>
        Ok(await ticketService.GetFieldChangeLogAsync(id, ct));

    [HttpGet("grouped-counts")]
    public async Task<ActionResult<TicketGroupedCountsDto>> GetGroupedCounts(CancellationToken ct) =>
        Ok(await ticketService.GetGroupedCountsAsync(ct));
```

**Create file: `src/SupportCrm.Api/Controllers/TicketCategoriesController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/ticket-categories")]
public class TicketCategoriesController(TicketCategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketCategoryDto>>> GetAll(CancellationToken ct) =>
        Ok(await categoryService.GetActiveAsync(ct));

    [HttpPost]
    public async Task<ActionResult<TicketCategoryDto>> Create([FromBody] CreateTicketCategoryRequest request, CancellationToken ct) =>
        await categoryService.CreateAsync(request, ct);
}
```

- After creating these files, run `dotnet ef migrations add AddTicketCategoriesAndPriority --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

---

## Edge Cases & Failure Modes

- **`CategoryId` referencing a non-existent or deactivated category** — `SetCategoryAsync` does not validate the id resolves to an active `TicketCategory` before storing it (no FK constraint modeled either). Documented gap: acceptable for this story since the intake only asks for changes to be *logged*, not strictly validated — flag for a future story if data integrity here becomes a problem.
- **Grouped-counts endpoint with zero tickets** — both dictionaries are empty, not an error; the DTO's `ToDictionary` calls handle empty source collections correctly.
- **Setting priority/category to its current value** — allowed and still logs a change entry (mirrors CM-2's `SetPrimaryAsync` idempotent-log behavior) — an accepted simplification, not a bug.
- **Unknown ticket id on category/priority endpoints** — `TicketNotFoundException` → `404`.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketServiceCategoryPriorityTests.cs`**:
   - `SetCategoryAsync_WritesFieldChangeEntry`
   - `SetPriorityAsync_WritesFieldChangeEntry`
   - `GetGroupedCountsAsync_CountsMatchTicketsByField`
2. **Integration — `tests/SupportCrm.Api.Tests/Controllers/TicketCategoriesControllerTests.cs`**:
   - `Get_ActiveCategories_ReturnsSeededList`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddTicketCategoriesAndPriority --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] Tickets can be assigned a category/sub-category from a configurable, seeded list (`PUT /api/tickets/{id}/category`, `GET /api/ticket-categories`).
- [ ] Tickets can be assigned a priority (`PUT /api/tickets/{id}/priority`).
- [ ] Category and priority changes are logged (`GET /api/tickets/{id}/field-history`).
- [ ] Grouped counts by category and priority are available (`GET /api/tickets/grouped-counts`).
- [ ] `dotnet build SupportCrm.slnx` succeeds.
