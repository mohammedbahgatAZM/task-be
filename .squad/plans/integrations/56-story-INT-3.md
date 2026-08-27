# Story 56 — Email, SMS & WhatsApp provider integration (Story: INT-3)

---

## Prerequisites

Story 57 (INT-4)'s connector framework must exist first (`IntegrationConnector`, `IntegrationConnectorService`). Communication Channels Story 10's `MockEmailSender`/`MockWhatsAppSender`/`MockSmsSender` are the senders this story adds configuration on top of, unmodified.

---

## Story Goal

Let an admin configure per-channel credentials/settings for Email/SMS/WhatsApp without a code change, run a connection test, and get alerted on failure — all through the generic connector framework, no new mechanism.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Integrations/IntegrationConnectorService.cs` (Story 57) — `TestConnectionAsync` is entirely generic; this story adds zero new code to it.
2. `backend/.squad/plans/communication-channels/00-overview.md` — the "no real email/WhatsApp/SMS provider account exists" scope decision this story does not revisit.

---

## Backend Tasks

### 1 — Domain

**File: `src/SupportCrm.Domain/Entities/IntegrationConnectorType.cs`** (Story 57) — `Email`, `Sms`, `WhatsApp` values, defined in the same enum as `Erp`/`Billing`/`Inventory` rather than a separate type, since the framework treats every connector type identically at the domain layer.

### 2 — Application

No new application-layer code for this story specifically — `IntegrationConnectorService.CreateAsync`/`UpdateConfigAsync`/`SetEnabledAsync`/`TestConnectionAsync` (Story 57) already handle any `IntegrationConnectorType` value, `Email`/`Sms`/`WhatsApp` included. This story's actual work is admin-facing config for those three types plus the frontend page listing them (see the frontend plan).

### 3 — Api

No new endpoints — `POST /api/admin/connectors` (with `type: "Email" | "Sms" | "WhatsApp"`), `POST .../test`, `PUT .../config`, `PUT .../enabled` (all Story 57) are reused verbatim.

---

## Edge Cases & Failure Modes

- **An empty or malformed `ConfigJson`** — `TestConnectionAsync` fails the test (`"Configuration is empty..."` / `"...not valid JSON."`) rather than throwing, and alerts every supervisor-flagged agent via `AgentNotificationService`. Verified live: a `WhatsApp` connector created with `{}` failed its test and the alert appeared in the flagged supervisor's own notification feed.
- **This is a mock validation, not a live provider call** — a connector can "pass" its test with syntactically valid but functionally wrong credentials, since nothing here ever contacts a real WhatsApp/SMS/email API. Documented explicitly in `docs/API.md` and this story's own intake notes so it's never mistaken for more than it is.

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` — 0 errors (no code changes specific to this story beyond the shared `IntegrationConnectorType` enum values already covered by Story 57's build).
2. **Live smoke test:** created a `WhatsApp` connector with an empty config → connection test correctly failed → confirmed the resulting alert notification on a supervisor-flagged agent.

---

## Done Criteria

- [x] `Email`/`Sms`/`WhatsApp` connectors can be created, configured, tested, enabled/disabled through the shared connector framework.
- [x] A failed test alerts supervisors.
- [x] Message dedup ("neither duplicated nor lost") is satisfied by Communication Channels' existing `TicketIngestionService` — confirmed unmodified by this story.
- [x] `dotnet build SupportCrm.slnx` succeeds, 0 warnings, 0 errors.
