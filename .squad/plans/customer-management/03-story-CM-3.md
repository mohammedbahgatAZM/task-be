# Story 03 — Interaction history (Story: CM-3)

---

## Prerequisites

- Story 01 completed: [`01-story-CM-1.md`](01-story-CM-1.md) — provides the `Customer` aggregate this timeline is keyed on.
- Not a hard blocker, but designed to interoperate with Story 04 ([`04-story-CM-4.md`](04-story-CM-4.md)) once it exists — see `## Story Goal` below.

---

## Story Goal

Support agents can view a customer's interactions (tickets, calls, chats, emails) as one chronological, filterable timeline.

**No Ticketing/Calls/Chat/Email source module exists anywhere in this codebase yet** (confirmed: only `customer-management` stories exist under `.squad/plans/` and `src/`). Building an aggregator against modules that don't exist would mean either blocking this story indefinitely or hard-coding against a schema that will change. Instead, this story:

1. Defines a small **source-provider seam** (`ICustomerInteractionSource`) — the same pattern as Story 01's `ICustomerActivitySummaryProvider` — that any number of future modules can implement and register via DI.
2. Ships **zero real sources** registered by default. The timeline endpoint returns an empty (but correctly paginated/filterable) result until a source is registered.
3. Story 04 (Notes) is expected to add the **first** real source (`NotesInteractionSource`) when it lands — see that story's plan for the registration. This story's aggregator must not assume Story 04 has run; it must work correctly with an empty source list.

This is an explicit **assumption to flag for review**: the "0 sources today" state means this story is mostly aggregation/pagination/filtering plumbing plus an empty-state UI, not a populated timeline, until Story 04 (or a future Ticketing story) registers a source.

---

## Context — Read These Files First

1. [`01-story-CM-1.md`](01-story-CM-1.md), `## Backend Tasks` → `### 2` — the `ICustomerActivitySummaryProvider` / `StubCustomerActivitySummaryProvider` pair. This story's `ICustomerInteractionSource` follows the exact same seam pattern (interface in `Application/Customers/`, DI registration in `Infrastructure/DependencyInjection.cs`), except this one supports **multiple** registered implementations (`IEnumerable<ICustomerInteractionSource>`) rather than a single provider.
2. `src/SupportCrm.Application/Customers/CustomerService.cs` (62 lines, whole file) — constructor-injection style (`(ICustomerRepository repository, ...)` primary constructor) to follow for the new `CustomerTimelineService`.
3. `src/SupportCrm.Infrastructure/DependencyInjection.cs` (24 lines, whole file) — `AddInfrastructure`'s registration style. Register `ICustomerInteractionSource` implementations with `services.AddScoped<ICustomerInteractionSource, ...>()` (repeatable — each call adds to the resolved `IEnumerable<ICustomerInteractionSource>`, this is standard .NET DI behavior, not a custom mechanism).
4. `src/SupportCrm.Api/Controllers/CustomersController.cs` (51 lines, whole file) — controller pattern to follow for the new timeline endpoint (primary-constructor controller, `[FromQuery]` binding, `try/catch` around domain exceptions).
5. `src/SupportCrm.Domain/Entities/Customer.cs` (38 lines, whole file, as extended by Story 02) — confirms `Customer.Id` (`Guid`) is the join key every interaction source must filter by.

No sibling plan yet builds a multi-source aggregator; this is the first. Keep the aggregator (`CustomerTimelineService`) a thin merge-sort-paginate over whatever `ICustomerInteractionSource.GetInteractionsAsync(...)` returns — do not let it know about tickets/calls/chats/emails/notes specifically.

---

## Backend Tasks

### 1 — Application: interaction DTO, source seam, aggregator service

**Create file: `src/SupportCrm.Application/Customers/CustomerInteractionDto.cs`**

```csharp
namespace SupportCrm.Application.Customers;

public record CustomerInteractionDto(
    Guid Id,
    string Channel,        // free-form discriminator, e.g. "Note", "Ticket", "Call", "Chat", "Email" — sources define their own values
    DateTimeOffset OccurredAtUtc,
    string Summary,
    string? AgentName,
    string? SourceUrl);    // relative link to the original record; null when no UI exists yet for that source
```

**Create file: `src/SupportCrm.Application/Customers/ICustomerInteractionSource.cs`**

```csharp
namespace SupportCrm.Application.Customers;

/// <summary>
/// One channel's contribution to a customer's interaction timeline (Story CM-3). Register an
/// implementation per channel (tickets, calls, chats, emails, notes, ...) via DI — none are
/// registered by default, since no such modules exist yet in this codebase. The aggregator
/// (<see cref="CustomerTimelineService"/>) works correctly with zero registered sources.
/// </summary>
public interface ICustomerInteractionSource
{
    Task<IReadOnlyList<CustomerInteractionDto>> GetInteractionsAsync(
        Guid customerId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, string? agentName, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Customers/CustomerTimelineQuery.cs`**

```csharp
namespace SupportCrm.Application.Customers;

public record CustomerTimelineQuery(
    string? Channel,
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? AgentName,
    int Page = 1,
    int PageSize = 50);

public record CustomerTimelinePageDto(IReadOnlyList<CustomerInteractionDto> Items, int Page, int PageSize, int TotalCount);
```

**Create file: `src/SupportCrm.Application/Customers/CustomerTimelineService.cs`**

```csharp
namespace SupportCrm.Application.Customers;

using SupportCrm.Domain.Repositories;

public class CustomerTimelineService(ICustomerRepository customerRepository, IEnumerable<ICustomerInteractionSource> sources)
{
    public async Task<CustomerTimelinePageDto> GetTimelineAsync(Guid customerId, CustomerTimelineQuery query, CancellationToken ct)
    {
        _ = await customerRepository.GetByIdAsync(customerId, ct) ?? throw new CustomerNotFoundException(customerId);

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 50 : query.PageSize;

        var perSourceResults = await Task.WhenAll(
            sources.Select(s => s.GetInteractionsAsync(customerId, query.FromUtc, query.ToUtc, query.AgentName, ct)));

        var merged = perSourceResults
            .SelectMany(r => r)
            .Where(i => query.Channel is null || string.Equals(i.Channel, query.Channel, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(i => i.OccurredAtUtc)
            .ToList();

        var pageItems = merged.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return new CustomerTimelinePageDto(pageItems, page, pageSize, merged.Count);
    }
}
```

**Design note for the executor:** each `ICustomerInteractionSource` is expected to apply the `fromUtc`/`toUtc`/`agentName` filters itself (so it can push them down to its own storage query, e.g. an indexed EF Core query) — the aggregator only applies the `Channel` filter and pagination centrally, since `Channel` is a cross-source concept the aggregator owns. Document this contract in the `ICustomerInteractionSource` XML doc comment (already drafted above) if further sources are added later.

### 2 — Infrastructure: no registrations by default

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add the service registration for `CustomerTimelineService` inside `AddInfrastructure` (~line 16), but register **no** `ICustomerInteractionSource` implementations here:

```csharp
        services.AddScoped<CustomerTimelineService>();
        // No ICustomerInteractionSource implementations are registered here yet.
        // Story CM-4 registers the first one (NotesInteractionSource) when it lands.
```

Resolving `IEnumerable<ICustomerInteractionSource>` with zero registrations returns an empty enumerable in .NET's built-in DI container — no special-casing needed in `CustomerTimelineService`.

### 3 — Api: timeline endpoint

**File: `src/SupportCrm.Api/Controllers/CustomersController.cs`** — add a new action (after `GetSummary`, ~line 25):

```csharp
    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<CustomerTimelinePageDto>> GetTimeline(
        Guid id,
        [FromServices] CustomerTimelineService timelineService,
        [FromQuery] string? channel,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? agent,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        try
        {
            var query = new CustomerTimelineQuery(channel, from, to, agent, page, pageSize);
            return await timelineService.GetTimelineAsync(id, query, ct);
        }
        catch (CustomerNotFoundException)
        {
            return NotFound();
        }
    }
```

`[FromServices]` on the action parameter (rather than adding `CustomerTimelineService` to the controller's primary constructor) is used here deliberately so `CustomersController`'s constructor doesn't grow with every new cross-cutting service — **flag this as a style choice**; moving it to the primary constructor is equally valid and more consistent with the rest of the file, if preferred on review.

---

## Edge Cases & Failure Modes

- **Zero registered sources** — `CustomerTimelineService.GetTimelineAsync` returns `TotalCount: 0`, `Items: []` — no exception, no special-case branch needed (`sources` resolves to an empty `IEnumerable<>`). This is the expected state until Story 04 or a future module registers a source.
- **Unknown customer id** — `CustomerNotFoundException` → `404`, checked before touching any source.
- **`page`/`pageSize` out of range (≤0, or `pageSize` > 200)** — clamped to `1` / `50` respectively in `GetTimelineAsync`, not rejected with an error — the intake doesn't require strict validation here, and clamping avoids a confusing 400 for an off-by-one UI bug.
- **A registered source throws** — `Task.WhenAll` propagates the first exception, failing the whole timeline request even if other sources would have succeeded. Documented as a known gap: a future story should wrap each source call in its own try/catch and skip failed sources with a logged warning, once there is more than one real source to justify it.
- **500 interactions, 2-second load budget (from the intake)** — enforced structurally by `Skip`/`Take` pagination happening centrally after each source has already applied its own date/agent filters (so no single source needs to materialize more than its own filtered set); actual latency depends on each source's own query efficiency, which this story cannot verify without a real source registered. Flag this as **unverifiable until Story 04 (or a Ticketing story) exists** — do not claim it's met without a real data source to measure against.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Customers/CustomerTimelineServiceTests.cs`**:
   - `GetTimelineAsync_WithNoRegisteredSources_ReturnsEmptyPage`
   - `GetTimelineAsync_UnknownCustomer_ThrowsCustomerNotFoundException`
   - `GetTimelineAsync_MergesAndSortsAcrossMultipleFakeSources_DescendingByOccurredAt` (use two in-test fake `ICustomerInteractionSource` implementations)
   - `GetTimelineAsync_FiltersByChannel`
   - `GetTimelineAsync_ClampsInvalidPageAndPageSize`
2. **Integration — `tests/SupportCrm.Api.Tests/Controllers/CustomersControllerTimelineTests.cs`**:
   - `Get_Timeline_UnknownCustomer_Returns404`
   - `Get_Timeline_WithNoSources_Returns200WithEmptyItems`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Backend tests:** `dotnet test SupportCrm.slnx` (once test projects exist).
3. **Manual smoke:** `GET /api/customers/{id}/timeline` against a customer created via Story 01's endpoint should return `200` with `items: [], totalCount: 0` before Story 04 lands.

---

## Done Criteria

- [ ] The timeline endpoint (`GET /api/customers/{id}/timeline`) returns a chronological, paginated list merged across all registered `ICustomerInteractionSource` implementations.
- [ ] The timeline can be filtered by channel, date range (`from`/`to`), and agent (`agent`) via query parameters.
- [ ] Each entry carries a `SourceUrl` seam for linking to the original record (null until a source populates it).
- [ ] The endpoint works correctly (empty result, no error) with zero sources registered, since none exist yet.
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 04 — Story 04 registers the first real `ICustomerInteractionSource`, so reviewing this seam's shape first avoids rework.**
