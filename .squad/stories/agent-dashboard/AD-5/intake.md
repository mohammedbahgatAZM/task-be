# Story intake

- Folder: `.squad/stories/agent-dashboard/AD-5/intake.md`

---

## Feature

- **Feature name (display):** Agent Dashboard
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `AD-5`
- **Work item type:** `Story`

---

## Title

```
Team collaboration
```

---

## Description

```
Role: Support Agent
As a support agent, I want to tag colleagues and leave internal comments on a ticket, so that we can collaborate on cases that need more than one person.
```

---

## Acceptance criteria

```
- An agent can @-mention a colleague in an internal note, triggering a notification to them.
- Internal comments are never visible to the customer.
- A ticket can show multiple collaborators alongside the primary assignee.
- Collaboration activity appears in the ticket's timeline.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Agent Dashboard AD-3 (`AgentNotification` mechanism, reused here), Ticket Management TM-5 (`TicketNote`, `TicketTimelineService`), TM-3 (`Ticket.AssignedAgentId`, `Agent`).
- **Depends on code areas or other stories:** backend TM-5 (`TicketNote`, timeline compose box's "Internal note" mode — already customer-invisible, satisfies AC #2 with no new work), AD-3 (`AgentNotification`, `GET /api/agents/{agentId}/notifications`).

## Extra notes (optional)

- **Internal comments already never customer-visible** — Ticket Management TM-5's "Internal note" compose mode already sets `IsCustomerVisible: false` on the timeline entry. This story adds @-mention parsing on top of that existing note text, it does not introduce a second kind of comment.
- **@-mention parsing:** `@AgentName` tokens in a note's text are matched against existing `Agent.Name`s (case-insensitive, exact full-name match — no fuzzy matching, no autocomplete UI in this story's backend scope) when the note is saved. Each matched agent gets an `AgentNotification` (reusing AD-3's mechanism) and is auto-added as a `TicketCollaborator` if not already one.
- **New entity:** `TicketCollaborator(TicketId, AgentId, AddedAtUtc)` — many-to-many, distinct from `Ticket.AssignedAgentId` (the single primary assignee from Ticket Management TM-3). An agent can also be added as a collaborator explicitly (not only via @-mention).
- **Timeline:** `TicketTimelineService` gets a new entry `Kind = "Collaboration"` for collaborator-added events (mentions themselves still show as ordinary `"Note"` entries — that part already works, no change needed).

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on `TicketNote`/`TicketTimelineService` (TM-5), `AgentNotification` (AD-3), `Agent` (TM-3).

## Out of scope

- Removing a collaborator (add-only for this story; a "remove" action can be a follow-up if needed).
- Autocomplete/typeahead UI for @-mentions while typing — plain text `@Name` is parsed on save.
- Fuzzy/partial name matching for mentions — exact (case-insensitive) full name only.
