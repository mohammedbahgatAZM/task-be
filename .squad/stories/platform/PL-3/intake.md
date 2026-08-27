# Story intake

- Folder: `.squad/stories/platform/PL-3/intake.md`

## Feature

- **Feature name (display):** Platform
- **Feature slug (folder under `plans/`):** `platform`

## Tracker

- **Tracker type:** `none` · **Work item id:** `PL-3` · **Work item type:** `Story`

## Title

```
Multi-department
```

## Description

```
Role: System Administrator
As a system administrator, I want to configure multiple departments, so that tickets are routed to and managed by the right team.
```

## Acceptance criteria

```
- Departments can be created, each with its own agents, categories, and queues.
- Tickets can be routed to a department automatically based on category or channel.
- Reports can be filtered and compared by department.
- A user's visibility can be restricted to their own department where required.
```

## Dependencies

- **Blocked by / related ids:** none new. Reuses `TicketChannel`, `TicketCategory`, `Team`, `Ticket`, and Reports & Management's `TicketReportQuery` (RM-1).

## Extra notes

- New `Department` entity (`Name`, `IsActive`, `DefaultForChannel: TicketChannel?`). "Its own agents, categories, and queues" is modeled as three additive nullable FKs on already-shipped entities — `Agent.DepartmentId`, `TicketCategory.DepartmentId`, `Team.DepartmentId` (`Team` is this codebase's existing "queue" concept — Ticket Management TM-3 — reused directly, not reinvented) — not a new join-table-per-relationship design.
- **Automatic routing**: a new `TicketDepartmentRoutingService`, called once more from `TicketService.CreateAsync` alongside the existing category-resolution step (AI-3/CP-1). Resolution order: the selected category's `DepartmentId` wins if set; otherwise, the first active department whose `DefaultForChannel` matches the ticket's channel. Neither matching → `Ticket.DepartmentId` stays `null` (an unrouted ticket, not an error) — mirrors this codebase's established "no match is a valid outcome, not a failure" convention (e.g. `SlaTargetService.ResolveAsync` returning `null`).
- **Reports filterable by department**: `TicketReportQuery`/`GetVolumeReportAsync` (Reports & Management RM-1) gains an optional `DepartmentId` filter and a `ByDepartment` breakdown, mirroring the existing `ByBranch` breakdown exactly — additive to an already-shipped, already-verified service.
- **Visibility restricted to own department** — explicitly **not built** in this story. This codebase has no existing row-level data-visibility scoping anywhere (Security & Administration's own permission system gates *actions*, not *which rows* a query returns), and building that generically is a large, separate undertaking. The data model (`Agent.DepartmentId`) is in place for a future enforcement layer to consume; flagged here exactly like Security & Administration flagged its own "not retrofitting existing controllers" boundary.

## Technical hints

- `src/SupportCrm.Application/Reports/TicketReportService.cs` (Reports & Management RM-1) — the file this story's `DepartmentId` filter/breakdown is added to.
- `src/SupportCrm.Application/Tickets/TicketService.cs`, `CreateAsync` — the exact insertion point for the new routing call, alongside the existing `categorizationService.CategorizeOnCreateAsync` call.

## Out of scope

- Department-scoped data visibility enforcement (see above).
- A full assignment-rule-engine-style routing UI — this story's routing is the two-step (category → channel-default) resolution described above, not a general rule builder.
