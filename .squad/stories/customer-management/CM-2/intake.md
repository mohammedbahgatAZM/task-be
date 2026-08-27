# Story intake

- Folder: `.squad/stories/customer-management/CM-2/intake.md`

---

## Feature

- **Feature name (display):** Customer Management
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CM-2`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Contact details
```

---

## Description

```
Role: Support Agent
As a support agent, I want to store and update multiple contact details for a customer (phone, email, WhatsApp number, address), so that I can reach them through their preferred channel.
```

---

## Acceptance criteria

```
- A customer can have multiple phone numbers, emails, and a WhatsApp number, with one marked as primary per type.
- Contact details can be edited and are versioned/logged (who changed what, when).
- Invalid formats (e.g. malformed email or phone) are rejected with a clear error message.
- Preferred contact channel can be flagged and is respected by outbound notifications.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** CM-1 (Customer profiles) — needs the `Customer` aggregate.
- **Depends on code areas or other stories:** backend CM-1 (`.squad/plans/customer-management/01-story-CM-1.md`).

## Extra notes (optional)

- "Respected by outbound notifications" only means the preferred-channel flag is stored and readable — actually sending notifications is out of scope (no notification module exists yet).

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on the `Customer` aggregate added in CM-1 (`SupportCrm.Domain` / `Application` / `Infrastructure` / `Api`). EF Core + Npgsql already referenced in `SupportCrm.Infrastructure`.
- "Versioned/logged" implies an audit trail (who/when) per contact-detail change — decide in the plan whether this is an audit table, event log, or temporal table.

## Out of scope

- Angular/UI implementation (covered by the matching frontend story in the frontend repo).
- Actually sending outbound notifications through any channel.
