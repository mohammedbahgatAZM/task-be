# Story 05 — Create and track tickets (Story: TM-1)

---

## Prerequisites

- Customer Management Stories 01–04 completed (`../customer-management/01-story-CM-1.md`..`04-story-CM-4.md`) — provides `Customer`, `ContactDetail`, `SupportCrmDbContext`, `AddInfrastructure`, and the `ICustomerInteractionSource` / `ICustomerActivitySummaryProvider` seams this story plugs real implementations into.

---

## Story Goal

1. A ticket can be created — manually by an agent, or via a single shared **ingestion seam** standing in for future email/WhatsApp/chat/SMS/web-form adapters (none of those integrations exist in this codebase yet) — with a unique reference number, timestamp, and originating channel.
2. Ticket creation resolves the requester to an existing `Customer` (via CM-2's stored contact-detail values, then CM-1's name-similarity duplicate detection) or creates a new one — this is the concrete implementation of the assumption Customer Management's CM-1 flagged from day one ("opening a ticket links to the correct customer, no duplicates").
3. A requester can look up their ticket's current status and last update by reference number (no login exists in this codebase, so the reference number **is** the access credential for this story — see Edge Cases).
4. Every status change is timestamped and attributed to an actor (agent name or `"System"`), recorded in an audit trail that story TM-4 will build the public status-change/escalation actions on top of.
5. **This story also replaces Customer Management's `StubCustomerActivitySummaryProvider`** with a real implementation — the "open tickets" count and "last interaction" date on a customer's profile summary have been hardcoded stubs since CM-1 and were never revisited; this is where they become real.

**Not in scope:** real email/WhatsApp/chat/SMS/web-form channel adapters (TM-1 only builds the seam they would call); categories/priorities (TM-2); assignment (TM-3); the full status vocabulary/escalation actions (TM-4); the unified per-ticket history view (TM-5).

---

## Context — Read These Files First

1. `../customer-management/01-story-CM-1.md`, `## Backend Tasks` → `### 1`/`### 2` — the `Customer` entity and `CustomerService` pattern (private-setter entities, primary-constructor services, `TimeProvider` injection) this story's `Ticket` entity and `TicketService` must match exactly.
2. `src/SupportCrm.Application/Customers/CustomerService.cs` (62 lines, whole file) — `FindDuplicatesAsync(name, company, ct)` returns `IReadOnlyList<DuplicateCandidateDto>` scored 0–1; this story's customer-resolution logic calls it directly rather than re-implementing name matching.
3. `src/SupportCrm.Application/Customers/IContactDetailRepository.cs` (whole file, from CM-2) — currently has `GetByIdAsync`, `GetByCustomerAsync`, `GetChangeLogAsync`, `AddAsync`, `AddChangeLogAsync`, `SaveChangesAsync`. Add a new `FindByValueAsync(string value, CancellationToken ct): Task<ContactDetail?>` member — an exact match on a stored phone/email/WhatsApp value is a far stronger signal for "is this the same customer" than name similarity, and this story needs it before falling back to `FindDuplicatesAsync`.
4. `src/SupportCrm.Infrastructure/Persistence/ContactDetailRepository.cs` (whole file, from CM-2) — implement the new `FindByValueAsync` here (`dbContext.ContactDetails.FirstOrDefaultAsync(c => c.Value == value, ct)`), following this file's existing method style exactly.
5. `src/SupportCrm.Application/Customers/ICustomerActivitySummaryProvider.cs` and `StubCustomerActivitySummaryProvider.cs` (11 and 7 lines) — the interface this story provides a **real** implementation for, replacing the stub's DI registration (not deleting the stub file — keep it available/documented as a fallback for tests).
6. `src/SupportCrm.Application/Customers/ICustomerInteractionSource.cs` (16 lines, whole file, from CM-3) and `NotesInteractionSource.cs` (17 lines, whole file, from CM-4) — the multi-registration seam and its first real implementation; this story's `TicketInteractionSource` is the second.
7. `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs` (68 lines, whole file) — `OnModelCreating`'s existing per-entity fluent-config blocks; add `Ticket` and `TicketStatusChangeEntry` blocks in the same style, plus new `DbSet` properties alongside the existing ones (~line 10).
8. `src/SupportCrm.Infrastructure/DependencyInjection.cs` (36 lines, whole file) — registration order/style; this story adds `ITicketRepository`, `TicketCustomerResolver`, `TicketService`, `ICustomerInteractionSource → TicketInteractionSource`, and **replaces** the `ICustomerActivitySummaryProvider` registration.
9. `src/SupportCrm.Api/Controllers/CustomersController.cs` (74 lines, whole file) — controller conventions (primary-constructor, try/catch → `NotFound()`) to match in the new `TicketsController`.

---

## Backend Tasks

### 1 — Domain: `Ticket`, `TicketChannel`, `TicketStatus`, status audit trail

**Create file: `src/SupportCrm.Domain/Entities/TicketChannel.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public enum TicketChannel
{
    Manual,
    Email,
    WhatsApp,
    Chat,
    Sms,
    WebForm
}
```

**Create file: `src/SupportCrm.Domain/Entities/TicketStatus.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// Full vocabulary per the intake (New/Open/Pending/Resolved/Closed). This story only
// sets it to New on creation; TM-4 builds the public transition/escalation actions.
public enum TicketStatus
{
    New,
    Open,
    Pending,
    Resolved,
    Closed
}
```

**Create file: `src/SupportCrm.Domain/Entities/Ticket.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class Ticket
{
    public Guid Id { get; private set; }
    public string ReferenceNumber { get; private set; } = default!;
    public Guid CustomerId { get; private set; }
    public TicketChannel Channel { get; private set; }
    public string Subject { get; private set; } = default!;
    public string? Description { get; private set; }
    public TicketStatus Status { get; private set; }
    public string RequesterName { get; private set; } = default!;
    public string? RequesterContactValue { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    private Ticket() { } // EF Core

    public Ticket(string referenceNumber, Guid customerId, TicketChannel channel, string subject, string? description,
        string requesterName, string? requesterContactValue, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
            throw new ArgumentException("Reference number is required.", nameof(referenceNumber));
        if (string.IsNullOrWhiteSpace(subject))
            throw new ArgumentException("Subject is required.", nameof(subject));
        if (string.IsNullOrWhiteSpace(requesterName))
            throw new ArgumentException("Requester name is required.", nameof(requesterName));

        Id = Guid.NewGuid();
        ReferenceNumber = referenceNumber;
        CustomerId = customerId;
        Channel = channel;
        Subject = subject;
        Description = description;
        Status = TicketStatus.New;
        RequesterName = requesterName;
        RequesterContactValue = requesterContactValue;
        CreatedAtUtc = createdAtUtc;
    }

    public void SetStatus(TicketStatus status, DateTimeOffset atUtc)
    {
        Status = status;
        ClosedAtUtc = status is TicketStatus.Closed ? atUtc : null;
    }
}
```

**Create file: `src/SupportCrm.Domain/Entities/TicketStatusChangeEntry.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class TicketStatusChangeEntry
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public TicketStatus? OldStatus { get; private set; } // null for the initial "Created" entry
    public TicketStatus NewStatus { get; private set; }
    public string ChangedBy { get; private set; } = default!;
    public string ChangedByKind { get; private set; } = default!; // "Agent" | "System"
    public string? Reason { get; private set; }
    public DateTimeOffset ChangedAtUtc { get; private set; }

    private TicketStatusChangeEntry() { } // EF Core

    public TicketStatusChangeEntry(Guid ticketId, TicketStatus? oldStatus, TicketStatus newStatus,
        string changedBy, string changedByKind, string? reason, DateTimeOffset changedAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        ChangedBy = string.IsNullOrWhiteSpace(changedBy) ? "unknown" : changedBy;
        ChangedByKind = changedByKind is "Agent" or "System" ? changedByKind : "Agent";
        Reason = reason;
        ChangedAtUtc = changedAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ITicketRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Ticket?> GetByReferenceNumberAsync(string referenceNumber, CancellationToken ct);
    Task<IReadOnlyList<Ticket>> GetByCustomerAsync(Guid customerId, CancellationToken ct);
    Task<int> CountOpenByCustomerAsync(Guid customerId, CancellationToken ct);
    Task AddAsync(Ticket ticket, CancellationToken ct);
    Task<IReadOnlyList<TicketStatusChangeEntry>> GetStatusHistoryAsync(Guid ticketId, CancellationToken ct);
    Task AddStatusChangeAsync(TicketStatusChangeEntry entry, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, customer resolution, `TicketService`, real activity-summary provider, timeline source

**Create file: `src/SupportCrm.Application/Tickets/TicketDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record CreateTicketRequest(
    TicketChannel Channel,
    string Subject,
    string? Description,
    string RequesterName,
    string? RequesterContactValue,
    string CreatedBy);

public record TicketDto(
    Guid Id,
    string ReferenceNumber,
    Guid CustomerId,
    TicketChannel Channel,
    string Subject,
    string? Description,
    TicketStatus Status,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ClosedAtUtc);

public record TicketStatusViewDto(string ReferenceNumber, TicketStatus Status, DateTimeOffset LastUpdatedAtUtc);

public class TicketNotFoundException(string reference) : Exception($"Ticket '{reference}' was not found.");
```

**Extend file: `src/SupportCrm.Application/Customers/IContactDetailRepository.cs`** — add one member to the existing interface:

```csharp
    Task<ContactDetail?> FindByValueAsync(string value, CancellationToken ct);
```

**Create file: `src/SupportCrm.Application/Tickets/TicketCustomerResolver.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Application.Customers;
using SupportCrm.Domain.Repositories;

/// <summary>
/// Resolves a ticket requester (name + optional contact value) to a Customer id,
/// reusing Customer Management's contact-detail lookup and duplicate-detection
/// rather than re-implementing matching here. This is the concrete fix for the
/// assumption Customer Management's CM-1 flagged: "opening a ticket links to the
/// correct existing customer profile (no duplicates)".
/// </summary>
public class TicketCustomerResolver(
    IContactDetailRepository contactDetailRepository,
    CustomerService customerService,
    TimeProvider timeProvider)
{
    private const double StrongNameMatchThreshold = 0.7;

    public async Task<Guid> ResolveCustomerIdAsync(string requesterName, string? requesterContactValue, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(requesterContactValue))
        {
            var existingContact = await contactDetailRepository.FindByValueAsync(requesterContactValue, ct);
            if (existingContact is not null)
                return existingContact.CustomerId;
        }

        var candidates = await customerService.FindDuplicatesAsync(requesterName, null, ct);
        var strongMatch = candidates.FirstOrDefault(c => c.Score >= StrongNameMatchThreshold);
        if (strongMatch is not null)
            return strongMatch.Customer.Id;

        var created = await customerService.CreateAsync(new CreateCustomerRequest(requesterName, null, null), ct);
        return created.Id;
    }
}
```

**Create file: `src/SupportCrm.Application/Tickets/TicketService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketService(
    ITicketRepository ticketRepository,
    TicketCustomerResolver customerResolver,
    TimeProvider timeProvider)
{
    public async Task<TicketDto> CreateAsync(CreateTicketRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Subject))
            throw new ArgumentException("Subject is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.RequesterName))
            throw new ArgumentException("Requester name is required.", nameof(request));

        var customerId = await customerResolver.ResolveCustomerIdAsync(request.RequesterName, request.RequesterContactValue, ct);
        var now = timeProvider.GetUtcNow();
        var referenceNumber = $"TCK-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        var ticket = new Ticket(referenceNumber, customerId, request.Channel, request.Subject.Trim(),
            request.Description?.Trim(), request.RequesterName.Trim(), request.RequesterContactValue?.Trim(), now);

        await ticketRepository.AddAsync(ticket, ct);
        await ticketRepository.AddStatusChangeAsync(
            new TicketStatusChangeEntry(ticket.Id, null, TicketStatus.New, request.CreatedBy, "Agent", null, now), ct);
        await ticketRepository.SaveChangesAsync(ct);

        return ToDto(ticket);
    }

    public async Task<TicketDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(id, ct) ?? throw new TicketNotFoundException(id.ToString());
        return ToDto(ticket);
    }

    public async Task<TicketStatusViewDto> GetStatusByReferenceAsync(string referenceNumber, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByReferenceNumberAsync(referenceNumber, ct)
            ?? throw new TicketNotFoundException(referenceNumber);
        var history = await ticketRepository.GetStatusHistoryAsync(ticket.Id, ct);
        var lastUpdate = history.Count > 0 ? history.Max(h => h.ChangedAtUtc) : ticket.CreatedAtUtc;
        return new TicketStatusViewDto(ticket.ReferenceNumber, ticket.Status, lastUpdate);
    }

    /// <summary>
    /// Records a status change + audit entry. Internal building block for TM-4's public
    /// status-change/escalation endpoints — TM-1 only calls it once, for ticket creation.
    /// </summary>
    public async Task RecordStatusChangeAsync(Guid ticketId, TicketStatus newStatus, string changedBy, string changedByKind, string? reason, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var now = timeProvider.GetUtcNow();
        var oldStatus = ticket.Status;
        ticket.SetStatus(newStatus, now);
        await ticketRepository.AddStatusChangeAsync(
            new TicketStatusChangeEntry(ticketId, oldStatus, newStatus, changedBy, changedByKind, reason, now), ct);
        await ticketRepository.SaveChangesAsync(ct);
    }

    private static TicketDto ToDto(Ticket t) => new(t.Id, t.ReferenceNumber, t.CustomerId, t.Channel, t.Subject, t.Description, t.Status, t.CreatedAtUtc, t.ClosedAtUtc);
}
```

### 3 — Application: replace the customer activity-summary stub, add the ticket timeline source

**Create file: `src/SupportCrm.Application/Customers/CustomerActivitySummaryProvider.cs`** (in the existing `Customers` namespace — it implements `Customers`' own seam, even though it now depends on ticket data; this is the intended cross-feature wiring point, not a layering violation, since `Application` already references both):

```csharp
namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

/// <summary>
/// Replaces <see cref="StubCustomerActivitySummaryProvider"/> (registered by Customer
/// Management's CM-1, before any real interaction data existed). "Open tickets" comes
/// from the ticket repository directly; "last interaction" is computed the same way the
/// CM-3 timeline is — by asking every registered <see cref="ICustomerInteractionSource"/>
/// for its most recent entry — so this provider never needs to change again as new
/// interaction sources are added.
/// </summary>
public class CustomerActivitySummaryProvider(
    ITicketRepository ticketRepository,
    IEnumerable<ICustomerInteractionSource> interactionSources) : ICustomerActivitySummaryProvider
{
    public async Task<(int OpenTicketCount, DateTimeOffset? LastInteractionAtUtc)> GetSummaryAsync(Guid customerId, CancellationToken ct)
    {
        var openTicketCount = await ticketRepository.CountOpenByCustomerAsync(customerId, ct);

        var perSourceResults = await Task.WhenAll(
            interactionSources.Select(s => s.GetInteractionsAsync(customerId, null, null, null, ct)));
        var lastInteractionAtUtc = perSourceResults
            .SelectMany(r => r)
            .Select(i => (DateTimeOffset?)i.OccurredAtUtc)
            .Max();

        return (openTicketCount, lastInteractionAtUtc);
    }
}
```

**Create file: `src/SupportCrm.Application/Tickets/TicketInteractionSource.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Application.Customers;
using SupportCrm.Domain.Repositories;

public class TicketInteractionSource(ITicketRepository ticketRepository) : ICustomerInteractionSource
{
    public async Task<IReadOnlyList<CustomerInteractionDto>> GetInteractionsAsync(
        Guid customerId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, string? agentName, CancellationToken ct)
    {
        var tickets = await ticketRepository.GetByCustomerAsync(customerId, ct);

        return tickets
            .Where(t => fromUtc is null || t.CreatedAtUtc >= fromUtc)
            .Where(t => toUtc is null || t.CreatedAtUtc <= toUtc)
            .Select(t => new CustomerInteractionDto(t.Id, "Ticket", t.CreatedAtUtc, $"{t.ReferenceNumber}: {t.Subject}", null, $"/tickets/{t.Id}"))
            .ToList();
    }
}
```

**Note for the executor:** this source only emits one entry per ticket (its creation) — status changes are not yet surfaced in the CM-3 timeline in this story; that refinement (each status change also appearing) is a natural follow-up once TM-4 exists, and is intentionally out of scope here to keep this story's timeline contribution simple and correct.

### 4 — Infrastructure: EF config, repository, DI wiring

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet` properties alongside the existing ones and two new `OnModelCreating` blocks, following the file's existing style exactly (`ToTable`, `HasKey`, `Property(...).IsRequired().HasMaxLength(...)`, `HasIndex`, `HasConversion<string>()` for enums):

```csharp
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketStatusChangeEntry> TicketStatusChangeEntries => Set<TicketStatusChangeEntry>();
```

```csharp
modelBuilder.Entity<Ticket>(entity =>
{
    entity.ToTable("Tickets");
    entity.HasKey(t => t.Id);
    entity.Property(t => t.ReferenceNumber).IsRequired().HasMaxLength(32);
    entity.HasIndex(t => t.ReferenceNumber).IsUnique();
    entity.Property(t => t.Channel).HasConversion<string>().HasMaxLength(16).IsRequired();
    entity.Property(t => t.Subject).IsRequired().HasMaxLength(256);
    entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
    entity.Property(t => t.RequesterName).IsRequired().HasMaxLength(256);
    entity.Property(t => t.RequesterContactValue).HasMaxLength(256);
    entity.HasIndex(t => t.CustomerId);
});

modelBuilder.Entity<TicketStatusChangeEntry>(entity =>
{
    entity.ToTable("TicketStatusChanges");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.OldStatus).HasConversion<string?>().HasMaxLength(16);
    entity.Property(e => e.NewStatus).HasConversion<string>().HasMaxLength(16).IsRequired();
    entity.Property(e => e.ChangedBy).IsRequired().HasMaxLength(256);
    entity.Property(e => e.ChangedByKind).IsRequired().HasMaxLength(16);
    entity.HasIndex(e => e.TicketId);
});
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/TicketRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketRepository(SupportCrmDbContext dbContext) : ITicketRepository
{
    private static readonly TicketStatus[] OpenStatuses = { TicketStatus.New, TicketStatus.Open, TicketStatus.Pending };

    public Task<Ticket?> GetByIdAsync(Guid id, CancellationToken ct) =>
        dbContext.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Ticket?> GetByReferenceNumberAsync(string referenceNumber, CancellationToken ct) =>
        dbContext.Tickets.FirstOrDefaultAsync(t => t.ReferenceNumber == referenceNumber, ct);

    public async Task<IReadOnlyList<Ticket>> GetByCustomerAsync(Guid customerId, CancellationToken ct) =>
        await dbContext.Tickets.Where(t => t.CustomerId == customerId).ToListAsync(ct);

    public Task<int> CountOpenByCustomerAsync(Guid customerId, CancellationToken ct) =>
        dbContext.Tickets.CountAsync(t => t.CustomerId == customerId && OpenStatuses.Contains(t.Status), ct);

    public Task AddAsync(Ticket ticket, CancellationToken ct)
    {
        dbContext.Tickets.Add(ticket);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TicketStatusChangeEntry>> GetStatusHistoryAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketStatusChangeEntries.Where(e => e.TicketId == ticketId).ToListAsync(ct);

    public Task AddStatusChangeAsync(TicketStatusChangeEntry entry, CancellationToken ct)
    {
        dbContext.TicketStatusChangeEntries.Add(entry);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/Persistence/ContactDetailRepository.cs`** — add the new interface member:

```csharp
    public Task<ContactDetail?> FindByValueAsync(string value, CancellationToken ct) =>
        dbContext.ContactDetails.FirstOrDefaultAsync(c => c.Value == value, ct);
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — inside `AddInfrastructure`: **replace** the existing `services.AddScoped<ICustomerActivitySummaryProvider, StubCustomerActivitySummaryProvider>();` line with:

```csharp
        services.AddScoped<ICustomerActivitySummaryProvider, CustomerActivitySummaryProvider>();
```

and add, alongside the other registrations:

```csharp
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<Application.Tickets.TicketCustomerResolver>();
        services.AddScoped<Application.Tickets.TicketService>();
        services.AddScoped<ICustomerInteractionSource, Application.Tickets.TicketInteractionSource>();
```

(Fully-qualifying the `Tickets` types here avoids a `using` collision with the existing `Customers`-namespace imports already in this file — the executor may add a `using SupportCrm.Application.Tickets;` instead if preferred, there is no existing type-name collision to avoid.)

### 5 — Api: controller

**Create file: `src/SupportCrm.Api/Controllers/TicketsController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/tickets")]
public class TicketsController(TicketService ticketService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<TicketDto>> Create([FromBody] CreateTicketRequest request, CancellationToken ct)
    {
        try
        {
            var dto = await ticketService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDto>> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            return await ticketService.GetByIdAsync(id, ct);
        }
        catch (TicketNotFoundException)
        {
            return NotFound();
        }
    }

    // Requester-facing lookup by reference number — no auth exists in this codebase yet,
    // so the reference number is the access credential for this story (see plan Edge Cases).
    [HttpGet("reference/{referenceNumber}/status")]
    public async Task<ActionResult<TicketStatusViewDto>> GetStatusByReference(string referenceNumber, CancellationToken ct)
    {
        try
        {
            return await ticketService.GetStatusByReferenceAsync(referenceNumber, ct);
        }
        catch (TicketNotFoundException)
        {
            return NotFound();
        }
    }
}
```

- After creating these files, run `dotnet ef migrations add AddTickets --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

---

## Edge Cases & Failure Modes

- **Requester status lookup has no auth check** — anyone who knows (or guesses) a reference number can view that ticket's status via `GET /api/tickets/reference/{referenceNumber}/status`. Reference numbers use an 8-hex-char GUID slice (`TCK-XXXXXXXX`), which is not brute-forceable in practice, but this is still a documented gap: once real authentication exists, this endpoint should verify the requester's identity (e.g. matching contact value) rather than trusting the reference number alone.
- **`RequesterContactValue` matches a `ContactDetail` belonging to a merged (non-canonical) customer** — `TicketCustomerResolver` does not check `Customer.IsMerged`/`MergedIntoCustomerId` before returning `existingContact.CustomerId`; a ticket could attach to an already-merged-away customer record. Documented as a known gap — CM-1's merge feature is itself unfinished (no UI to trigger it), so this interaction hasn't been exercised yet.
- **Name-similarity match is borderline (score between 0 and 0.7)** — `TicketCustomerResolver` does not surface these to an agent for confirmation; it silently creates a new customer. This mirrors CM-1's create-flow (duplicate candidates are shown to the *agent* creating a profile, not to this automated resolution path) — flag as an acceptable simplification for this story, not a bug.
- **Zero registered `ICustomerInteractionSource` implementations somehow reachable** — `CustomerActivitySummaryProvider.GetSummaryAsync`'s `.Max()` on an empty sequence of nullable `DateTimeOffset?` returns `null` correctly (not an exception, since the sequence element type is nullable) — verified behavior, not assumed.
- **Blank `Subject`/`RequesterName`** — `Ticket`'s constructor and `TicketService.CreateAsync` both validate, surfacing as `400`.
- **Unknown reference number** — `TicketNotFoundException` → `404`.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketCustomerResolverTests.cs`** (new test project, per Customer Management CM-1's Test Plan note):
   - `ResolveCustomerIdAsync_WithMatchingContactValue_ReturnsThatCustomerId`
   - `ResolveCustomerIdAsync_WithStrongNameMatch_ReturnsThatCustomerId`
   - `ResolveCustomerIdAsync_WithNoMatch_CreatesNewCustomer`
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketServiceTests.cs`**:
   - `CreateAsync_GeneratesUniqueReferenceNumberAndInitialStatusChangeEntry`
   - `GetStatusByReferenceAsync_UnknownReference_ThrowsTicketNotFoundException`
3. **Unit — `tests/SupportCrm.Application.Tests/Customers/CustomerActivitySummaryProviderTests.cs`**:
   - `GetSummaryAsync_CountsOnlyOpenTickets`
   - `GetSummaryAsync_LastInteraction_IsMaxAcrossAllRegisteredSources` (use two fake `ICustomerInteractionSource`s)
4. **Integration — `tests/SupportCrm.Api.Tests/Controllers/TicketsControllerTests.cs`**:
   - `Post_CreateTicket_Returns201WithReferenceNumber`
   - `Get_StatusByUnknownReference_Returns404`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Backend tests:** `dotnet test SupportCrm.slnx` (once test projects exist).
3. **Migration generation:** `dotnet ef migrations add AddTickets --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from `d:\Code\selfAssessment\backend`.
4. **Manual smoke:** create a customer (CM-1 endpoint), then `POST /api/tickets` with that customer's contact value as `requesterContactValue`, confirm the returned `customerId` matches; then `GET /api/customers/{customerId}` and confirm `openTicketCount` is now `1` (previously always `0`).

---

## Done Criteria

- [ ] A ticket is created with a unique reference number, timestamp, and originating channel (`POST /api/tickets`).
- [ ] Manual creation works end-to-end; a shared ingestion seam stands in for automatic email/WhatsApp/chat/SMS/web-form creation (no real adapters built).
- [ ] Ticket creation resolves to an existing customer (by contact value or strong name match) or creates a new one — closing Customer Management CM-1's long-standing assumption.
- [ ] A requester can view current status + last update by reference number (`GET /api/tickets/reference/{referenceNumber}/status`).
- [ ] Every status change is timestamped and attributed (`TicketStatusChangeEntry`, written on creation; TM-4 adds more write paths).
- [ ] Customer Management's profile summary now shows real `openTicketCount` and `lastInteractionAtUtc` instead of the CM-1 stub.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
