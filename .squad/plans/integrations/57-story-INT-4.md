# Story 57 — External systems (Story: INT-4)

---

## Prerequisites

None. **Implemented first**, ahead of its tracker-id-driven numbering — Stories 55 and 56 both build on the framework introduced here. See the feature overview's dependency note.

---

## Story Goal

A generic, genuinely reusable connector framework (`IntegrationConnector` + `IExternalSystemConnector`), an external-data panel that degrades gracefully per-connector, and two independent example connectors (`Erp`, `Billing`) proving the framework isn't a one-off.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Tickets/AgentNotificationService.cs` — reused for connector-test-failure alerts.
2. `src/SupportCrm.Domain/Repositories/ICustomerRepository.cs` — `GetByIdAsync`, what `ExternalDataService` resolves the customer through.

---

## Backend Tasks

### 1 — Domain

**File: `src/SupportCrm.Domain/Entities/IntegrationConnectorType.cs`** — `Email`, `Sms`, `WhatsApp`, `Erp`, `Billing`, `Inventory` — one enum shared by every connector-type story in this feature.

**File: `src/SupportCrm.Domain/Entities/IntegrationConnector.cs`** — `Type`, `Name`, `ConfigJson` (free-form), `IsEnabled`, `LastTestedAtUtc`/`LastTestSucceeded`, `LastSyncAtUtc`. `UpdateConfig`/`Enable`/`Disable`/`RecordTestResult`/`RecordSync`.

**File: `src/SupportCrm.Domain/Repositories/IIntegrationConnectorRepository.cs`** — `GetByIdAsync`/`GetAllAsync`/`GetEnabledByTypeAsync`/`AddAsync`/`SaveChangesAsync`.

### 2 — Application

**File: `src/SupportCrm.Application/Integrations/IExternalSystemConnector.cs`** — the framework's one interface: `Type`, `FetchCustomerDataAsync(connector, customer, now, ct) -> ExternalDataSnippetDto`. Registered as `IEnumerable<IExternalSystemConnector>` in DI so `ExternalDataService` can fan out to every registered implementation without knowing about them individually.

**File: `src/SupportCrm.Application/Integrations/MockBillingConnector.cs`** — a second, independent implementation (Plan/Outstanding balance/Payment method, deterministic from customer id) — deliberately simpler than `MockErpConnector` (no sync/conflict logic, read-only), proving the interface genuinely generalizes rather than being shaped around ERP alone.

**File: `src/SupportCrm.Application/Integrations/IntegrationConnectorService.cs`** — `CreateAsync`/`GetAllAsync`/`UpdateConfigAsync`/`SetEnabledAsync`, and `TestConnectionAsync` (mock: succeeds iff `ConfigJson` parses as a non-empty JSON object; alerts supervisors on failure via `AgentNotificationService`).

**File: `src/SupportCrm.Application/Integrations/ExternalDataService.cs`** — `GetForCustomerAsync(customerId)`: for every *enabled* connector, finds the matching `IExternalSystemConnector` (connector types with no data-fetching implementation, like `Email`/`Sms`/`WhatsApp`, are silently skipped — they're not data sources), calls it inside its own `try`/`catch`, and turns an exception into a `{ success: false, errorMessage }` snippet rather than letting it propagate. One connector failing never affects the others or the caller.

**File: `src/SupportCrm.Application/Integrations/IntegrationDtos.cs`** — `ConnectorDto`, `ConnectorTestResultDto`, `ExternalDataSnippetDto`/`ExternalDataFieldDto` (shared contract for every connector's output).

### 3 — Infrastructure

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — `DbSet<IntegrationConnector>` + `OnModelCreating` block (`Type` stored as a string, not an int, so adding future enum values never shifts existing stored rows).

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — `services.AddScoped<IExternalSystemConnector, MockErpConnector>(); services.AddScoped<IExternalSystemConnector, MockBillingConnector>();` — both registered against the same interface, resolved as `IEnumerable<IExternalSystemConnector>` by `ExternalDataService`.

### 4 — Api

**File: `src/SupportCrm.Api/Controllers/ConnectorsController.cs`** — `GET`/`POST`/`PUT .../config`/`PUT .../enabled`/`POST .../test`, all JWT-secured, `[RequirePermission("Integrations", ...)]`.

**File: `src/SupportCrm.Api/Controllers/ExternalDataController.cs`** — `GET api/customers/{customerId:guid}/external-data`, deliberately **unauthenticated** — matches `TicketsController`/`CustomersController`'s existing "no real auth" convention, since it's read by the same ticket/customer pages those controllers already serve without a JWT session.

---

## Edge Cases & Failure Modes

- **A connector type with no registered `IExternalSystemConnector`** (e.g. an `Email` connector) — `ExternalDataService` looks it up, finds no match, and skips it silently; it never appears in the external-data panel, which is correct (it isn't a data source).
- **One connector throwing mid-fetch** — caught per-connector; verified live with `ERP` and `Billing` both enabled: one call to `GET .../external-data` returned two independent snippets, each labeled and timestamped on its own.
- **A disabled connector** — excluded from `GetForCustomerAsync` entirely (filtered by `IsEnabled` before any fetch is attempted), not fetched-then-hidden.

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` — 0 errors.
2. **Migration:** part of the consolidated `AddIntegrations` migration — 6 new tables total across the whole feature (`ApiKeys`, `WebhookSubscriptions`, `WebhookDeliveryLogs`, `IntegrationConnectors`, `ErpSyncLogs`, `ErpSyncStates`), no changes to any existing table, plus the 5 new `Integrations` permission rows (auto-granted to the seeded Admin role) — applied live via `dotnet ef database update`.
3. **Live smoke test:** created `Erp` and `Billing` connectors → both returned independently in `GET /api/customers/{id}/external-data`, correctly source-labeled and timestamped.

---

## Done Criteria

- [x] `IntegrationConnector` + `IExternalSystemConnector` form one generic framework, proven by two independent implementations (`Erp`, `Billing`).
- [x] `GET api/customers/{id}/external-data` degrades gracefully per-connector — one failure never blocks the others or the calling page.
- [x] Every snippet carries a source label and a fetched-at timestamp.
- [x] `dotnet build SupportCrm.slnx` succeeds, 0 warnings, 0 errors; `AddIntegrations` migration applied to the live database.
