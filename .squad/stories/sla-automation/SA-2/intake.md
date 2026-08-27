# Story intake

- Folder: `.squad/stories/sla-automation/SA-2/intake.md`

---

## Feature

- **Feature name (display):** SLA & Automation
- **Feature slug (folder under `plans/`):** `sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `SA-2`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Automatic assignment
```

---

## Description

```
Role: System
As a support manager, I want tickets to be automatically assigned based on rules such as category, workload, or skill, so that response times are minimized.
```

---

## Acceptance criteria

```
- Assignment rules can be configured based on category, channel, language, or agent skill.
- Auto-assignment considers current agent workload to balance the queue.
- A ticket that matches no rule falls back to a default queue rather than being lost.
- Auto-assigned tickets notify the assigned agent immediately.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** TM-1 (`Ticket`), TM-2 (`TicketCategory`), TM-3 (`Agent`, `Team`, `TicketAssignmentService`, `IAssignmentNotifier`).
- **Depends on code areas or other stories:** backend TM-1, TM-2, TM-3 — this story adds a rules engine on top of TM-3's existing manual `TicketAssignmentService.AssignAsync`, it does not replace it.

## Extra notes (optional)

- `Agent` (`Agent.cs`) has no skill or language fields today. Add minimal `Skills`/`Languages` collections (or tags) to `Agent` as a stand-in for a real skills-matrix module, flagged explicitly.
- `Ticket` has `Channel` (`TicketChannel`) already from TM-1 but no language field. Add a `Language` property to `Ticket` (nullable, defaults unset) as a stand-in — real language detection from message content is out of scope.
- "Auto-assignment considers current agent workload" — reuse TM-3's `TicketAssignmentService.GetAgentLoadAsync` / `CountOpenGroupedByAgentAsync` as the workload signal rather than building a second load query.
- "Notifies the assigned agent immediately" — reuse/extend TM-3's `IAssignmentNotifier` seam (`NoOpAssignmentNotifier` stub already exists) rather than defining a second notification interface; add a method for "new auto-assignment" if `NotifyReassignedAsync`'s semantics don't fit a from-nothing assignment.
- Define rule structure and precedence explicitly in the plan: an ordered list of rules (each with match conditions on category/channel/language/skill and a target agent/team/pool), evaluated top-down; first match wins; no match → default queue (an unassigned state, distinct from "no rules configured at all").
- Rule *configuration* (CRUD for rules) is in scope; a visual rule builder UI is not — a simple ordered-list admin view is enough.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- New service under `src/SupportCrm.Application/Sla/` (or `Tickets/`, matching where TM-3's `TicketAssignmentService` already lives) e.g. `AutoAssignmentService` / `AssignmentRuleEngine`, plus `src/SupportCrm.Domain/Entities/AssignmentRule.cs` and persistence under `src/SupportCrm.Infrastructure/Persistence/`.
- Should call into TM-3's existing `TicketAssignmentService.AssignAsync` to perform the actual assignment + audit entry + notification, rather than duplicating that logic.

## Out of scope

- SLA target configuration/breach calculation (SA-1) and escalation rules (SA-3) — this story only decides *who* a new ticket goes to, not response/resolution timers.
- A full skills-matrix or workforce-management module — only minimal tags on `Agent`.
- A drag-and-drop/visual rule builder — a simple ordered rule list is sufficient.
