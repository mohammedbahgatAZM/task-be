# Story intake

- Folder: `.squad/stories/security-administration/SEC-4/intake.md`

---

## Feature

- **Feature name (display):** Security & Administration
- **Feature slug (folder under `plans/`):** `security-administration`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `SEC-4`
- **Work item type:** `Story`

---

## Title

```
System configuration
```

---

## Description

```
Role: System Administrator
As a system administrator, I want to configure system-wide settings, so that the CRM can be tailored to our organization's needs.
```

---

## Acceptance criteria

```
- Settings such as business hours, holidays, languages, and notification defaults can be configured centrally.
- Configuration changes are logged in the audit trail.
- Invalid configuration values are validated and rejected with guidance.
- Configuration changes can be previewed or tested before being applied organization-wide.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Story 45 (SEC-1) — auth pipeline (this module's own endpoints are admin-only).
- **Depends on code areas or other stories:** SLA & Automation's `BusinessCalendarConfigService`/`BusinessHours`/`Holiday` (Story 22) — **reused entirely unmodified**, not rebuilt.

## Extra notes (optional)

- **Business hours and holidays already have full, working CRUD** (SLA & Automation Story 22) — this story does not duplicate that. It adds the two setting categories the AC names that don't exist yet — languages and notification defaults — as a small, generic `SystemSetting` key/value store, and gives the frontend one consolidated "System Configuration" page that surfaces *both* the pre-existing business-hours/holiday sections *and* this story's new settings side by side. "Configured centrally" is a **frontend information-architecture decision** (one page, several sections calling their respective existing/new endpoints), not a backend data-model merge — merging business hours into the new key/value store would be a regression from a purposely-typed, validated existing entity to a stringly-typed one for no benefit.
- New settings are a small, fixed, code-defined catalog (`SystemSettingCatalog`) — not a fully generic admin-configurable schema. Three entries for this story: `SupportedLanguages` (JSON array of 2-letter codes), `NotifyCustomerOnStatusChangeByDefault` (bool), `NotifyCustomerOnResolutionByDefault` (bool). Adding a fourth setting later means adding one catalog entry, not a migration.
- **"Previewed or tested before being applied"** is a real two-step flow: `POST /admin/system-settings/validate` runs every catalog entry's validator against the proposed values and returns errors with **no persistence** — a true dry run, not a cosmetic confirmation dialog; `POST /admin/system-settings/apply` only persists once the caller has (implicitly or explicitly) validated. The frontend always validates before offering "Apply."
- "Configuration changes are logged in the audit trail" needs no bespoke code in this story — `apply` is a `POST`, already captured by SEC-3's global `AuditLoggingActionFilter`.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#` / .NET 10.
- `src/SupportCrm.Application/Sla/BusinessCalendarConfigService.cs` — read, not modified; this story's own service sits alongside it, not inside it.

## Out of scope

- Migrating business hours/holidays into the new `SystemSetting` store (see above — a deliberate non-goal, not an oversight).
- A fully generic, admin-authorable settings schema (new setting types still require a code change to the catalog).
