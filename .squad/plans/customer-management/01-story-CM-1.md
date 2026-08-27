# Story 01 — Customer profiles (Story: CM-1)

---

## Prerequisites

- None. This is the first feature implemented in this solution — `SupportCrm.Domain`, `SupportCrm.Application`, and `SupportCrm.Infrastructure` currently contain no types, and `SupportCrm.Api` has no controllers.

---

## Story Goal

Support agents can:

1. Create a customer profile with a name, company/branch, and a system-generated **unique customer number**.
2. Look up an existing customer by name/company (used both for manual search and for duplicate detection before creating a new profile).
3. View a customer's profile summary: core fields, contact info placeholder, open-tickets count, and last-interaction date.
4. Detect duplicate profiles and merge one into another (authorized users only, enforced later — for now expose the endpoint unauthenticated per the intake's technical hints, but structure the code so an `[Authorize]` attribute can be added without refactoring).

**Not in scope** (per intake): building the Ticketing module, sending notifications, any Angular/UI work (tracked separately in the frontend repo's `customer-management/CM-1` story). The "open tickets" count and "last interaction date" in the summary are **stubs** — no Ticketing/interaction module exists yet in this codebase, so these fields must be modeled as an injectable seam (an interface with a stub implementation returning `0` / `null`) rather than hard-coded literals, so a later story can supply a real implementation via DI without touching the summary-building code.

---

## Context — Read These Files First

1. `src/SupportCrm.Api/Program.cs` — the entire file (23 lines). Minimal `WebApplication` builder: `AddControllers()`, `AddOpenApi()`, `MapOpenApi()` in Development, `UseHttpsRedirection()`, `UseAuthorization()`, `MapControllers()`. Add `AddDbContext`, `AddScoped` service registrations, and `AddEndpointsApiExplorer`/Swagger UI wiring here — do not restructure the existing pipeline order.
2. `src/SupportCrm.Api/SupportCrm.Api.csproj` — confirms package references: `Microsoft.AspNetCore.Authentication.JwtBearer` 10.0.11, `Microsoft.AspNetCore.OpenApi` 10.0.9, `Microsoft.EntityFrameworkCore.Design` 10.0.11, `Swashbuckle.AspNetCore` 10.2.3. References `SupportCrm.Application` and `SupportCrm.Infrastructure` by `ProjectReference`. No new package reference is needed for this story.
3. `src/SupportCrm.Application/SupportCrm.Application.csproj` — references `SupportCrm.Domain` only. Target `net10.0`, `Nullable` enable, `ImplicitUsings` enable.
4. `src/SupportCrm.Domain/SupportCrm.Domain.csproj` — no project references (correct — Domain must stay dependency-free).
5. `src/SupportCrm.Infrastructure/SupportCrm.Infrastructure.csproj` — references `SupportCrm.Domain` and `SupportCrm.Application`. Already has `Microsoft.AspNetCore.Identity.EntityFrameworkCore` 10.0.11, `Microsoft.EntityFrameworkCore.Design` 10.0.11, `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3. No new package reference needed; add `Microsoft.EntityFrameworkCore.Relational` transitively via Npgsql (already present).
6. `src/SupportCrm.Api/appsettings.json` — only has `Logging` and `AllowedHosts` (8 lines total). Add a `ConnectionStrings:Default` key here (and to `appsettings.Development.json`) rather than hard-coding the connection string in `Program.cs`.

No sibling plan exists yet in `.squad/plans/` to follow as precedent — this story establishes the pattern for the feature. Match ASP.NET Core / EF Core conventional layering: **Domain** = entities + value objects + repository interfaces; **Application** = DTOs + service interfaces/implementations + validation; **Infrastructure** = `DbContext` + EF repository implementations + migrations; **Api** = controllers + DI wiring only.

---

## Backend Tasks

### 1 — Domain: `Customer` aggregate

**Create file: `src/SupportCrm.Domain/Entities/Customer.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class Customer
{
    public Guid Id { get; private set; }
    public string CustomerNumber { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Company { get; private set; }
    public string? Branch { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid? MergedIntoCustomerId { get; private set; }

    private Customer() { } // EF Core

    public Customer(string customerNumber, string name, string? company, string? branch, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(customerNumber))
            throw new ArgumentException("Customer number is required.", nameof(customerNumber));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Id = Guid.NewGuid();
        CustomerNumber = customerNumber;
        Name = name;
        Company = company;
        Branch = branch;
        CreatedAtUtc = createdAtUtc;
    }

    public bool IsMerged => MergedIntoCustomerId is not null;

    public void MergeInto(Guid targetCustomerId)
    {
        if (targetCustomerId == Id)
            throw new InvalidOperationException("A customer cannot be merged into itself.");
        MergedIntoCustomerId = targetCustomerId;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ICustomerRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Customer?> GetByCustomerNumberAsync(string customerNumber, CancellationToken ct);
    Task<IReadOnlyList<Customer>> SearchAsync(string query, int take, CancellationToken ct);
    Task AddAsync(Customer customer, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, summary seam, and service

**Create file: `src/SupportCrm.Application/Customers/CustomerDtos.cs`**

```csharp
namespace SupportCrm.Application.Customers;

public record CreateCustomerRequest(string Name, string? Company, string? Branch);

public record CustomerDto(
    Guid Id,
    string CustomerNumber,
    string Name,
    string? Company,
    string? Branch,
    DateTimeOffset CreatedAtUtc);

public record CustomerSummaryDto(
    CustomerDto Customer,
    int OpenTicketCount,
    DateTimeOffset? LastInteractionAtUtc);

public record DuplicateCandidateDto(CustomerDto Customer, double Score);

public record MergeCustomersRequest(Guid SourceCustomerId, Guid TargetCustomerId);
```

**Create file: `src/SupportCrm.Application/Customers/ICustomerActivitySummaryProvider.cs`** — the seam the intake calls for, so a future Ticketing/interaction story can supply real data without touching `CustomerService`:

```csharp
namespace SupportCrm.Application.Customers;

/// <summary>
/// Supplies the "open tickets" / "last interaction" figures shown on a customer's profile summary.
/// No Ticketing or interaction-history module exists yet in this codebase; register
/// <see cref="StubCustomerActivitySummaryProvider"/> until one does.
/// </summary>
public interface ICustomerActivitySummaryProvider
{
    Task<(int OpenTicketCount, DateTimeOffset? LastInteractionAtUtc)> GetSummaryAsync(Guid customerId, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Customers/StubCustomerActivitySummaryProvider.cs`**

```csharp
namespace SupportCrm.Application.Customers;

public class StubCustomerActivitySummaryProvider : ICustomerActivitySummaryProvider
{
    public Task<(int OpenTicketCount, DateTimeOffset? LastInteractionAtUtc)> GetSummaryAsync(Guid customerId, CancellationToken ct)
        => Task.FromResult((0, (DateTimeOffset?)null));
}
```

**Create file: `src/SupportCrm.Application/Customers/CustomerNotFoundException.cs`**

```csharp
namespace SupportCrm.Application.Customers;

public class CustomerNotFoundException(Guid id) : Exception($"Customer '{id}' was not found.");
```

**Create file: `src/SupportCrm.Application/Customers/CustomerService.cs`**

```csharp
namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class CustomerService(ICustomerRepository repository, ICustomerActivitySummaryProvider activitySummaryProvider, TimeProvider timeProvider)
{
    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required.", nameof(request));

        var customerNumber = $"CUST-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
        var customer = new Customer(customerNumber, request.Name.Trim(), request.Company?.Trim(), request.Branch?.Trim(), timeProvider.GetUtcNow());

        await repository.AddAsync(customer, ct);
        await repository.SaveChangesAsync(ct);

        return ToDto(customer);
    }

    public async Task<CustomerSummaryDto> GetSummaryAsync(Guid id, CancellationToken ct)
    {
        var customer = await repository.GetByIdAsync(id, ct) ?? throw new CustomerNotFoundException(id);
        var (openTicketCount, lastInteractionAtUtc) = await activitySummaryProvider.GetSummaryAsync(id, ct);
        return new CustomerSummaryDto(ToDto(customer), openTicketCount, lastInteractionAtUtc);
    }

    public async Task<IReadOnlyList<DuplicateCandidateDto>> FindDuplicatesAsync(string name, string? company, CancellationToken ct)
    {
        var candidates = await repository.SearchAsync(name, take: 10, ct);
        return candidates
            .Where(c => !c.IsMerged)
            .Select(c => new DuplicateCandidateDto(ToDto(c), ScoreMatch(c, name, company)))
            .Where(d => d.Score > 0)
            .OrderByDescending(d => d.Score)
            .ToList();
    }

    public async Task MergeAsync(MergeCustomersRequest request, CancellationToken ct)
    {
        if (request.SourceCustomerId == request.TargetCustomerId)
            throw new ArgumentException("Source and target customer must differ.", nameof(request));

        var source = await repository.GetByIdAsync(request.SourceCustomerId, ct) ?? throw new CustomerNotFoundException(request.SourceCustomerId);
        _ = await repository.GetByIdAsync(request.TargetCustomerId, ct) ?? throw new CustomerNotFoundException(request.TargetCustomerId);

        source.MergeInto(request.TargetCustomerId);
        await repository.SaveChangesAsync(ct);
    }

    private static double ScoreMatch(Customer candidate, string name, string? company)
    {
        var score = 0.0;
        if (string.Equals(candidate.Name, name, StringComparison.OrdinalIgnoreCase)) score += 0.7;
        else if (candidate.Name.Contains(name, StringComparison.OrdinalIgnoreCase)) score += 0.3;
        if (!string.IsNullOrWhiteSpace(company) && string.Equals(candidate.Company, company, StringComparison.OrdinalIgnoreCase)) score += 0.3;
        return score;
    }

    private static CustomerDto ToDto(Customer c) => new(c.Id, c.CustomerNumber, c.Name, c.Company, c.Branch, c.CreatedAtUtc);
}
```

**Note:** `TimeProvider` is injected (not `DateTimeOffset.UtcNow` directly) so tests can control time deterministically — register `TimeProvider.System` in `Program.cs`.

### 3 — Infrastructure: `DbContext` + EF repository + migration

**Create file: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;

public class SupportCrmDbContext(DbContextOptions<SupportCrmDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(c => c.Id);
            entity.Property(c => c.CustomerNumber).IsRequired().HasMaxLength(32);
            entity.HasIndex(c => c.CustomerNumber).IsUnique();
            entity.Property(c => c.Name).IsRequired().HasMaxLength(256);
            entity.Property(c => c.Company).HasMaxLength(256);
            entity.Property(c => c.Branch).HasMaxLength(256);
            entity.Property(c => c.CreatedAtUtc).IsRequired();
        });
    }
}
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/CustomerRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class CustomerRepository(SupportCrmDbContext dbContext) : ICustomerRepository
{
    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Customers.FirstOrDefaultAsync(c => c.Id == id, ct);

    public Task<Customer?> GetByCustomerNumberAsync(string customerNumber, CancellationToken ct) =>
        dbContext.Customers.FirstOrDefaultAsync(c => c.CustomerNumber == customerNumber, ct);

    public async Task<IReadOnlyList<Customer>> SearchAsync(string query, int take, CancellationToken ct) =>
        await dbContext.Customers
            .Where(c => c.Name.Contains(query) || (c.Company != null && c.Company.Contains(query)))
            .OrderBy(c => c.Name)
            .Take(take)
            .ToListAsync(ct);

    public Task AddAsync(Customer customer, CancellationToken ct)
    {
        dbContext.Customers.Add(customer);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**Create file: `src/SupportCrm.Infrastructure/DependencyInjection.cs`**

```csharp
namespace SupportCrm.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportCrm.Application.Customers;
using SupportCrm.Domain.Repositories;
using SupportCrm.Infrastructure.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SupportCrmDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerActivitySummaryProvider, StubCustomerActivitySummaryProvider>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<CustomerService>();

        return services;
    }
}
```

- **After creating these files**, run `dotnet ef migrations add InitialCustomer --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root to generate the migration (requires `dotnet-ef` tool; install with `dotnet tool install --global dotnet-ef` if missing). Apply with `dotnet ef database update` **only** if a reachable Postgres instance is configured — do not fail the story on a missing local database, note it in Verification Steps instead.

### 4 — Api: DI wiring + controller

**File: `src/SupportCrm.Api/Program.cs`** — after `builder.Services.AddOpenApi();` (line 7) and before `var app = builder.Build();` (line 9), add:

```csharp
builder.Services.AddInfrastructure(builder.Configuration);
```

Add `using SupportCrm.Infrastructure;` at the top of the file.

**File: `src/SupportCrm.Api/appsettings.json`** — add:

```json
"ConnectionStrings": {
  "Default": "Host=localhost;Port=5432;Database=supportcrm;Username=postgres;Password=postgres"
}
```

Add the same key (with a `Development`-appropriate value, or omit if `appsettings.json`'s value already suffices) to `src/SupportCrm.Api/appsettings.Development.json`.

**Create file: `src/SupportCrm.Api/Controllers/CustomersController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Customers;

[ApiController]
[Route("api/customers")]
public class CustomersController(CustomerService customerService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var dto = await customerService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetSummary), new { id = dto.Id }, dto);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerSummaryDto>> GetSummary(Guid id, CancellationToken ct)
    {
        try
        {
            return await customerService.GetSummaryAsync(id, ct);
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("duplicates")]
    public async Task<ActionResult<IReadOnlyList<DuplicateCandidateDto>>> FindDuplicates([FromQuery] string name, [FromQuery] string? company, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest("Query parameter 'name' is required.");
        return Ok(await customerService.FindDuplicatesAsync(name, company, ct));
    }

    [HttpPost("merge")]
    public async Task<IActionResult> Merge([FromBody] MergeCustomersRequest request, CancellationToken ct)
    {
        try
        {
            await customerService.MergeAsync(request, ct);
            return NoContent();
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
    }
}
```

---

## Edge Cases & Failure Modes

- **Empty/whitespace name on create** — `Customer`'s constructor throws `ArgumentException`; `CustomerService.CreateAsync` also validates before constructing so the 400 path is hit before touching the domain type. Enforced in `src/SupportCrm.Domain/Entities/Customer.cs` and `src/SupportCrm.Application/Customers/CustomerService.cs`.
- **Duplicate `CustomerNumber` collision** — astronomically unlikely with an 8-hex-char suffix of a GUID, but the unique index on `CustomerNumber` (in `SupportCrmDbContext.OnModelCreating`) makes any collision surface as a `DbUpdateException` on `SaveChangesAsync` rather than silently overwriting data. Not caught explicitly in this story; document as a known gap if it needs a controller-level 409 later.
- **Merge into a non-existent or already-merged target** — `MergeAsync` throws `CustomerNotFoundException` if either id doesn't resolve; merging into an already-merged customer is currently allowed (chains merges) — flag this as an accepted simplification, not a bug, since the intake doesn't specify chain behavior.
- **Self-merge** — `Customer.MergeInto` throws `InvalidOperationException` when `targetCustomerId == Id`; `CustomerService.MergeAsync` throws `ArgumentException` earlier for the same condition from the request DTO.
- **Search with empty/very short query** — `SearchAsync`'s `Contains` filter will match broadly; the controller requires a non-empty `name` (400 otherwise) but does not enforce a minimum length. Acceptable for this story; the intake did not request a minimum-length rule.
- **No database reachable** — `AddDbContext` registration succeeds regardless (connection is lazy); the first request touching `SupportCrmDbContext` will throw `Npgsql.NpgsqlException` at that point, surfacing as a 500. No custom handling added in this story — out of scope per the intake's minimal-auth/minimal-infra framing, but call this out to the reviewer.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Customers/CustomerServiceTests.cs`** (new file/project if no test project exists yet; if none exists, create `tests/SupportCrm.Application.Tests/SupportCrm.Application.Tests.csproj` referencing `SupportCrm.Application` plus `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, and a mocking library such as `NSubstitute` or hand-rolled fakes for `ICustomerRepository`/`ICustomerActivitySummaryProvider`):
   - `CreateAsync_WithValidRequest_ReturnsCustomerWithGeneratedNumber`
   - `CreateAsync_WithEmptyName_ThrowsArgumentException`
   - `GetSummaryAsync_WithUnknownId_ThrowsCustomerNotFoundException`
   - `FindDuplicatesAsync_MatchesByNameAndCompany_ReturnsScoredCandidates`
   - `MergeAsync_WithSameSourceAndTarget_ThrowsArgumentException`
   - `MergeAsync_WithUnknownTarget_ThrowsCustomerNotFoundException`
2. **Unit — `tests/SupportCrm.Domain.Tests/Entities/CustomerTests.cs`** (same new-test-project note applies, `SupportCrm.Domain.Tests`):
   - `Constructor_WithBlankName_Throws`
   - `MergeInto_Self_Throws`
   - `MergeInto_ValidTarget_SetsMergedIntoCustomerId`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/CustomersControllerTests.cs`** (new test project `SupportCrm.Api.Tests` using `Microsoft.AspNetCore.Mvc.Testing`'s `WebApplicationFactory<Program>`; use an EF Core InMemory or SQLite in-memory provider for the `DbContext` in the test host to avoid requiring Postgres):
   - `Post_CreateCustomer_Returns201WithLocation`
   - `Get_UnknownCustomer_Returns404`
   - `Get_DuplicatesWithoutName_Returns400`
   - `Post_MergeCustomers_Returns204`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Backend tests:** `dotnet test SupportCrm.slnx` from `d:\Code\selfAssessment\backend` (once the test projects above exist).
3. **Migration generation:** `dotnet ef migrations add InitialCustomer --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from `d:\Code\selfAssessment\backend`. If `dotnet-ef` is not installed or no Postgres is reachable, note that explicitly rather than skipping silently.
4. **Manual smoke (optional, requires a running Postgres matching the connection string):** `dotnet run --project src/SupportCrm.Api`, then `POST /api/customers` with `{"name":"Acme Corp","company":"Acme","branch":"HQ"}` via the generated `SupportCrm.Api.http` file or Swagger UI, then `GET /api/customers/{id}`.

---

## Done Criteria

- [ ] A new customer profile can be created with name, company/branch, and a unique customer ID (`POST /api/customers`).
- [ ] A duplicate-detection/lookup capability exists (`GET /api/customers/duplicates`) that a future Ticketing story can call before linking a ticket to a customer — actual ticket-linking is out of scope and not implemented here.
- [ ] The profile summary endpoint (`GET /api/customers/{id}`) returns contact info fields (name/company/branch — full contact-details model is CM-2's scope), an open-tickets count, and a last-interaction date, both sourced from the `ICustomerActivitySummaryProvider` seam rather than hard-coded inline.
- [ ] Duplicate profiles can be merged by an authorized user (`POST /api/customers/merge`) — authorization itself is not enforced in this story per the technical hints, but the endpoint shape supports adding `[Authorize]` later without a breaking change.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
