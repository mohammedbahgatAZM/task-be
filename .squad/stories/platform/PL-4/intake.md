# Story intake

- Folder: `.squad/stories/platform/PL-4/intake.md`

## Feature

- **Feature name (display):** Platform
- **Feature slug (folder under `plans/`):** `platform`

## Tracker

- **Tracker type:** `none` · **Work item id:** `PL-4` · **Work item type:** `Story`

## Title

```
Multi-branch
```

## Description

```
Role: System Administrator
As a system administrator, I want to configure multiple branches or locations, so that regional data and operations are managed separately.
```

## Acceptance criteria

```
- Branches can be created and associated with their own customers, agents, and settings (e.g. business hours).
- Tickets and reports can be filtered and segmented by branch.
- Head-office roles can view data across all branches; branch roles see only their own.
- Branch-specific configuration (language default, contact numbers) does not affect other branches.
```

## Dependencies

- **Blocked by / related ids:** none new. `Customer.Branch` (a plain string, Customer Management CM-1) already exists and is already the filter field Reports & Management RM-1/RM-5 use — this story does not replace it.

## Extra notes

- New `Branch` entity (`Name`, `Code`, `DefaultLanguage`, `ContactNumber`, `IsActive`) — a genuinely new, manageable, first-class entity, not a rename of the existing string field.
- **`Customer.Branch` (string) is kept, unmodified.** A new, parallel `Customer.BranchId` (nullable FK to `Branch`) is added *alongside* it, and `Agent.BranchId` is added too. Migrating every existing branch-name string to a real FK — and every place that already filters/reads `Customer.Branch` (Reports & Management RM-1/RM-5) — is a larger, riskier retrofit than this story takes on; the two fields coexist deliberately, flagged, not silently duplicated by accident.
- **"Business hours… per branch" is explicitly NOT implemented.** SLA & Automation's `BusinessHours` table (Story 22) is a single global calendar, one row per `DayOfWeek` with `DayOfWeek` as its key — not scoped to anything. Making it branch-scoped would mean changing that table's primary key shape and every consumer of `BusinessCalendarService`, which is large, risky, already-shipped, already-verified code. This story instead implements the *other* two examples the AC itself names as branch-specific config — **language default and contact number** — as real fields directly on the new `Branch` entity. Business-hours-per-branch is a clearly flagged non-goal, not an oversight.
- "Head-office vs. branch-role visibility" — same boundary as PL-3's department visibility: the data model (`Agent.BranchId`) exists; enforcement is a follow-up, not built here.

## Technical hints

- `src/SupportCrm.Domain/Entities/Customer.cs`, `Branch` property (line 9) — read first, to see exactly what's being left alone.
- `src/SupportCrm.Application/Sla/BusinessCalendarService.cs`/`BusinessHours` — read to confirm the single-global-calendar shape that makes per-branch business hours out of scope.

## Out of scope

- Per-branch business hours (see above).
- Migrating `Customer.Branch` (string) off to `BranchId` everywhere it's currently read.
- Branch-scoped data visibility enforcement.
