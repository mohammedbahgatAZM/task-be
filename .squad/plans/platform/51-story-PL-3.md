# Story 51 — Multi-department (Story: PL-3)

---

## Prerequisites

- Reports & Management Story 40 — `TicketReportQuery`/`TicketReportService`, extended here with a `DepartmentId` filter.

---

## Story Goal

1. `Department` entity — `Name`, `IsActive`, `DefaultForChannel`.
2. `Agent.DepartmentId`, `TicketCategory.DepartmentId`, `Team.DepartmentId` — "its own agents, categories, and queues."
3. Automatic routing (category wins, channel-default fallback) wired into `TicketService.CreateAsync`.
4. `DepartmentId` filter + `ByDepartment` breakdown on the ticket volume report.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Tickets/TicketService.cs`, `CreateAsync` — the exact insertion point for the new routing call.
2. `src/SupportCrm.Application/Reports/TicketReportService.cs` — `ByBranch`, the exact shape `ByDepartment` mirrors.
3. `src/SupportCrm.Application/Sla/SlaTargetService.cs`, `ResolveAsync` — precedent for "no match returns null, not an error."

---

## Backend Tasks

### 1 — Domain

**Create file: `src/SupportCrm.Domain/Entities/Department.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class Department
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public bool IsActive { get; private set; } = true;
    public TicketChannel? DefaultForChannel { get; private set; }

    private Department() { }

    public Department(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Department name is required.", nameof(name));
        Id = Guid.NewGuid();
        Name = name.Trim();
    }

    public void SetDefaultChannel(TicketChannel? channel) => DefaultForChannel = channel;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IDepartmentRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IDepartmentRepository
{
    Task<Department?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Department>> GetAllAsync(CancellationToken ct);
    Task AddAsync(Department department, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Entity additions** (each a nullable `DepartmentId` + `SetDepartment`, same shape three times):

- `src/SupportCrm.Domain/Entities/Agent.cs`: `public Guid? DepartmentId { get; private set; }` + `public void SetDepartment(Guid? departmentId) => DepartmentId = departmentId;`
- `src/SupportCrm.Domain/Entities/TicketCategory.cs`: same.
- `src/SupportCrm.Domain/Entities/Team.cs`: same.
- `src/SupportCrm.Domain/Entities/Ticket.cs`: same, named `SetDepartment`.

### 2 — Application

**Create file: `src/SupportCrm.Application/Platform/PlatformDtos.cs`** (this story's subset — Stories 52/53 append theirs):

```csharp
namespace SupportCrm.Application.Platform;

using SupportCrm.Domain.Entities;

public record CreateDepartmentRequest(string Name);
public record DepartmentDto(Guid Id, string Name, bool IsActive, TicketChannel? DefaultForChannel);
public record SetDepartmentChannelRequest(TicketChannel? Channel);
public record SetDepartmentIdRequest(Guid? DepartmentId);
```

**Create file: `src/SupportCrm.Application/Platform/DepartmentService.cs`**

```csharp
namespace SupportCrm.Application.Platform;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class DepartmentService(IDepartmentRepository repository)
{
    public async Task<DepartmentDto> CreateAsync(CreateDepartmentRequest request, CancellationToken ct)
    {
        var department = new Department(request.Name);
        await repository.AddAsync(department, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(department);
    }

    public async Task<IReadOnlyList<DepartmentDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var department = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Department '{id}' was not found.");
        if (isActive) department.Activate(); else department.Deactivate();
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetDefaultChannelAsync(Guid id, SetDepartmentChannelRequest request, CancellationToken ct)
    {
        var department = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Department '{id}' was not found.");
        department.SetDefaultChannel(request.Channel);
        await repository.SaveChangesAsync(ct);
    }

    private static DepartmentDto ToDto(Department d) => new(d.Id, d.Name, d.IsActive, d.DefaultForChannel);
}
```

**Create file: `src/SupportCrm.Application/Platform/TicketDepartmentRoutingService.cs`**

```csharp
namespace SupportCrm.Application.Platform;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

// Two-step resolution, category first: an explicit category-to-department assignment always
// beats a channel default. Neither matching leaves the ticket unrouted (null), same "no match
// is a valid outcome" convention SlaTargetService.ResolveAsync already established.
public class TicketDepartmentRoutingService(ITicketCategoryRepository categoryRepository, IDepartmentRepository departmentRepository)
{
    public async Task<Guid?> ResolveDepartmentAsync(Guid? categoryId, TicketChannel channel, CancellationToken ct)
    {
        if (categoryId is Guid catId)
        {
            var category = await categoryRepository.GetByIdAsync(catId, ct);
            if (category?.DepartmentId is Guid deptId) return deptId;
        }

        var departments = await departmentRepository.GetAllAsync(ct);
        return departments.FirstOrDefault(d => d.IsActive && d.DefaultForChannel == channel)?.Id;
    }
}
```

**File: `src/SupportCrm.Application/Tickets/TicketService.cs`** — add `SupportCrm.Application.Platform.TicketDepartmentRoutingService departmentRoutingService` to the constructor, and in `CreateAsync`, right after the category-resolution block (post Customer Portal CP-1's edit) and before `await ticketRepository.AddAsync(ticket, ct);`:

```csharp
        ticket.SetDepartment(await departmentRoutingService.ResolveDepartmentAsync(ticket.CategoryId, request.Channel, ct));
```

**File: `src/SupportCrm.Application/Tickets/TicketDtos.cs`** — add `Guid? DepartmentId` to `TicketDto`, and update `TicketService.ToDto` to pass `t.DepartmentId`.

**File: `src/SupportCrm.Application/Tickets/TicketCategoryService.cs`** — add:

```csharp
    public async Task SetDepartmentAsync(Guid categoryId, Guid? departmentId, CancellationToken ct)
    {
        var category = await repository.GetByIdAsync(categoryId, ct) ?? throw new KeyNotFoundException($"Category '{categoryId}' was not found.");
        category.SetDepartment(departmentId);
        await repository.SaveChangesAsync(ct);
    }
```

(Needs `ITicketCategoryRepository.GetByIdAsync` — already exists.)

**File: `src/SupportCrm.Application/Tickets/TeamService.cs`** — add the equivalent `SetDepartmentAsync`, needs `ITeamRepository.GetByIdAsync` (already exists).

**File: `src/SupportCrm.Application/Tickets/AgentService.cs`** — add the equivalent `SetDepartmentAsync`.

**File: `src/SupportCrm.Application/Reports/ReportDtos.cs`** — extend `TicketReportQuery` with `Guid? DepartmentId` and `TicketVolumeReportDto` with `IReadOnlyDictionary<string, int> ByDepartment`.

**File: `src/SupportCrm.Application/Reports/TicketReportService.cs`** — in `Filter`, add a `DepartmentId` predicate; in `GetVolumeReportAsync`/`ExportVolumeReportAsync`, compute `byDepartment` the same way `byBranch` is computed (grouping on `t.DepartmentId`, looking up department names via a new `departmentRepository.GetAllAsync` call, `"Unassigned"` for `null`), and thread it through both the JSON DTO and the export columns.

### 3 — Infrastructure

**Create file: `src/SupportCrm.Infrastructure/Persistence/DepartmentRepository.cs`** — standard EF repo mirroring `RoleRepository.cs`'s shape.

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet<Department>`, an `OnModelCreating` block (`ToTable("Departments")`, unique-ish nothing required, `Property(d => d.Name).IsRequired().HasMaxLength(256)`, `Property(d => d.DefaultForChannel).HasConversion<string?>().HasMaxLength(16)`), and add `entity.Property(a => a.DepartmentId)`-style FK columns (no `HasOne`/navigation needed — every consumer already works of off the raw Guid, same as `Ticket.CategoryId` today) to the existing `Agent`/`TicketCategory`/`Team`/`Ticket` blocks, each with a `HasIndex`.

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add `using SupportCrm.Application.Platform;`, `services.AddScoped<IDepartmentRepository, DepartmentRepository>();`, `services.AddScoped<DepartmentService>();`, `services.AddScoped<TicketDepartmentRoutingService>();`.

### 4 — Api

**Create file: `src/SupportCrm.Api/Controllers/DepartmentsController.cs`** (`api/departments`) — `GET` (all), `POST` (create), `PUT {id}/activate`, `PUT {id}/deactivate`, `PUT {id}/default-channel`.

**File: `src/SupportCrm.Api/Controllers/AgentsController.cs`** — add `PUT {id:guid}/department` calling `agentService.SetDepartmentAsync`.

**File: `src/SupportCrm.Api/Controllers/TicketCategoriesController.cs`** — add `PUT {id:guid}/department`.

**File: `src/SupportCrm.Api/Controllers/TeamsController.cs`** — add `PUT {id:guid}/department`.

**File: `src/SupportCrm.Api/Controllers/ReportsController.cs`** — add a `departmentId` query parameter to `GetTicketReport`/`ExportTicketReport`, threaded into `TicketReportQuery`.

---

## Edge Cases & Failure Modes

- **A ticket whose category has no department AND whose channel matches no department's `DefaultForChannel`** — `DepartmentId` stays `null`; nothing in this codebase treats that as an error (matches `CategoryId`'s own long-established nullability).
- **Two active departments both claim the same `DefaultForChannel`** — `FirstOrDefault` picks whichever the repository returns first (undefined tie-break order); flagged, not defended against — the UI should prevent this by disallowing a second department from claiming an already-claimed channel, a validation left to the frontend rather than a database constraint, matching this codebase's general preference for simple server-side rules over exhaustive constraint modeling.
- **Deactivating a department that's still someone's `DefaultForChannel`** — allowed; the routing resolver already filters on `d.IsActive`, so a deactivated department simply stops being a routing target, no cascading cleanup needed.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Platform/TicketDepartmentRoutingServiceTests.cs`**: `ResolveDepartmentAsync_CategoryDepartmentWinsOverChannelDefault`; `ResolveDepartmentAsync_NoMatch_ReturnsNull`.

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx`.
2. **Migration:** generated together with Stories 52/53's model changes as one `AddPlatform` migration (see Story 52).

---

## Done Criteria

- [ ] Departments CRUD-able; agents/categories/teams assignable to one.
- [ ] Ticket creation auto-routes to a department (category first, channel-default fallback).
- [ ] Ticket volume report filterable/breakdown-able by department.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
