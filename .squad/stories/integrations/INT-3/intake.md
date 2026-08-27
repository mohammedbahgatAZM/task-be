# Story intake

- Folder: `.squad/stories/integrations/INT-3/intake.md`

## Feature

- **Feature name (display):** Integrations
- **Feature slug (folder under `plans/`):** `integrations`

## Tracker

- **Tracker type:** `none` · **Work item id:** `INT-3` · **Work item type:** `Story`

## Title

```
Email, SMS & WhatsApp provider integration
```

## Description

```
Role: System Administrator
As a system administrator, I want to connect our email, SMS, and WhatsApp providers, so that messages from these channels are ingested automatically.
```

## Acceptance criteria

```
- Provider credentials/API keys can be configured per channel without code changes.
- A connection test confirms each integration is working before going live.
- Provider outages or authentication failures trigger an alert to the admin.
- Messages are neither duplicated nor lost during normal provider retries.
```

## Dependencies

- Depends on: Story 57 (INT-4)'s connector framework (`IntegrationConnector`, `IntegrationConnectorService`) — this story only adds the `Email`/`Sms`/`WhatsApp` connector *types* and their admin UI, reusing the framework's CRUD/test-connection endpoints as-is, same forward-dependency shape as Story 55 (INT-2).

## Extra notes

- **This is a configuration layer on top of Communication Channels' already-shipped mock senders (`MockEmailSender`, `MockWhatsAppSender`, `MockSmsSender`), not new ingestion logic.** Those senders still don't call a real provider — same documented decision as ever — but an admin can now record credentials/settings per channel (`IntegrationConnector.ConfigJson`, free-form JSON, "without code changes" per the AC) and run a connection test against them.
- **"A connection test confirms each integration is working"** — mock, deliberately: `IntegrationConnectorService.TestConnectionAsync` validates that `ConfigJson` parses as a non-empty JSON object. There is no real provider to actually call. This is documented explicitly in `docs/API.md` and the story notes here, not implied to be more than it is.
- **"Provider outages or authentication failures trigger an alert to the admin"** — a failed test result calls `AgentNotificationService.NotifyAsync` for every supervisor-flagged agent, the same mechanism Story 54 (INT-1)'s failed webhook deliveries and Story 55 (INT-2)'s failed/conflicted ERP syncs both use. Verified live: a `WhatsApp` connector created with `{}` as its config failed its test (`"Configuration is empty..."`) and the notification appeared in the flagged supervisor's own `GET /api/agents/{id}/notifications` feed.
- **"Messages are neither duplicated nor lost during normal provider retries"** — already satisfied by Communication Channels' `TicketIngestionService`, its shared channel-agnostic dedup-to-open-ticket path (documented in that feature's own overview). Not rebuilt here; this story adds nothing new to that path.

## Technical hints

- `src/SupportCrm.Domain/Entities/IntegrationConnectorType.cs` — `Email`/`Sms`/`WhatsApp` values (alongside `Erp`/`Billing`/`Inventory`, all defined together in Story 57 since they're one enum).
- `src/SupportCrm.Application/Integrations/IntegrationConnectorService.cs` — `TestConnectionAsync`, shared by every connector type, no per-type branching needed for this story.

## Out of scope

- Actually calling a real email/SMS/WhatsApp provider — sending still goes through the existing mocks.
- Per-channel rate limits or provider-specific retry policies.
