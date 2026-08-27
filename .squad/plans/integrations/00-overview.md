# integrations — plan overview

Entry point for the **integrations** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 54 | [54-story-INT-1.md](54-story-INT-1.md) | APIs | INT-1 | — |
| 55 | [55-story-INT-2.md](55-story-INT-2.md) | ERP integration | INT-2 | Story 57 (connector framework, built first despite the numbering) |
| 56 | [56-story-INT-3.md](56-story-INT-3.md) | Email, SMS & WhatsApp provider integration | INT-3 | Story 57 (connector framework); Communication Channels Story 10 (mock senders) |
| 57 | [57-story-INT-4.md](57-story-INT-4.md) | External systems | INT-4 | — (foundational — see note below) |

## Dependency notes

- **Story 57 (INT-4) is the technical foundation for Stories 55 and 56**, despite being numbered last by tracker id — `IntegrationConnector`, `IIntegrationConnectorRepository`, `IntegrationConnectorService`, and `IExternalSystemConnector` all originate there. Implemented in that order (57's framework first, then 55/56 on top of it); documented here and in each story's own intake notes, the same shape as Communication Channels' Story 15 plan documenting its own out-of-order dependency correction.
- New bounded concern `SupportCrm.Application.Integrations` — one file per responsibility (`ApiKeyService`, `WebhookService`, `IntegrationConnectorService`, `ErpSyncService`, `ExternalDataService`, `ExternalApiService`), `IntegrationDtos.cs`/`ExternalApiDtos.cs` shared across it, the same per-feature convention as `SupportCrm.Application.Platform`.
- Story 54 (INT-1) is independent of the other three — its `ApiKey`/`WebhookSubscription` entities and `ApiKeyAuthenticationHandler` scheme aren't touched by 55/56/57 at all, beyond `TicketService` calling `WebhookService.DispatchAsync` at its two existing lifecycle points (ticket created, ticket resolved).
- Two hosted services now run alongside the existing `SlaEscalationHostedService`: `ErpSyncHostedService` (Story 55, 15-minute interval) — the same minimal `PeriodicTimer` scheduler pattern, not a new mechanism.
- Every failure/alert path across all four stories (webhook delivery failure, connector test failure, ERP sync failure/conflict) reuses the existing `AgentNotificationService.NotifyAsync`, notifying every supervisor-flagged agent — no new notification mechanism was introduced.
- One consolidated migration (`AddIntegrations`) covers every schema change across all four stories: six new tables (`ApiKeys`, `WebhookSubscriptions`, `WebhookDeliveryLogs`, `IntegrationConnectors`, `ErpSyncLogs`, `ErpSyncStates`) and one new seeded permission module (`Integrations`, granting `View`/`Create`/`Edit`/`Delete`/`Export` to the seeded Admin role) — no changes to any existing table's columns.
- **Two real bugs were caught and fixed during implementation** (see Story 55's intake notes for the full detail): an EF Core "duplicate tracked entity" crash on the second ERP sync of an already-baselined customer, and a conflict-detection logic bug where the mock ERP's simulated value was entangled with the very field it was being compared against, making every local edit misreport as a conflict. Both fixed and re-verified live before this feature was considered done.
- **Explicit scope boundaries carried through this whole feature:** rate limiting is per-server, not per-key (Story 54); webhook redelivery is a manual admin action, not automatic (Story 54); ERP sync only bi-directionally syncs `Customer.Company` (Story 55); every provider "connection test" is a mock shape-validation, not a real call to any provider (Story 56); no shipped `Inventory` connector exists yet, only the connector type (Story 57).
