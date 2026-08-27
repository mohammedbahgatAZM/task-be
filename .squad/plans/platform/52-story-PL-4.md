# Story 52 — Multi-branch (Story: PL-4)

---

## Prerequisites

None (independent of Story 51, though they land in the same consolidated migration).

---

## Story Goal

1. `Branch` entity — `Name`, `Code`, `DefaultLanguage`, `ContactNumber`, `IsActive`.
2. `Agent.BranchId`, `Customer.BranchId` — additive, parallel to the existing `Customer.Branch` string (kept, unmodified).
3. Business hours stay global — explicitly not retrofitted (see intake).

---

## Context — Read These Files First

1. `src/SupportCrm.Domain/Entities/Customer.cs`, `Branch` property — confirm it's untouched by this story.

---

## Backend Tasks

### 1 — Domain

**Create file: `src/SupportCrm.Domain/Entities/Branch.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class Branch
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string Code { get; private set; } = default!;
    public string? DefaultLanguage { get; private set; } // "en" | "ar"
    public string? ContactNumber { get; private set; }
    public bool IsActive { get; private set; } = true;

    private Branch() { }

    public Branch(string name, string code)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Branch name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Branch code is required.", nameof(code));
        Id = Guid.NewGuid();
        Name = name.Trim();
        Code = code.Trim().ToUpperInvariant();
    }

    public void UpdateSettings(string? defaultLanguage, string? contactNumber)
    {
        DefaultLanguage = defaultLanguage is "en" or "ar" ? defaultLanguage : null;
        ContactNumber = contactNumber?.Trim();
    }

    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IBranchRepository.cs`** — `GetByIdAsync`, `GetAllAsync`, `AddAsync`, `SaveChangesAsync` (same shape as `IDepartmentRepository`).

**Entity additions:**

- `src/SupportCrm.Domain/Entities/Agent.cs`: `public Guid? BranchId { get; private set; }` + `public void SetBranch(Guid? branchId) => BranchId = branchId;`
- `src/SupportCrm.Domain/Entities/Customer.cs`: same, alongside the existing (unmodified) `Branch` string property.

### 2 — Application

**File: `src/SupportCrm.Application/Platform/PlatformDtos.cs`** — append:

```csharp
public record CreateBranchRequest(string Name, string Code);
public record BranchDto(Guid Id, string Name, string Code, string? DefaultLanguage, string? ContactNumber, bool IsActive);
public record UpdateBranchSettingsRequest(string? DefaultLanguage, string? ContactNumber);
```

**Create file: `src/SupportCrm.Application/Platform/BranchService.cs`**

```csharp
namespace SupportCrm.Application.Platform;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class BranchService(IBranchRepository repository)
{
    public async Task<BranchDto> CreateAsync(CreateBranchRequest request, CancellationToken ct)
    {
        var branch = new Branch(request.Name, request.Code);
        await repository.AddAsync(branch, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(branch);
    }

    public async Task<IReadOnlyList<BranchDto>> GetAllAsync(CancellationToken ct) =>
        (await repository.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task UpdateSettingsAsync(Guid id, UpdateBranchSettingsRequest request, CancellationToken ct)
    {
        var branch = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Branch '{id}' was not found.");
        branch.UpdateSettings(request.DefaultLanguage, request.ContactNumber);
        await repository.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken ct)
    {
        var branch = await repository.GetByIdAsync(id, ct) ?? throw new KeyNotFoundException($"Branch '{id}' was not found.");
        if (isActive) branch.Activate(); else branch.Deactivate();
        await repository.SaveChangesAsync(ct);
    }

    private static BranchDto ToDto(Branch b) => new(b.Id, b.Name, b.Code, b.DefaultLanguage, b.ContactNumber, b.IsActive);
}
```

**File: `src/SupportCrm.Application/Tickets/AgentService.cs`** — add `SetBranchAsync(Guid agentId, Guid? branchId, CancellationToken ct)`, same shape as Story 51's `SetDepartmentAsync`.

**File: `src/SupportCrm.Application/Customers/CustomerService.cs`** — add `SetBranchAsync(Guid customerId, Guid? branchId, CancellationToken ct)`.

### 3 — Infrastructure

**Create file: `src/SupportCrm.Infrastructure/Persistence/BranchRepository.cs`** — standard EF repo.

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet<Branch>`, an `OnModelCreating` block (`ToTable("Branches")`, `Property(b => b.Name)`/`Code` required+max-length, `HasIndex(b => b.Code).IsUnique()`), and `BranchId` FK columns + indexes on the `Agent`/`Customer` blocks.

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — `services.AddScoped<IBranchRepository, BranchRepository>();`, `services.AddScoped<BranchService>();`.

### 4 — Api

**Create file: `src/SupportCrm.Api/Controllers/BranchesController.cs`** (`api/branches`) — `GET`, `POST`, `PUT {id}/settings`, `PUT {id}/activate`, `PUT {id}/deactivate`.

**File: `src/SupportCrm.Api/Controllers/AgentsController.cs`** — add `PUT {id:guid}/branch`.

**File: `src/SupportCrm.Api/Controllers/CustomersController.cs`** — add `PUT {id:guid}/branch`.

---

## Edge Cases & Failure Modes

- **Duplicate branch code** — rejected at the database level (`HasIndex(b => b.Code).IsUnique()`); the service layer doesn't pre-check, matching this codebase's existing precedent of letting a handful of genuinely rare uniqueness violations surface as a `500` rather than adding a pre-check to every creation path (e.g. `TicketCategory` has no duplicate-name guard either).
- **A `Customer` with a `Branch` string but no `BranchId`** (every customer created before this story, or via a flow that still only sets the string) — fully valid, unremarkable state; Reports & Management's existing branch filter keeps working off the string exactly as before.

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx`.
2. **Migration:** `dotnet ef migrations add AddPlatform --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`, run once after this story's and Stories 51/53's model changes are all in place.

---

## Done Criteria

- [ ] Branches CRUD-able with their own language default + contact number.
- [ ] Agents/customers assignable to a branch via `BranchId`, alongside the existing `Customer.Branch` string (untouched).
- [ ] `dotnet build SupportCrm.slnx` succeeds; `AddPlatform` migration generated cleanly.
