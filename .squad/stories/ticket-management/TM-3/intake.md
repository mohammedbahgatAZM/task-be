# Story intake

- Folder: `.squad/stories/ticket-management/TM-3/intake.md`

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `TM-3`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Assign tickets to agents
```

---

## Description

```
Role: Team Lead
As a team lead, I want to assign tickets to specific agents or teams, so that ownership is clear and workload is balanced.
```

---

## Acceptance criteria

```
- A ticket can be manually assigned to one agent or one team.
- Reassignment is possible and notifies both the previous and new assignee.
- The dashboard shows current ticket load per agent to support balanced assignment.
- Unassigned tickets are visibly flagged so none are missed.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** TM-1 (Create and track tickets).
- **Depends on code areas or other stories:** backend TM-1.

## Extra notes (optional)

- No user/agent identity or team-management module exists anywhere in this codebase (no seeded ASP.NET Identity users, despite the package being referenced). Model lightweight `Agent` and `Team` reference entities (id + name) scoped to this feature — enough to give assignment real foreign keys and a real "load per agent" query — rather than blocking on a full identity system. Flag this explicitly as a stand-in for real user management.
- "Notifies both the previous and new assignee" — no notification channel (email/push/SMS) exists yet, same gap as Customer Management's CM-2 preferred-channel AC. Define an `IAssignmentNotifier` seam with a no-op/logging stub implementation, so a real notifier can be registered later without touching the assignment service.
- "Ticket load per agent" is a count of currently-open (non-Closed/non-Resolved) tickets assigned to each agent — define the exact status set considered "load" in the plan.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on the `Ticket` aggregate from TM-1.

## Out of scope

- Real notification delivery (email/SMS/push) — only the seam + a stub.
- Automatic/rules-based assignment (round-robin, skill-based routing) — this story is manual assignment only, per the AC.
