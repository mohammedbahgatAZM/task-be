# Story intake

- Folder: `.squad/stories/agent-dashboard/AD-4/intake.md`

---

## Feature

- **Feature name (display):** Agent Dashboard
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `AD-4`
- **Work item type:** `Story`

---

## Title

```
Quick replies
```

---

## Description

```
Role: Support Agent
As a support agent, I want to use pre-written quick reply templates, so that I can respond faster to common questions.
```

---

## Acceptance criteria

```
- Agents can insert a saved template into a reply with one click or shortcut.
- Templates support placeholders (e.g. customer name, ticket number) that auto-fill.
- Templates can be organized by category and shared across the team.
- Authorized users can create, edit, and retire templates.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Agent Dashboard AD-1 (agent switcher/identity, agent-admin list), Ticket Management TM-5 / Communication Channels CC-6 (the unified reply compose box this story's "insert template" action attaches to).
- **Depends on code areas or other stories:** backend TM-5 (`TicketTimelineComponent`'s compose box, extended by CC-6's unified reply).

## Extra notes (optional)

- **New entity:** `QuickReplyTemplate(Category, Name, Body, IsRetired, CreatedBy, CreatedAtUtc)` — global/shared, not per-agent (per the AC's "shared across the team").
- **Placeholders:** stored as literal tokens in `Body` (e.g. `{{CustomerName}}`, `{{TicketReferenceNumber}}`). Rendering happens **server-side** via `POST /api/quick-reply-templates/{id}/render` (given a `ticketId`) — the backend already has the ticket's and customer's data loaded elsewhere, and keeping the token syntax/resolution in one place avoids the frontend re-implementing (and potentially drifting from) the same logic. The frontend calls render, then inserts the returned plain text into the compose box.
- **Retiring, not deleting:** `IsRetired` is a soft flag — retired templates disappear from the "insert template" picker but stay visible/editable in the admin list (audit-friendly, and cheap: no cascading-delete concerns for templates used in past replies, since a *rendered* reply is just plain text already saved as a `TicketMessage`/`TicketNote` — retiring a template never touches history).
- **"Authorized users":** per this app's established precedent (Communication Channels CC-5's web-form field admin has no permission gate either), this story does **not** add a new permission check for template CRUD — any agent can manage templates for now. This is a deliberate scope decision, not an oversight: introducing enforcement here would be the *first* place in the whole app enforcing "authorized users" for an admin action, which is a bigger, cross-cutting decision better made once (e.g. alongside AD-2's `CanViewSensitiveData`) than bolted onto one template screen. Flag this explicitly rather than silently gating or silently not gating.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on the TM-5/CC-6 timeline compose box (insertion point), TM-2's `TicketCategory` is a *separate* concept from this story's own template categories (plain free-text strings) — don't conflate the two.

## Out of scope

- A rich template editor (formatting, rich text) — plain text with `{{Placeholder}}` tokens only.
- Enforcing "authorized users" as a real permission check (see note above) — flagged, not silently decided either way beyond "no gate yet."
