# Story 02 — Contact details (Story: CM-2)

---

## Prerequisites

- Story 01 completed: [`01-story-CM-1.md`](01-story-CM-1.md) — provides the `Customer` aggregate, `SupportCrmDbContext`, and `AddInfrastructure` DI wiring this story extends.

---

## Story Goal

Support agents can:

1. Add multiple phone numbers, emails, and WhatsApp numbers to a customer, each with one marked **primary per type**.
2. Set/update a single postal address per customer (not multi-value — the intake's AC only requires multi-value + "primary per type" for phone/email/WhatsApp, not address).
3. Edit or add contact details and see who changed what, when (an audit trail).
4. Have malformed emails/phone numbers rejected with a clear error message.
5. Flag one **preferred contact channel** (Phone, Email, or WhatsApp) on the customer, readable by anything that later sends notifications (sending itself is out of scope).

**Assumption (no auth exists yet):** "who changed what" needs an actor identity, but no authentication middleware is wired up in this codebase (per CM-1's plan, `JwtBearer` is referenced but unused). This story accepts a `ChangedBy` string in each write request body rather than resolving a current user from a token. **Flag this explicitly** — when real auth lands, `ChangedBy` should switch to being resolved server-side from the authenticated principal, not client-supplied.

---

## Context — Read These Files First

1. [`01-story-CM-1.md`](01-story-CM-1.md), `## Backend Tasks` → `### 1` — the `Customer` entity pattern (private setters, a validating constructor, EF Core's parameterless private constructor). Follow the same pattern for new entities in this story.
2. `src/SupportCrm.Domain/Entities/Customer.cs` (38 lines, whole file) — `Customer` has `Id`, `CustomerNumber`, `Name`, `Company`, `Branch`, `CreatedAtUtc`, `MergedIntoCustomerId`. Add a nullable `PreferredContactChannel` enum property here (with a private setter and a public method to change it, matching `MergeInto`'s style) rather than exposing a public setter.
3. `src/SupportCrm.Application/Customers/CustomerService.cs` (62 lines, whole file) — existing `CustomerService` constructor takes `(ICustomerRepository repository, ICustomerActivitySummaryProvider activitySummaryProvider, TimeProvider timeProvider)` via primary-constructor syntax. New contact-detail operations belong in a **new** `ContactDetailService` (do not grow `CustomerService` — keep one aggregate's operations per service file, matching this file's single-responsibility scope).
4. `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs` (24 lines, whole file) — `OnModelCreating` currently configures only `Customer`. Add `DbSet<ContactDetail>` and `DbSet<ContactDetailChangeLogEntry>` plus their `entity.ToTable(...)` blocks in the same method, following the same fluent-config style (`HasKey`, `Property(...).IsRequired().HasMaxLength(...)`, `HasIndex`).
5. `src/SupportCrm.Infrastructure/DependencyInjection.cs` (24 lines, whole file) — `AddInfrastructure` registers `ICustomerRepository`, `ICustomerActivitySummaryProvider`, `TimeProvider`, `CustomerService` as `AddScoped`/`AddSingleton`. Add `IContactDetailRepository` and `ContactDetailService` registrations here in the same style.
6. `src/SupportCrm.Api/Controllers/CustomersController.cs` (51 lines, whole file) — `[ApiController]` + `[Route("api/customers")]` primary-constructor controller pattern with try/catch around `*NotFoundException` mapping to `NotFound()`. Add a **new** `ContactDetailsController` at `[Route("api/customers/{customerId:guid}/contact-details")]` rather than growing this file, matching the "one controller per aggregate-ish concern" split already implied by having a dedicated `CustomersController`.

No sibling plan in `.squad/plans/` yet covers contact details — Story 01 is the only precedent; follow its layering exactly (Domain entity → Application service/DTOs → Infrastructure EF config/repository → Api controller).

---

## Backend Tasks

### 1 — Domain: `ContactDetail`, `ContactChannelType`, audit entry, and `PreferredContactChannel` on `Customer`

**Create file: `src/SupportCrm.Domain/Entities/ContactChannelType.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public enum ContactChannelType
{
    Phone,
    Email,
    WhatsApp
}
```

**Create file: `src/SupportCrm.Domain/Entities/ContactDetail.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class ContactDetail
{
    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public ContactChannelType ChannelType { get; private set; }
    public string Value { get; private set; } = default!;
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private ContactDetail() { } // EF Core

    public ContactDetail(Guid customerId, ContactChannelType channelType, string value, bool isPrimary, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Contact value is required.", nameof(value));

        Id = Guid.NewGuid();
        CustomerId = customerId;
        ChannelType = channelType;
        Value = value;
        IsPrimary = isPrimary;
        CreatedAtUtc = createdAtUtc;
    }

    public void UpdateValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Contact value is required.", nameof(value));
        Value = value;
    }

    public void SetPrimary(bool isPrimary) => IsPrimary = isPrimary;
}
```

**Create file: `src/SupportCrm.Domain/Entities/ContactDetailChangeLogEntry.cs`** — one immutable row per create/update/primary-change:

```csharp
namespace SupportCrm.Domain.Entities;

public class ContactDetailChangeLogEntry
{
    public Guid Id { get; private set; }
    public Guid ContactDetailId { get; private set; }
    public Guid CustomerId { get; private set; }
    public string ChangeType { get; private set; } = default!; // "Created" | "ValueChanged" | "PrimaryChanged"
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public string ChangedBy { get; private set; } = default!;
    public DateTimeOffset ChangedAtUtc { get; private set; }

    private ContactDetailChangeLogEntry() { } // EF Core

    public ContactDetailChangeLogEntry(Guid contactDetailId, Guid customerId, string changeType, string? oldValue, string? newValue, string changedBy, DateTimeOffset changedAtUtc)
    {
        Id = Guid.NewGuid();
        ContactDetailId = contactDetailId;
        CustomerId = customerId;
        ChangeType = changeType;
        OldValue = oldValue;
        NewValue = newValue;
        ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        ChangedAtUtc = changedAtUtc;
    }
}
```

**File: `src/SupportCrm.Domain/Entities/Customer.cs`** — add alongside the existing properties (after `MergedIntoCustomerId`, ~line 10):

```csharp
    public ContactChannelType? PreferredContactChannel { get; private set; }
    public string? Address { get; private set; }
```

and add two public methods near `MergeInto` (~line 30):

```csharp
    public void SetPreferredContactChannel(ContactChannelType? channel) => PreferredContactChannel = channel;

    public void SetAddress(string? address) => Address = address;
```

### 2 — Application: validation, DTOs, `ContactDetailService`

**Create file: `src/SupportCrm.Application/Customers/ContactDetailValidation.cs`**

```csharp
namespace SupportCrm.Application.Customers;

using System.Text.RegularExpressions;
using SupportCrm.Domain.Entities;

public static class ContactDetailValidation
{
    private static readonly Regex EmailPattern = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(@"^\+?[0-9]{7,15}$", RegexOptions.Compiled);

    public static string? Validate(ContactChannelType channelType, string value) => channelType switch
    {
        ContactChannelType.Email => EmailPattern.IsMatch(value) ? null : "Enter a valid email address.",
        ContactChannelType.Phone or ContactChannelType.WhatsApp =>
            PhonePattern.IsMatch(value) ? null : "Enter a valid phone number (digits only, optionally starting with '+').",
        _ => "Unsupported contact channel."
    };
}
```

**Create file: `src/SupportCrm.Application/Customers/ContactDetailDtos.cs`**

```csharp
namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;

public record AddContactDetailRequest(ContactChannelType ChannelType, string Value, bool IsPrimary, string ChangedBy);
public record UpdateContactDetailRequest(string Value, string ChangedBy);
public record SetPrimaryContactDetailRequest(string ChangedBy);
public record SetPreferredChannelRequest(ContactChannelType? Channel, string ChangedBy);
public record SetAddressRequest(string? Address, string ChangedBy);

public record ContactDetailDto(Guid Id, ContactChannelType ChannelType, string Value, bool IsPrimary, DateTimeOffset CreatedAtUtc);

public record ContactDetailChangeLogDto(Guid Id, string ChangeType, string? OldValue, string? NewValue, string ChangedBy, DateTimeOffset ChangedAtUtc);
```

**Create file: `src/SupportCrm.Application/Customers/IContactDetailRepository.cs`** (Application-facing port; implemented in Infrastructure, following the same split as `ICustomerRepository` in Domain — this one lives in Application because it also persists change-log rows the Application layer constructs):

```csharp
namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;

public interface IContactDetailRepository
{
    Task<ContactDetail?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ContactDetail>> GetByCustomerAsync(Guid customerId, CancellationToken ct);
    Task<IReadOnlyList<ContactDetailChangeLogEntry>> GetChangeLogAsync(Guid customerId, CancellationToken ct);
    Task AddAsync(ContactDetail contactDetail, CancellationToken ct);
    Task AddChangeLogAsync(ContactDetailChangeLogEntry entry, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Customers/ContactDetailService.cs`**

```csharp
namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ContactDetailService(
    ICustomerRepository customerRepository,
    IContactDetailRepository contactDetailRepository,
    TimeProvider timeProvider)
{
    public async Task<ContactDetailDto> AddAsync(Guid customerId, AddContactDetailRequest request, CancellationToken ct)
    {
        _ = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);

        var validationError = ContactDetailValidation.Validate(request.ChannelType, request.Value);
        if (validationError is not null)
            throw new ArgumentException(validationError, nameof(request));

        var existing = await contactDetailRepository.GetByCustomerAsync(customerId, ct);
        var makePrimary = request.IsPrimary || !existing.Any(c => c.ChannelType == request.ChannelType);

        if (makePrimary)
            foreach (var other in existing.Where(c => c.ChannelType == request.ChannelType && c.IsPrimary))
                other.SetPrimary(false);

        var now = timeProvider.GetUtcNow();
        var contactDetail = new ContactDetail(customerId, request.ChannelType, request.Value.Trim(), makePrimary, now);
        await contactDetailRepository.AddAsync(contactDetail, ct);
        await contactDetailRepository.AddChangeLogAsync(
            new ContactDetailChangeLogEntry(contactDetail.Id, customerId, "Created", null, contactDetail.Value, request.ChangedBy, now), ct);
        await contactDetailRepository.SaveChangesAsync(ct);

        return ToDto(contactDetail);
    }

    public async Task<ContactDetailDto> UpdateValueAsync(Guid contactDetailId, UpdateContactDetailRequest request, CancellationToken ct)
    {
        var contactDetail = await contactDetailRepository.GetByIdAsync(contactDetailId, ct)
            ?? throw new KeyNotFoundException($"Contact detail '{contactDetailId}' was not found.");

        var validationError = ContactDetailValidation.Validate(contactDetail.ChannelType, request.Value);
        if (validationError is not null)
            throw new ArgumentException(validationError, nameof(request));

        var oldValue = contactDetail.Value;
        contactDetail.UpdateValue(request.Value.Trim());

        await contactDetailRepository.AddChangeLogAsync(
            new ContactDetailChangeLogEntry(contactDetail.Id, contactDetail.CustomerId, "ValueChanged", oldValue, contactDetail.Value, request.ChangedBy, timeProvider.GetUtcNow()), ct);
        await contactDetailRepository.SaveChangesAsync(ct);

        return ToDto(contactDetail);
    }

    public async Task SetPrimaryAsync(Guid contactDetailId, SetPrimaryContactDetailRequest request, CancellationToken ct)
    {
        var contactDetail = await contactDetailRepository.GetByIdAsync(contactDetailId, ct)
            ?? throw new KeyNotFoundException($"Contact detail '{contactDetailId}' was not found.");

        var siblings = await contactDetailRepository.GetByCustomerAsync(contactDetail.CustomerId, ct);
        foreach (var other in siblings.Where(c => c.ChannelType == contactDetail.ChannelType && c.Id != contactDetail.Id && c.IsPrimary))
            other.SetPrimary(false);

        contactDetail.SetPrimary(true);
        await contactDetailRepository.AddChangeLogAsync(
            new ContactDetailChangeLogEntry(contactDetail.Id, contactDetail.CustomerId, "PrimaryChanged", "false", "true", request.ChangedBy, timeProvider.GetUtcNow()), ct);
        await contactDetailRepository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ContactDetailDto>> GetForCustomerAsync(Guid customerId, CancellationToken ct) =>
        (await contactDetailRepository.GetByCustomerAsync(customerId, ct)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<ContactDetailChangeLogDto>> GetChangeLogAsync(Guid customerId, CancellationToken ct) =>
        (await contactDetailRepository.GetChangeLogAsync(customerId, ct))
            .OrderByDescending(e => e.ChangedAtUtc)
            .Select(e => new ContactDetailChangeLogDto(e.Id, e.ChangeType, e.OldValue, e.NewValue, e.ChangedBy, e.ChangedAtUtc))
            .ToList();

    private static ContactDetailDto ToDto(ContactDetail c) => new(c.Id, c.ChannelType, c.Value, c.IsPrimary, c.CreatedAtUtc);
}
```

**Create file: `src/SupportCrm.Application/Customers/CustomerProfileService.cs`** — small service for the two `Customer`-level fields this story adds (preferred channel, address), kept separate from `ContactDetailService` since it mutates `Customer` itself, not `ContactDetail` rows:

```csharp
namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Repositories;

public class CustomerProfileService(ICustomerRepository customerRepository)
{
    public async Task SetPreferredChannelAsync(Guid customerId, SetPreferredChannelRequest request, CancellationToken ct)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);
        customer.SetPreferredContactChannel(request.Channel);
        await customerRepository.SaveChangesAsync(ct);
    }

    public async Task SetAddressAsync(Guid customerId, SetAddressRequest request, CancellationToken ct)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);
        customer.SetAddress(request.Address);
        await customerRepository.SaveChangesAsync(ct);
    }
}
```

### 3 — Infrastructure: EF config + repository

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet` properties alongside `Customers` (~line 8):

```csharp
    public DbSet<ContactDetail> ContactDetails => Set<ContactDetail>();
    public DbSet<ContactDetailChangeLogEntry> ContactDetailChangeLogEntries => Set<ContactDetailChangeLogEntry>();
```

and inside `OnModelCreating`, after the existing `Customer` block, add:

```csharp
modelBuilder.Entity<Customer>(entity =>
{
    entity.Property(c => c.PreferredContactChannel).HasConversion<string?>();
    entity.Property(c => c.Address).HasMaxLength(512);
});

modelBuilder.Entity<ContactDetail>(entity =>
{
    entity.ToTable("ContactDetails");
    entity.HasKey(c => c.Id);
    entity.Property(c => c.ChannelType).HasConversion<string>().HasMaxLength(16).IsRequired();
    entity.Property(c => c.Value).IsRequired().HasMaxLength(256);
    entity.HasIndex(c => new { c.CustomerId, c.ChannelType });
});

modelBuilder.Entity<ContactDetailChangeLogEntry>(entity =>
{
    entity.ToTable("ContactDetailChangeLog");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.ChangeType).IsRequired().HasMaxLength(32);
    entity.Property(e => e.ChangedBy).IsRequired().HasMaxLength(256);
    entity.HasIndex(e => e.CustomerId);
});
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/ContactDetailRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Application.Customers;
using SupportCrm.Domain.Entities;

public class ContactDetailRepository(SupportCrmDbContext dbContext) : IContactDetailRepository
{
    public Task<ContactDetail?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.ContactDetails.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<ContactDetail>> GetByCustomerAsync(Guid customerId, CancellationToken ct) =>
        await dbContext.ContactDetails.Where(c => c.CustomerId == customerId).ToListAsync(ct);

    public async Task<IReadOnlyList<ContactDetailChangeLogEntry>> GetChangeLogAsync(Guid customerId, CancellationToken ct) =>
        await dbContext.ContactDetailChangeLogEntries.Where(e => e.CustomerId == customerId).ToListAsync(ct);

    public Task AddAsync(ContactDetail contactDetail, CancellationToken ct)
    {
        dbContext.ContactDetails.Add(contactDetail);
        return Task.CompletedTask;
    }

    public Task AddChangeLogAsync(ContactDetailChangeLogEntry entry, CancellationToken ct)
    {
        dbContext.ContactDetailChangeLogEntries.Add(entry);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — inside `AddInfrastructure`, alongside the existing registrations (~line 16):

```csharp
        services.AddScoped<IContactDetailRepository, ContactDetailRepository>();
        services.AddScoped<ContactDetailService>();
        services.AddScoped<CustomerProfileService>();
```

- After these files exist, run `dotnet ef migrations add AddContactDetails --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: controllers

**Create file: `src/SupportCrm.Api/Controllers/ContactDetailsController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Customers;

[ApiController]
[Route("api/customers/{customerId:guid}/contact-details")]
public class ContactDetailsController(ContactDetailService contactDetailService, CustomerProfileService customerProfileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContactDetailDto>>> GetAll(Guid customerId, CancellationToken ct) =>
        Ok(await contactDetailService.GetForCustomerAsync(customerId, ct));

    [HttpGet("history")]
    public async Task<ActionResult<IReadOnlyList<ContactDetailChangeLogDto>>> GetHistory(Guid customerId, CancellationToken ct) =>
        Ok(await contactDetailService.GetChangeLogAsync(customerId, ct));

    [HttpPost]
    public async Task<ActionResult<ContactDetailDto>> Add(Guid customerId, [FromBody] AddContactDetailRequest request, CancellationToken ct)
    {
        try
        {
            return await contactDetailService.AddAsync(customerId, request, ct);
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{contactDetailId:guid}")]
    public async Task<ActionResult<ContactDetailDto>> UpdateValue(Guid contactDetailId, [FromBody] UpdateContactDetailRequest request, CancellationToken ct)
    {
        try
        {
            return await contactDetailService.UpdateValueAsync(contactDetailId, request, ct);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{contactDetailId:guid}/set-primary")]
    public async Task<IActionResult> SetPrimary(Guid contactDetailId, [FromBody] SetPrimaryContactDetailRequest request, CancellationToken ct)
    {
        try
        {
            await contactDetailService.SetPrimaryAsync(contactDetailId, request, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("/api/customers/{customerId:guid}/preferred-channel")]
    public async Task<IActionResult> SetPreferredChannel(Guid customerId, [FromBody] SetPreferredChannelRequest request, CancellationToken ct)
    {
        try
        {
            await customerProfileService.SetPreferredChannelAsync(customerId, request, ct);
            return NoContent();
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("/api/customers/{customerId:guid}/address")]
    public async Task<IActionResult> SetAddress(Guid customerId, [FromBody] SetAddressRequest request, CancellationToken ct)
    {
        try
        {
            await customerProfileService.SetAddressAsync(customerId, request, ct);
            return NoContent();
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
    }
}
```

**Note the absolute route overrides** (`/api/customers/{customerId:guid}/preferred-channel` and `/address`) on a controller whose base route is `.../contact-details` — this is deliberate (these two actions are customer-level, not contact-detail-level) but is an unusual pattern worth a second look; an alternative is adding these two actions to `CustomersController` instead. **Flag this as a design choice for review, not a settled decision** — either placement works, but keep the two actions together as this plan places them.

---

## Edge Cases & Failure Modes

- **Malformed email/phone** — `ContactDetailValidation.Validate` (in `ContactDetailValidation.cs`) rejects and `ContactDetailService.AddAsync`/`UpdateValueAsync` throw `ArgumentException` with the validation message, mapped to `400 BadRequest` by the controller.
- **Adding a second primary of the same type** — `AddAsync` demotes any existing primary of that `ChannelType` before inserting when `request.IsPrimary` is `true`, or auto-promotes the new entry to primary when it's the type's first entry — enforced in `ContactDetailService.AddAsync`.
- **Setting primary on an already-primary contact detail** — idempotent; `SetPrimaryAsync` still writes a change-log row (harmless duplicate log entry) — acceptable simplification, not treated as an error.
- **`ChangedBy` missing/blank** — `ContactDetailChangeLogEntry`'s constructor coerces blank values to `"unknown"` rather than throwing, since there's no real auth yet to guarantee a value.
- **Updating a contact detail that doesn't exist** — `UpdateValueAsync`/`SetPrimaryAsync` throw `KeyNotFoundException`, mapped to `404` by the controller.
- **Customer not found on contact-detail or address/preferred-channel endpoints** — `CustomerNotFoundException` mapped to `404`.
- **Concurrent primary-flag changes on two contact details of the same type** — no optimistic concurrency token is added in this story; a race could leave two rows marked primary. Documented as a known gap, not fixed here (the intake doesn't call for concurrency handling).

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Customers/ContactDetailServiceTests.cs`** (new test project, per Story 01's note on adding `SupportCrm.Application.Tests` — see Story 01 Test Plan):
   - `AddAsync_WithMalformedEmail_ThrowsArgumentException`
   - `AddAsync_FirstOfType_IsAutoPrimary`
   - `AddAsync_SecondPrimaryOfSameType_DemotesPreviousPrimary`
   - `UpdateValueAsync_WritesChangeLogEntry`
   - `SetPrimaryAsync_UnknownId_ThrowsKeyNotFoundException`
2. **Unit — `tests/SupportCrm.Application.Tests/Customers/ContactDetailValidationTests.cs`**:
   - `Validate_ValidEmail_ReturnsNull`, `Validate_MalformedEmail_ReturnsMessage`
   - `Validate_ValidPhone_ReturnsNull`, `Validate_MalformedPhone_ReturnsMessage`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/ContactDetailsControllerTests.cs`**:
   - `Post_AddContactDetail_Returns200WithDto`
   - `Post_AddContactDetail_WithBadEmail_Returns400`
   - `Put_SetPreferredChannel_Returns204`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Backend tests:** `dotnet test SupportCrm.slnx` (once test projects exist).
3. **Migration generation:** `dotnet ef migrations add AddContactDetails --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from `d:\Code\selfAssessment\backend`.

---

## Done Criteria

- [ ] A customer can have multiple phone numbers, emails, and WhatsApp numbers, with one marked primary per type (`POST .../contact-details`, `POST .../set-primary`).
- [ ] Contact details can be edited and are versioned/logged (`PUT .../contact-details/{id}`, `GET .../contact-details/history`) — logging uses a client-supplied `ChangedBy` until real auth exists.
- [ ] Invalid email/phone formats are rejected with a clear error message (`ContactDetailValidation`, 400 responses).
- [ ] A preferred contact channel can be set and read back (`PUT/GET` via `CustomerProfileService` / profile summary).
- [ ] `dotnet build SupportCrm.slnx` succeeds.
