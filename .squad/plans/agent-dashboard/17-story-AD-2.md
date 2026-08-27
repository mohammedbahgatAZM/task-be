# Story 17 — Customer information (Story: AD-2)

---

## Prerequisites

- Story 16 completed (`16-story-AD-1.md`) — the "acting as" agent-identity parameter pattern this story's masking check follows.
- Customer Management Stories 01–02 (`Customer`, `ContactDetail`), Ticket Management Story 05 (`ITicketRepository.GetByCustomerAsync`).

---

## Story Goal

1. `Customer` gets two account flags: `IsVip`, `IsAtRisk`.
2. `Agent` gets one permission flag: `CanViewSensitiveData` (default `false`).
3. A single "agent panel" endpoint returns the customer's profile (with flags), contact details, open ticket count, and a short past-tickets list — with `Address` and every `ContactDetail.Value` masked server-side when the requesting agent lacks `CanViewSensitiveData`.

**Not in scope:** a general roles/permissions system; masking anywhere other than this endpoint.

---

## Context — Read These Files First

1. `src/SupportCrm.Domain/Entities/Customer.cs`, `src/SupportCrm.Application/Customers/CustomerDtos.cs` — the flags this story adds.
2. `src/SupportCrm.Domain/Entities/Agent.cs`, `AgentTeamDtos.cs` (`AgentDto`, extended by Story 16 with `IsAvailable`) — add `CanViewSensitiveData` alongside it.
3. `src/SupportCrm.Application/Customers/ContactDetailService.cs`'s `GetForCustomerAsync`/`ContactDetailDto` — reused as-is, masking happens on the DTOs it returns, not inside that service.
4. `src/SupportCrm.Domain/Repositories/ITicketRepository.cs`'s `GetByCustomerAsync` (Ticket Management Story 05) — reused for the past-tickets list.

---

## Backend Tasks

### 1 — Domain: two new fields, no new tables

**File: `Customer.cs`** — add:

```csharp
    public bool IsVip { get; private set; }
    public bool IsAtRisk { get; private set; }
```

and a method:

```csharp
    public void SetAccountFlags(bool isVip, bool isAtRisk)
    {
        IsVip = isVip;
        IsAtRisk = isAtRisk;
    }
```

**File: `Agent.cs`** — add:

```csharp
    public bool CanViewSensitiveData { get; private set; }
```

and:

```csharp
    public void SetSensitiveDataAccess(bool canView) => CanViewSensitiveData = canView;
```

### 2 — Application: DTOs, masking, panel service

**File: `CustomerDtos.cs`** — extend `CustomerDto` with the two flags:

```csharp
public record CustomerDto(
    Guid Id, string CustomerNumber, string Name, string? Company, string? Branch, DateTimeOffset CreatedAtUtc,
    string? Address, ContactChannelType? PreferredContactChannel, bool IsVip, bool IsAtRisk);
```

(Update every `new CustomerDto(...)` construction site — grep for them before considering this task done; there are at least `CustomerService`'s create/summary paths.)

Add:

```csharp
public record SetCustomerAccountFlagsRequest(bool IsVip, bool IsAtRisk);
```

**File: `AgentTeamDtos.cs`** — extend `AgentDto` (already touched by Story 16) with the permission flag:

```csharp
public record AgentDto(Guid Id, string Name, bool IsAvailable, bool CanViewSensitiveData);
public record SetAgentSensitiveDataAccessRequest(bool CanViewSensitiveData);
```

**Create file: `src/SupportCrm.Application/Customers/CustomerAgentPanelDtos.cs`**

```csharp
namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;

public record CustomerPastTicketDto(Guid Id, string ReferenceNumber, string Subject, TicketStatus Status);

public record CustomerAgentPanelDto(
    CustomerDto Customer,
    IReadOnlyList<ContactDetailDto> ContactDetails,
    int OpenTicketCount,
    IReadOnlyList<CustomerPastTicketDto> PastTickets,
    bool IsSensitiveDataMasked);
```

**Create file: `src/SupportCrm.Application/Customers/CustomerAgentPanelService.cs`**

```csharp
namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

/// <summary>
/// Masks Customer.Address and every ContactDetail.Value server-side when the requesting
/// agent lacks CanViewSensitiveData — the client never decides this itself. There is no
/// auth middleware in this app, so "the requesting agent" arrives as an explicit id
/// (same "acting as" pattern as Story 16), and this service looks up *that* agent's own
/// flag rather than trusting anything the caller claims about its own permissions.
/// </summary>
public class CustomerAgentPanelService(
    ICustomerRepository customerRepository,
    IContactDetailRepository contactDetailRepository,
    ITicketRepository ticketRepository,
    IAgentRepository agentRepository)
{
    private const string MaskedPlaceholder = "•••• (restricted)";

    public async Task<CustomerAgentPanelDto> GetPanelAsync(Guid customerId, Guid requestingAgentId, CancellationToken ct)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);
        var requestingAgent = await agentRepository.GetByIdAsync(requestingAgentId, ct);
        var canView = requestingAgent?.CanViewSensitiveData ?? false;

        var contactDetails = await contactDetailRepository.GetByCustomerAsync(customerId, ct);
        var tickets = await ticketRepository.GetByCustomerAsync(customerId, ct);

        var customerDto = new CustomerDto(
            customer.Id, customer.CustomerNumber, customer.Name, customer.Company, customer.Branch, customer.CreatedAtUtc,
            Mask(customer.Address, canView), customer.PreferredContactChannel, customer.IsVip, customer.IsAtRisk);

        var contactDetailDtos = contactDetails
            .Select(c => new ContactDetailDto(c.Id, c.ChannelType, canView ? c.Value : MaskedPlaceholder, c.IsPrimary, c.CreatedAtUtc))
            .ToList();

        var openStatuses = new[] { TicketStatus.New, TicketStatus.Open, TicketStatus.Pending };
        var pastTickets = tickets
            .OrderByDescending(t => t.CreatedAtUtc)
            .Take(10)
            .Select(t => new CustomerPastTicketDto(t.Id, t.ReferenceNumber, t.Subject, t.Status))
            .ToList();

        return new CustomerAgentPanelDto(
            customerDto, contactDetailDtos, tickets.Count(t => openStatuses.Contains(t.Status)), pastTickets, !canView);
    }

    private static string? Mask(string? value, bool canView) =>
        canView || value is null ? value : MaskedPlaceholder;
}
```

**File: `AgentService.cs`** — add:

```csharp
    public async Task SetSensitiveDataAccessAsync(Guid agentId, bool canView, CancellationToken ct)
    {
        var agent = await repository.GetByIdAsync(agentId, ct) ?? throw new KeyNotFoundException($"Agent '{agentId}' was not found.");
        agent.SetSensitiveDataAccess(canView);
        await repository.SaveChangesAsync(ct);
    }
```

(Update `CreateAsync`/`GetAllAsync`'s `AgentDto` construction to include `agent.CanViewSensitiveData`.)

**File: `CustomerService.cs`** — add:

```csharp
    public async Task SetAccountFlagsAsync(Guid customerId, SetCustomerAccountFlagsRequest request, CancellationToken ct)
    {
        var customer = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);
        customer.SetAccountFlags(request.IsVip, request.IsAtRisk);
        await customerRepository.SaveChangesAsync(ct);
    }
```

(Update every other `new CustomerDto(...)` call site in this file to pass `customer.IsVip, customer.IsAtRisk`.)

### 3 — Infrastructure: EF config, DI

**File: `SupportCrmDbContext.cs`** — no new `entity.Property` calls strictly required (plain `bool` columns map by convention), but for consistency with the rest of `OnModelCreating`, add inside the existing `Customer`/`Agent` entity configuration blocks: no explicit line needed — EF Core maps `bool` properties automatically. (If the `Customer`/`Agent` entity blocks don't exist as explicit `modelBuilder.Entity<...>()` calls yet, leave convention-based mapping as-is; do not introduce a block solely for two booleans.)

**File: `DependencyInjection.cs`** — add `services.AddScoped<CustomerAgentPanelService>();`.

### 4 — Api: controllers

**File: `CustomersController.cs`** — add:

```csharp
    [HttpGet("{id:guid}/agent-panel")]
    public async Task<ActionResult<CustomerAgentPanelDto>> GetAgentPanel(
        Guid id, [FromQuery] Guid requestingAgentId, [FromServices] CustomerAgentPanelService panelService, CancellationToken ct)
    {
        try { return await panelService.GetPanelAsync(id, requestingAgentId, ct); }
        catch (CustomerNotFoundException) { return NotFound(); }
    }

    [HttpPut("{id:guid}/account-flags")]
    public async Task<IActionResult> SetAccountFlags(Guid id, [FromBody] SetCustomerAccountFlagsRequest request, CancellationToken ct)
    {
        try { await customerService.SetAccountFlagsAsync(id, request, ct); return NoContent(); }
        catch (CustomerNotFoundException) { return NotFound(); }
    }
```

**File: `AgentsController.cs`** — add:

```csharp
    [HttpPut("{id:guid}/sensitive-data-access")]
    public async Task<IActionResult> SetSensitiveDataAccess(Guid id, [FromBody] SetAgentSensitiveDataAccessRequest request, CancellationToken ct)
    {
        try { await agentService.SetSensitiveDataAccessAsync(id, request.CanViewSensitiveData, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
```

---

## Edge Cases & Failure Modes

- **`requestingAgentId` doesn't resolve to a real agent** (bad id, or the "acting as" switcher hasn't loaded yet) — `requestingAgent` is `null`, `canView` defaults to `false` — **fails closed**, masking sensitive data rather than accidentally exposing it.
- **`customer.Address` is `null` already** — `Mask` returns `null`, not the placeholder — there's nothing to hide, so nothing is substituted.
- **Every existing `new CustomerDto(...)` call site** — flagged explicitly: the executor must grep for all of them (not just `CustomerAgentPanelService`'s new one) before considering this story done, since the record's shape changed.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Customers/CustomerAgentPanelServiceTests.cs`**:
   - `GetPanelAsync_AgentWithoutPermission_MasksAddressAndContactDetails`
   - `GetPanelAsync_AgentWithPermission_ReturnsRealValues`
   - `GetPanelAsync_UnknownRequestingAgent_MasksByDefault`
   - `GetPanelAsync_NullAddress_StaysNullNotMasked`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Manual smoke:** create an agent with `CanViewSensitiveData: false`, call the panel endpoint as that agent, confirm masked values; toggle the flag, confirm real values.

---

## Done Criteria

- [ ] `GET /api/customers/{id}/agent-panel?requestingAgentId=...` returns profile + contact details + open/past tickets.
- [ ] `Address`/contact detail values are masked server-side for agents without `CanViewSensitiveData`, and only for them.
- [ ] `PUT /api/customers/{id}/account-flags` and `PUT /api/agents/{id}/sensitive-data-access` work.
- [ ] `dotnet build SupportCrm.slnx` succeeds. Migration needed: `Customer.IsVip`/`IsAtRisk`, `Agent.CanViewSensitiveData` columns.
