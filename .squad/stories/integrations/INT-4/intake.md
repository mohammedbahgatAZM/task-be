# Story intake

- Folder: `.squad/stories/integrations/INT-4/intake.md`

## Feature

- **Feature name (display):** Integrations
- **Feature slug (folder under `plans/`):** `integrations`

## Tracker

- **Tracker type:** `none` · **Work item id:** `INT-4` · **Work item type:** `Story`

## Title

```
External systems
```

## Description

```
Role: System Administrator
As a system administrator, I want to integrate with other external systems such as billing or inventory, so that agents have complete context without switching tools.
```

## Acceptance criteria

```
- Relevant external data can be displayed within the ticket or customer profile via integration.
- New integrations can be added through a configurable connector framework where possible.
- Integration failures degrade gracefully (ticket still usable) rather than blocking agent work.
- Data fetched from external systems is clearly labeled with its source and refresh time.
```

## Dependencies

- Depends on: none new. **This story is actually the foundation Stories 55 (INT-2) and 56 (INT-3) build on**, despite being numbered last by tracker id — `IntegrationConnector`, `IIntegrationConnectorRepository`, `IntegrationConnectorService`, and the `IExternalSystemConnector` interface all originate here. Documented as a forward-dependency in both of those stories' own intake notes, the same pattern Communication Channels' Story 15 plan already used for a similar out-of-numeric-order dependency.

## Extra notes

- **"A configurable connector framework where possible":** `IntegrationConnector` (`Type`, `Name`, `ConfigJson`, `IsEnabled`) is the one generic entity every connector type (`Erp`, `Billing`, `Inventory`, plus Story 56's `Email`/`Sms`/`WhatsApp`) shares — same CRUD, same connection test, same enable/disable. Adding a new data-providing type means one new class implementing `IExternalSystemConnector` (`FetchCustomerDataAsync`) registered in DI — `MockErpConnector` (Story 55) and `MockBillingConnector` (this story) are two independent proofs the interface genuinely generalizes, not just a one-off built for ERP.
- **"Relevant external data ... within the ticket or customer profile"** and **"clearly labeled with its source and refresh time"**: `GET /api/customers/{customerId}/external-data` returns one `ExternalDataSnippetDto` per enabled data-providing connector — `sourceName`, `fetchedAtUtc`, and a `fields` list, always present regardless of outcome.
- **"Integration failures degrade gracefully"**: `ExternalDataService.GetForCustomerAsync` calls each enabled connector's `FetchCustomerDataAsync` inside its own `try`/`catch` — one connector throwing produces a `{ success: false, errorMessage: ... }` snippet for *that* connector only, the others still return their data, and the customer/ticket page itself never fails because of it. Verified live: with `ERP` and `Billing` connectors both enabled, `GET .../external-data` returned two independent, source-labeled, timestamped snippets in one call.
- `MockBillingConnector` (Plan, Outstanding balance, Payment method — deterministic from customer id, same "no real provider" pattern as everything else in this codebase) is the "billing or inventory" example the AC names; `Inventory` exists as a connector *type* in the enum but has no shipped connector implementation — a real `IExternalSystemConnector` for it is a one-file addition following the exact same shape, left as the next natural extension rather than built speculatively with nothing yet to show for a made-up inventory system.

## Technical hints

- `src/SupportCrm.Domain/Entities/IntegrationConnector.cs`, `IntegrationConnectorType.cs` — new.
- `src/SupportCrm.Application/Integrations/IExternalSystemConnector.cs`, `MockBillingConnector.cs`, `ExternalDataService.cs`, `IntegrationConnectorService.cs` — new.
- `src/SupportCrm.Api/Controllers/ExternalDataController.cs`, `ConnectorsController.cs` — new.

## Out of scope

- A shipped `Inventory` connector (type exists, implementation doesn't yet).
- Any UI-side caching of external data beyond what the single GET call already returns.
