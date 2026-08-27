# Story intake

- Folder: `.squad/stories/ticket-management/TM-4/intake.md`

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `TM-4`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Status and escalation
```

---

## Description

```
Role: Support Agent
As a support agent, I want to update a ticket's status and escalate it when needed, so that stakeholders know where things stand and unresolved issues get attention.
```

---

## Acceptance criteria

```
- Ticket status can be set to at least: New, Open, Pending, Resolved, Closed.
- An agent can escalate a ticket to a supervisor or specialist team with one action.
- Escalating a ticket requires (or allows) a reason/comment.
- The customer is notified when their ticket's status changes, if configured to do so.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** TM-1 (ticket + status history already recorded there), TM-3 (escalating to a specialist "team" needs the `Team` entity from TM-3).
- **Depends on code areas or other stories:** backend TM-1, backend TM-3.

## Extra notes (optional)

- TM-1 already timestamps and attributes every status change (per its own AC) — this story adds the specific status vocabulary (New/Open/Pending/Resolved/Closed) as a real enum/lookup validated on write, plus the escalation action, which is distinct from a plain status change (it also reassigns to a supervisor/team and requires a reason).
- "The customer is notified... if configured to do so" implies a per-ticket or global toggle (`NotifyCustomerOnStatusChange`), not that every status change always notifies. Reuse TM-3's `IAssignmentNotifier`-style seam pattern for a `ICustomerStatusNotifier` (stub/no-op), since no real notification channel exists yet — same documented gap as CM-2 and TM-3.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on the `Ticket` aggregate (TM-1) and `Team` entity (TM-3).

## Out of scope

- Real customer notification delivery — only the seam + a stub/no-op, same as TM-3.
- SLA timers / automatic escalation rules — this story is a manual, one-action escalation only.
