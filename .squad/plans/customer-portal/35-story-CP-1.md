# Story 35 — Submit tickets (Story: CP-1)

---

## Prerequisites

- Ticket Management Stories 05–06 completed ([`../ticket-management/05-story-TM-1.md`](../ticket-management/05-story-TM-1.md), [`06-story-TM-2.md`](../ticket-management/06-story-TM-2.md)) — `TicketService.CreateAsync`, `TicketCategory`, `TicketAttachmentService`.
- AI Features Story 32 completed ([`../ai-features/32-story-AI-3.md`](../ai-features/32-story-AI-3.md)) — `TicketCategorizationService.CategorizeOnCreateAsync`, whose call this story makes conditional.

---

## Story Goal

1. `GET /api/customers/by-number/{customerNumber}` — the portal's "log in" lookup (no password).
2. `CreateTicketRequest` gains optional `CustomerId`/`CategoryId` — when a logged-in customer submits with a known id and/or an explicit category choice, `TicketService.CreateAsync` uses them directly instead of resolver/AI guessing.
3. A new `TicketChannel.Portal` value for reporting.
4. Attachments at submission are a two-call sequence (create, then upload) using Ticket Management's existing attachment endpoint — no new endpoint.

**Not in scope:** real authentication.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Tickets/TicketService.cs`, lines 15–42 (`CreateAsync`, post AI Features Story 32's edits) — every line this story touches.
2. `src/SupportCrm.Domain/Repositories/ICustomerRepository.cs`, line 8 (`GetByCustomerNumberAsync`) — already exists, just never had a controller endpoint.
3. `src/SupportCrm.Application/Tickets/TicketDtos.cs`, lines 5–12 (`CreateTicketRequest`) — extended with two new optional fields.
4. `src/SupportCrm.Domain/Entities/TicketChannel.cs` (all 11 lines) — the enum this story adds one member to.

---

## Backend Tasks

### 1 — Domain: `TicketChannel.Portal`

**File: `src/SupportCrm.Domain/Entities/TicketChannel.cs`** — add a member:

```csharp
public enum TicketChannel
{
    Manual,
    Email,
    WhatsApp,
    Chat,
    Sms,
    WebForm,
    Portal
}
```

(No migration needed — `SupportCrmDbContext`'s `Channel` mapping is `HasConversion<string>().HasMaxLength(16)`; `"Portal"` fits and needs no schema change.)

### 2 — Application: `CustomerService` addition, `CreateTicketRequest` extension, `TicketService.CreateAsync` changes

**File: `src/SupportCrm.Application/Customers/CustomerService.cs`** — add:

```csharp
    public async Task<CustomerDto?> GetByCustomerNumberAsync(string customerNumber, CancellationToken ct)
    {
        var customer = await repository.GetByCustomerNumberAsync(customerNumber, ct);
        return customer is null ? null : ToDto(customer);
    }
```

**File: `src/SupportCrm.Application/Tickets/TicketDtos.cs`** — replace `CreateTicketRequest`:

```csharp
public record CreateTicketRequest(
    TicketChannel Channel,
    string Subject,
    string? Description,
    string RequesterName,
    string? RequesterContactValue,
    string CreatedBy,
    string? Language = null,
    Guid? CustomerId = null,
    Guid? CategoryId = null);
```

**File: `src/SupportCrm.Application/Tickets/TicketService.cs`** — replace the start of `CreateAsync` (lines 21–37):

```csharp
        var customerId = request.CustomerId ?? await customerResolver.ResolveCustomerIdAsync(request.RequesterName, request.RequesterContactValue, ct);
        var now = timeProvider.GetUtcNow();
        var referenceNumber = $"TCK-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        var ticket = new Ticket(referenceNumber, customerId, request.Channel, request.Subject.Trim(),
            request.Description?.Trim(), request.RequesterName.Trim(), request.RequesterContactValue?.Trim(), now);
        ticket.SetLanguage(request.Language?.Trim());

        // CP-1: an explicit customer-selected category wins outright over AI-3's guess.
        IReadOnlyList<TicketFieldChangeEntry> aiFieldChanges = Array.Empty<TicketFieldChangeEntry>();
        if (request.CategoryId is not null)
            ticket.SetCategory(request.CategoryId);
        else
            aiFieldChanges = await categorizationService.CategorizeOnCreateAsync(ticket, ct);

        await ticketRepository.AddAsync(ticket, ct);
        await ticketRepository.AddStatusChangeAsync(
            new TicketStatusChangeEntry(ticket.Id, null, TicketStatus.New, request.CreatedBy, "Agent", null, now), ct);
        foreach (var change in aiFieldChanges)
            await ticketRepository.AddFieldChangeAsync(change, ct);
        await ticketRepository.SaveChangesAsync(ct);
```

(The rest of the method — `EvaluateAndAssignAsync` and `return ToDto(ticket);` — is unchanged.)

### 3 — Infrastructure: none

No new tables, no new repository members — everything reused already exists.

### 4 — Api: `CustomersController` addition

**File: `src/SupportCrm.Api/Controllers/CustomersController.cs`** — add:

```csharp

    [HttpGet("by-number/{customerNumber}")]
    public async Task<ActionResult<CustomerDto>> GetByCustomerNumber(string customerNumber, CancellationToken ct)
    {
        var customer = await customerService.GetByCustomerNumberAsync(customerNumber, ct);
        return customer is null ? NotFound() : customer;
    }
```

---

## Edge Cases & Failure Modes

- **`customerNumber` doesn't match any customer** — `404`, letting the frontend show "customer number not found" rather than silently creating a new customer (this endpoint never creates — only `TicketCustomerResolver`'s name/contact path does, unchanged).
- **`CustomerId` provided but doesn't exist** — `Ticket`'s constructor doesn't validate the FK (it never did, for any channel); a bad `CustomerId` would fail at the database FK constraint on `SaveChangesAsync`, same as it always would have — not a new gap this story introduces.
- **Both `CategoryId` and an eventual low-confidence AI suggestion "conflict"** — there's no conflict: `CategoryId is not null` short-circuits the AI call entirely, so `TicketCategorizationSuggestion` isn't even written for that ticket — a customer-selected category means "no AI suggestion was ever made," not "AI agreed."
- **`CategoryId` references an inactive/deactivated `TicketCategory`** — `Ticket.SetCategory` doesn't validate this (matches TM-2's own existing `SetCategoryAsync`, which has the identical gap) — not a new issue.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketServiceTests.cs`** (extend Story 05's tests):
   - `CreateAsync_WithCustomerId_SkipsResolver`
   - `CreateAsync_WithCategoryId_SkipsAiCategorization`
2. **Integration — `tests/SupportCrm.Api.Tests/Controllers/CustomersControllerTests.cs`**:
   - `Get_ByCustomerNumber_UnknownNumber_Returns404`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Regression:** confirm `POST /api/tickets` from every other existing caller (web form, chat, email/SMS/WhatsApp ingestion) still works with `CustomerId`/`CategoryId` omitted — both are optional and default to the pre-existing behavior.

---

## Done Criteria

- [ ] `GET /api/customers/by-number/{customerNumber}` is the portal's login lookup.
- [ ] A known customer + explicit category submit without resolver/AI-categorization overhead.
- [ ] `TicketChannel.Portal` exists and is usable in reporting.
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 36.**
