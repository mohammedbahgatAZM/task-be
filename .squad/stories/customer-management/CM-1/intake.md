# Story intake

- Folder: `.squad/stories/customer-management/CM-1/intake.md`

---

## Feature

- **Feature name (display):** Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CM-1`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Customer profiles
```

---

## Description

```
Role: Support Agent
As a support agent, I want to create and view a customer profile, so that I have a single, reliable record of who I'm working with.
```

---

## Acceptance criteria

```
- A new customer profile can be created with name, company/branch, and a unique customer ID.
- Opening a ticket automatically links it to the correct existing customer profile (no duplicates).
- The profile page shows a summary: contact info, open tickets, and last interaction date.
- Duplicate profiles can be detected and merged by an authorized user.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** none (first backend feature in this codebase).
- **Depends on code areas or other stories:** none yet.

## Extra notes (optional)

- AC 2 ("opening a ticket links to the correct customer") and part of AC 3 ("open tickets" in the summary) reference a Ticketing module that does not exist anywhere in this codebase yet. This story should implement customer creation plus a duplicate-detection/lookup capability, and expose it so a future Ticketing story can call it — the "open tickets" count in the summary can be a stub/seam (e.g. always 0, or an injected interface) until Ticketing exists. Flag this explicitly as an assumption in the plan.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Greenfield Clean Architecture .NET 10 solution: `SupportCrm.Domain` / `SupportCrm.Application` / `SupportCrm.Infrastructure` / `SupportCrm.Api`. No entities exist yet — this story establishes the first `Customer` aggregate.
- `SupportCrm.Infrastructure` already references EF Core + `Npgsql.EntityFrameworkCore.PostgreSQL` (no `DbContext` yet).
- `SupportCrm.Api` already references Swashbuckle + `Microsoft.AspNetCore.Authentication.JwtBearer` (no auth wired up yet — assume endpoints are unauthenticated for now unless the plan says otherwise).

## Out of scope

- Angular/UI implementation (covered by the matching frontend story in the frontend repo).
- Building the Ticketing module itself (tickets, calls, chats) — not built yet in this codebase.
- Sending outbound notifications.
