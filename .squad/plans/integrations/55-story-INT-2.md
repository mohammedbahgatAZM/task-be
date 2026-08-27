# Story 55 — ERP integration (Story: INT-2)

---

## Prerequisites

Story 57 (INT-4)'s connector framework — `IntegrationConnector`, `IIntegrationConnectorRepository`, `IntegrationConnectorService`, `IExternalSystemConnector` — must exist first. Implemented in that order despite the tracker-id numbering; see the feature overview's dependency note.

---

## Story Goal

Bi-directional sync of `Customer.Company` against a simulated ERP value, on a schedule and on demand, with genuine conflict detection, failure logging, and an agent-facing external-data panel showing simulated order/invoice data.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Tickets/SlaEscalationHostedService.cs` — the exact `PeriodicTimer`/`IServiceScopeFactory` shape `ErpSyncHostedService` copies.
2. `src/SupportCrm.Application/Integrations/IntegrationConnectorService.cs` (Story 57) — `IntegrationConnector`/`IIntegrationConnectorRepository`, this story's `Erp`-type connectors.
3. `src/SupportCrm.Domain/Entities/Customer.cs` — no setter existed for `Company` before this story.

---

## Backend Tasks

### 1 — Domain

**File: `src/SupportCrm.Domain/Entities/Customer.cs`** — add:

```csharp
    public void SetCompany(string? company) => Company = company;
```

**Files: `src/SupportCrm.Domain/Entities/ErpSyncState.cs`, `ErpSyncLog.cs`, `ErpSyncStatus.cs`** — new. `ErpSyncState` (keyed by `CustomerId`) tracks `LastSyncedRemoteCompany`/`LastSyncedLocalCompany`/`LastSyncedAtUtc` — the bookkeeping the conflict check compares against. `ErpSyncLog` is an append-only audit row per sync attempt (`Synced`/`Conflict`/`Failed`).

**File: `src/SupportCrm.Domain/Repositories/IErpSyncRepository.cs`** — `GetStateAsync`, `UpsertStateAsync`, `AddLogAsync`, `GetLogsAsync`.

### 2 — Application

**File: `src/SupportCrm.Application/Integrations/MockErpConnector.cs`** — implements `IExternalSystemConnector` for `FetchCustomerDataAsync` (order status/last order date/invoice balance/ERP account name, all deterministic from the customer id) and exposes `static SimulateRemoteCompanyName(customer, now)`, shared with `ErpSyncService`. **Deliberately derives the simulated value from `Customer.Name` (immutable) plus a day-of-year term — not from `Customer.Company`** (the field being synced): see Edge Cases below for why that distinction mattered.

**File: `src/SupportCrm.Application/Integrations/ErpSyncService.cs`** — `SyncAllAsync` (every enabled `Erp` connector × every customer), `SyncCustomerAsync` (the actual compare-and-apply-or-conflict logic):
  - No prior `ErpSyncState` → establish baseline, log `Synced`, apply nothing.
  - Remote changed, local unchanged → apply remote to `Customer.Company`, log `Synced`.
  - Local changed, remote unchanged → accept local as the new baseline (nothing to apply), log `Synced`.
  - **Both changed → log `Conflict`, apply nothing, alert every supervisor-flagged agent.** The existing `ErpSyncState` row is left untouched so the same conflict doesn't silently vanish on the next tick.
  - Any exception → log `Failed`, alert supervisors, still return normally (one customer's failure doesn't stop the rest of `SyncAllAsync`'s loop).

**File: `src/SupportCrm.Application/Integrations/ErpSyncHostedService.cs`** — `PeriodicTimer`, 15-minute interval, calls `ErpSyncService.SyncAllAsync` per tick.

**File: `src/SupportCrm.Application/Customers/CustomerService.cs`** — `SetCompanyAsync`, same `GetByIdAsync ?? throw CustomerNotFoundException` shape as every other setter here.

**File: `src/SupportCrm.Application/Customers/CustomerDtos.cs`** — `SetCustomerCompanyRequest(string? Company)`.

### 3 — Infrastructure

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — `DbSet<ErpSyncLog>`, `DbSet<ErpSyncState>` + `OnModelCreating` blocks.

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — `IErpSyncRepository`/`ErpSyncRepository`, `ErpSyncService`, `AddHostedService<ErpSyncHostedService>()`.

### 4 — Api

**File: `src/SupportCrm.Api/Controllers/CustomersController.cs`** — `PUT {id:guid}/company`.

**File: `src/SupportCrm.Api/Controllers/ConnectorsController.cs`** (Story 57) — this story's two endpoints live on it: `POST erp/sync` (manual trigger), `GET erp/sync-logs?customerId=`.

---

## Edge Cases & Failure Modes

- **EF Core "duplicate tracked entity" crash on re-sync (caught and fixed live).** The first implementation constructed a *new* `ErpSyncState` on every "no material change" branch instead of mutating the instance `GetStateAsync` had already attached to the request's `DbContext`. `UpsertStateAsync`'s `dbContext.Entry(state).State == EntityState.Detached` check was true for that *new* object, so it called `.Add(...)` — which EF Core rejects with `ArgumentOutOfRangeException: Unexpected entry.EntityState: Detached` because a tracked entity with the same primary key already exists. Reproduced live: the second `POST .../erp/sync` for an already-baselined connector 500'd every single time. **Fix:** call `state.Update(remoteCompany, localCompany, now)` on the already-tracked instance instead of constructing a replacement. Re-verified: three consecutive sync triggers all returned `204`.
- **False-positive conflicts on every local edit (caught and fixed before shipping).** The first version of `SimulateRemoteCompanyName` used `Customer.Company` as its base name — the same field the sync applies changes to. That meant "what the ERP thinks" was defined in terms of the live local value, so any local edit made the simulated remote value drift too, and the both-sides-changed check fired on *every* one-sided local edit. **Fix:** base the simulated value on `Customer.Name` (never edited) instead, decoupling it entirely from the field under sync. Re-verified live: create customer → sync (baseline) → edit `Company` → sync again → correctly logged "CRM-side change accepted as the new baseline," not `Conflict`.
- **A conflict occurring for real** requires both sides to change between two syncs — reachable today only via the day-of-year drift term landing on a "changed" day for that customer while a local edit also happens in the same window; not independently forced/seeded, by design (no non-deterministic test hook was added for it).

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` — 0 errors.
2. **Migration:** part of the consolidated `AddIntegrations` migration (see Story 57).
3. **Live smoke test:** created an ERP connector → connection test succeeded → triggered sync (11 customers, all `Synced: "Initial sync baseline established."`) → triggered sync twice more (both `204`, no crash — confirms the EF fix) → created a fresh customer → synced (baseline) → edited its `Company` via the new endpoint → synced again → confirmed `"CRM-side change accepted as the new baseline."` (confirms the conflict-logic fix) → `GET /api/customers/{id}/external-data` returned the simulated order/invoice fields, source-labeled `"ERP"`, timestamped.

---

## Done Criteria

- [x] `Customer.Company` bi-directionally syncs against `MockErpConnector`'s simulated value.
- [x] Both-sides-changed is flagged as `Conflict`, never silently overwritten.
- [x] Failed syncs are logged and alert supervisors.
- [x] `ErpSyncHostedService` runs the sync every 15 minutes; `POST .../erp/sync` triggers it on demand.
- [x] Both real bugs (EF tracking crash, false-conflict logic) fixed and re-verified live, not just fixed and assumed correct.
- [x] `dotnet build SupportCrm.slnx` succeeds, 0 warnings, 0 errors.
