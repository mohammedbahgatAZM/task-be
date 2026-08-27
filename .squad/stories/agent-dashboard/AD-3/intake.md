# Story intake

- Folder: `.squad/stories/agent-dashboard/AD-3/intake.md`

---

## Feature

- **Feature name (display):** Agent Dashboard
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `AD-3`
- **Work item type:** `Story`

---

## Title

```
Tasks and reminders
```

---

## Description

```
Role: Support Agent
As a support agent, I want to set tasks and reminders on tickets, so that I don't forget necessary follow-ups.
```

---

## Acceptance criteria

```
- An agent can create a task with a due date and note, linked to a ticket.
- The agent receives a reminder/notification when a task is due.
- Overdue tasks are highlighted on the dashboard.
- Tasks can be reassigned to another agent.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Agent Dashboard AD-1 (agent switcher/identity, dashboard shell).
- **Depends on code areas or other stories:** backend TM-1 (`Ticket`), AD-1 (agent switcher, dashboard component this story adds an "overdue tasks" section to).

## Extra notes (optional)

- **New entity:** `TicketTask(TicketId, Note, DueAtUtc, AssignedAgentId, IsCompleted, CreatedBy, CreatedAtUtc)`.
- **Team decision — notification mechanism:** this story introduces a single shared, polling-based `AgentNotification` mechanism (`GET /api/agents/{agentId}/notifications`, `POST .../{id}/read`) that Agent Dashboard AD-5's @-mentions also reuses — one notification system, not two. There is no background job scheduler in this app and this story does not add one; due-task notifications are computed **lazily**: whenever an agent's notifications are polled, any of that agent's incomplete tasks with `DueAtUtc` at or before "now" that haven't already been notified get a notification materialized at that moment. This is a deliberate, documented scope decision (consistent with Communication Channels' mock-provider seams) — flag it explicitly, don't imply a real scheduled reminder system exists.
- **Reassignment:** just updates `TicketTask.AssignedAgentId` — the AC doesn't require notifying the new assignee, so this story doesn't add one (avoid scope creep beyond the stated AC).
- **Dashboard integration:** "Overdue tasks are highlighted on the dashboard" extends AD-1's dashboard (already built) with an additional section/badge, rather than a separate screen.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on `Ticket` (TM-1), `Agent` (TM-3), the AD-1 dashboard and agent switcher.

## Out of scope

- Real push/email/SMS reminders — in-app, poll-based notifications only (same "no real provider" stance as Communication Channels).
- A background job scheduler — notifications are computed lazily on poll, not on a timer.
- Notifying an agent when a task is reassigned to them.
