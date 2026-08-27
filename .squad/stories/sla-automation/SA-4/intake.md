# Story intake

- Folder: `.squad/stories/sla-automation/SA-4/intake.md`

---

## Feature

- **Feature name (display):** SLA & Automation
- **Feature slug (folder under `plans/`):** `sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `SA-4`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Alerts and notifications
```

---

## Description

```
Role: Support Agent
As a support agent or manager, I want to receive alerts when a ticket is approaching or has breached its SLA, so that I can act before the customer is affected.
```

---

## Acceptance criteria

```
- In-app, email, and/or push notifications can be configured for SLA warnings and breaches.
- Alerts identify the specific ticket, its remaining time, and a direct link to it.
- Notification channels and frequency are configurable per user or role.
- A daily/weekly digest of at-risk tickets is available to managers.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** SA-1 (time-to-breach source), SA-3 (escalation events, and its deferred "notify a supervisor" action which should route through this story's seam).
- **Depends on code areas or other stories:** backend SA-1, SA-3; reuses the notifier-seam pattern already established by TM-3's `IAssignmentNotifier` and TM-4's `ICustomerStatusNotifier`.

## Extra notes (optional)

- No email/push notification channel exists anywhere in this codebase yet — same documented gap as TM-3's `IAssignmentNotifier` and TM-4's `ICustomerStatusNotifier` (both ship only no-op/stub implementations; TM-4 also has a real `SmsCustomerStatusNotifier`/`MockSmsSender` pair worth checking as a template for "real-ish but mocked" delivery). Define an `ISlaAlertNotifier` (or similar) seam here with a no-op/logging stub, following the exact same pattern — flag explicitly as a stand-in for real email/push infrastructure.
- "In-app" notifications are the one channel this story *can* deliver for real (no external dependency) — implement in-app alerts as real, persisted, queryable records (so a UI can list/mark-read them); treat email/push as configuration + the stub seam only, per the note above.
- "Configurable per user or role" — no user/role/identity system exists yet (same gap noted in TM-1/TM-3/TM-4). Reuse `Agent` as the "user" and add a minimal preference record (channel + frequency per `Agent`, or per a coarse role flag like `Agent.CanViewSensitiveData`-style boolean) rather than building real RBAC.
- "Alerts identify the ticket, remaining time, and a direct link" — "link" means a deep-link route/URL pattern the frontend resolves (e.g. `/tickets/{id}`), not an actual hosted URL this backend story needs to serve.
- "Daily/weekly digest" reuses the same "no scheduler infrastructure" gap flagged in SA-3 — reuse whatever minimal recurring-check mechanism SA-3 introduces rather than adding a second one.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Continue the `Sla` bounded concern from SA-1/SA-3: `src/SupportCrm.Domain/Entities/SlaAlert.cs`, `AlertPreference.cs`, `src/SupportCrm.Application/Sla/SlaAlertService.cs`, `ISlaAlertNotifier.cs` + `NoOpSlaAlertNotifier.cs` (mirroring `NoOpAssignmentNotifier.cs` / `NoOpCustomerStatusNotifier.cs`), controller under `src/SupportCrm.Api/Controllers/`.
- `TimeProvider` (already used in `TicketAssignmentService`) should drive the recurring digest/threshold checks for testability.

## Out of scope

- Real email/push delivery infrastructure — only the seam + stub, same as every other notifier in this codebase.
- Full RBAC/user-preference management — only a minimal per-agent alert-preference record.
- SLA calculation and escalation-trigger logic themselves (SA-1, SA-3) — this story only delivers alerts about them.
