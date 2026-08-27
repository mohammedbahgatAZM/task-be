# Story intake

- Folder: `.squad/stories/integrations/INT-2/intake.md`

## Feature

- **Feature name (display):** Integrations
- **Feature slug (folder under `plans/`):** `integrations`

## Tracker

- **Tracker type:** `none` · **Work item id:** `INT-2` · **Work item type:** `Story`

## Title

```
ERP integration
```

## Description

```
Role: System Administrator
As a system administrator, I want to integrate the CRM with our ERP system, so that customer and order data stay in sync.
```

## Acceptance criteria

```
- Customer records can be synced bi-directionally between the CRM and ERP on a defined schedule or trigger.
- Sync conflicts (e.g. edited in both systems) are flagged rather than silently overwritten.
- Failed sync attempts are logged and retried or alerted to an administrator.
- An agent can view relevant ERP data (e.g. order/invoice status) from within a ticket.
```

## Dependencies

- Depends on: Story 57 (INT-4)'s connector framework — `IntegrationConnector`, `IIntegrationConnectorRepository`, `IExternalSystemConnector`, `ExternalDataService` all had to exist before this story's `MockErpConnector`/`ErpSyncService` could plug into them. Built in tracker-ID order in this document set, but implemented with Story 57's foundation in place first — same kind of forward-dependency note Communication Channels' Story 15 plan already uses.

## Extra notes

- No real ERP account exists anywhere in this codebase — same documented decision as the Communication Channels mock senders. `MockErpConnector` simulates order status/last order date/invoice balance deterministically from the customer id (same customer always shows the same values), plus a simulated "ERP account name" used specifically for the sync/conflict logic below.
- **Bi-directional sync is scoped to `Customer.Company` only** (a new `SetCompany` setter + `PUT /api/customers/{id}/company` endpoint, since no such edit path existed before this story). A real ERP integration would sync a much wider field set; this prototype demonstrates the mechanism on one field rather than building it out for fields that don't otherwise matter to this app.
- **Conflict detection, precisely:** `ErpSyncState` tracks, per customer, what the ERP-simulated value and the local `Company` each looked like as of the last sync. On each sync: if only the remote value changed since last sync, it's applied to `Customer.Company`. If only the local value changed, it's accepted as the new baseline (nothing to apply). If **both** changed, neither is applied — a `Conflict`-status `ErpSyncLog` row is written and every supervisor-flagged agent is alerted (`AgentNotificationService`), and the conflicting state is left as-is so it doesn't silently self-resolve on the next tick.
- **A real bug was caught and fixed during implementation:** the first version of `ErpSyncService` constructed a *new* `ErpSyncState` instance on every "no material change" sync instead of mutating the one already tracked by the request's `DbContext` (fetched moments earlier via `GetStateAsync`) — EF Core throws `ArgumentOutOfRangeException: Unexpected entry.EntityState: Detached` the moment a second instance sharing that primary key is added while the first is still tracked. Confirmed live: the *second* sync trigger for an already-baselined connector 500'd every time. Fixed by mutating the tracked `state` via its own `Update(...)` method instead of constructing a replacement; re-verified with three consecutive sync triggers, all `204`.
- **A second, subtler bug was caught before it shipped:** the very first draft of the mock ERP's simulated company name was derived from `Customer.Company` itself — the same field the sync writes to. That meant editing `Company` locally always looked like the remote had *also* just changed (since "what the ERP thinks" was defined as "whatever `Company` currently is"), so a plain one-sided local edit always misreported as a `Conflict`. Fixed by deriving the simulated remote value from `Customer.Name` (immutable after creation) plus a day-of-year term instead — decoupled from the field being synced, and drifts roughly once every 5 days per customer rather than being frozen forever or changing on every call. Verified live: creating a customer, syncing (baseline), editing `Company`, and syncing again now correctly logs "CRM-side change accepted as the new baseline," not a false conflict.
- "On a defined schedule or trigger": `ErpSyncHostedService` (a `PeriodicTimer`, 15 minutes — the same minimal-scheduler pattern `SlaEscalationHostedService` already established) is the schedule; `POST /api/admin/connectors/erp/sync` is the manual trigger.
- "An agent can view relevant ERP data ... from within a ticket": `GET /api/customers/{customerId}/external-data` (INT-4's `ExternalDataService`) — deliberately unauthenticated, matching `TicketsController`/`CustomersController`'s own "no real auth" convention, since it's read by the same ticket/customer pages those live on.

## Technical hints

- `src/SupportCrm.Domain/Entities/ErpSyncState.cs`, `ErpSyncLog.cs`, `ErpSyncStatus.cs` — new.
- `src/SupportCrm.Application/Integrations/ErpSyncService.cs`, `MockErpConnector.cs`, `ErpSyncHostedService.cs` — new.
- `src/SupportCrm.Domain/Entities/Customer.cs` — new `SetCompany` setter.

## Out of scope

- Syncing any field other than `Company`.
- A real ERP connection of any kind.
- Automatic conflict resolution — conflicts are surfaced for manual resolution, never auto-merged.
