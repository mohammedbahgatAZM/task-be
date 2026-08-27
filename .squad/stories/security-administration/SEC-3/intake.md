# Story intake

- Folder: `.squad/stories/security-administration/SEC-3/intake.md`

---

## Feature

- **Feature name (display):** Security & Administration
- **Feature slug (folder under `plans/`):** `security-administration`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `SEC-3`
- **Work item type:** `Story`

---

## Title

```
Audit logs
```

---

## Description

```
Role: Compliance Officer
As a compliance officer or administrator, I want an audit log of system actions, so that I can track changes and investigate issues.
```

---

## Acceptance criteria

```
- Key actions (login, data changes, permission changes, deletions) are logged with user, timestamp, and details.
- Audit logs are read-only and cannot be edited or deleted by regular users.
- Logs can be filtered by user, date range, and action type.
- Logs can be exported for external audit or compliance review.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Story 45 (SEC-1) — `User`, JWT claims (who's acting).
- **Depends on code areas or other stories:** Reports & Management's `IReportExporter` (Story 40) — reused directly for this story's own export, not reimplemented.

## Extra notes (optional)

- **A single global `AuditLoggingActionFilter`, registered app-wide, not per-controller.** Every mutating request (`POST`/`PUT`/`DELETE`/`PATCH`) across the *entire* API — every prior module included — is logged automatically: who (from the JWT if present, else `"anonymous"`), the HTTP method + route, a short action summary, the timestamp, and the caller's IP. This is how the AC's "data changes… are logged" reaches Tickets/Customers/Knowledge Base/etc. **without** touching any of those controllers' code — a cross-cutting filter, not a per-endpoint retrofit (see SEC-1's own scope note on why per-endpoint *authorization* retrofitting is out of scope; *logging* via a global filter carries none of that risk, since it only observes, never blocks).
- "Read-only, cannot be edited or deleted" is enforced by omission: no endpoint anywhere accepts an audit-log update or delete — the only write path is the filter itself, which never exposes a public "create" action either. There is nothing to secure against because nothing else can reach it.
- "Permission changes… logged" and "deletions… logged" both fall out of the same global filter for free — a role's permission update is a `PUT`, a user delete is a `DELETE`, both auto-captured.
- Login itself is logged the same way (`POST /api/auth/login` is a mutating-verb request to the filter's eyes) — including failed attempts, since the filter logs the request regardless of the response status.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#` / .NET 10.
- ASP.NET Core `IAsyncActionFilter`, registered via `options.Filters.AddService<AuditLoggingActionFilter>()` in `Program.cs`.

## Out of scope

- Logging `GET` (read) requests — the AC's own examples (login, data changes, permission changes, deletions) are all mutations; logging every read would be enormous noise for a demo app and isn't asked for.
