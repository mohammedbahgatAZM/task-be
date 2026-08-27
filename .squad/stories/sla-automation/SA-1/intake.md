# Story intake

- Folder: `.squad/stories/sla-automation/SA-1/intake.md`

---

## Feature

- **Feature name (display):** SLA & Automation
- **Feature slug (folder under `plans/`):** `sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `SA-1`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Response and resolution targets
```

---

## Description

```
Role: Support Manager
As a support manager, I want to define SLA targets for response and resolution times, so that customer expectations are met consistently.
```

---

## Acceptance criteria

```
- SLA targets can be configured per priority level and/or category.
- Different SLA policies can apply to different customer segments or contract tiers.
- The system calculates and displays time-to-breach for each ticket in real time.
- Business hours and holidays are factored into SLA time calculations.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** TM-1 (ticket + `CreatedAtUtc`), TM-2 (`TicketCategory`), TM-4 (`TicketPriority`, `TicketStatus`).
- **Depends on code areas or other stories:** backend TM-1 (`Ticket`, `ITicketRepository`), TM-2 (`TicketCategory`), TM-4 (`TicketPriority` enum, status vocabulary).

## Extra notes (optional)

- No `Customer` "segment" or "contract tier" field exists yet (`Customer.cs` has no such property) — add a lightweight `CustomerTier` (or similar) enum/reference on `Customer` as a stand-in, flagged explicitly, so SLA policies can key off it; do not build a full contracts/billing module.
- No business-hours/holiday calendar exists anywhere in this codebase. Model a simple `BusinessCalendar` (weekly working hours + a holiday date list), single global calendar for this story — do not build per-region/per-team calendars unless trivial. Flag as a stand-in a real scheduling/calendar system would replace.
- "SLA targets per priority and/or category" implies a policy resolution order when both a priority-level and a category-level (and a tier-level) target could match the same ticket — define the precedence explicitly in the plan (e.g. most specific match wins: tier+category+priority > category+priority > priority alone).
- "Calculates and displays time-to-breach in real time" — this story owns the calculation service and a queryable per-ticket breach time/remaining-time; it does not require a live push/websocket UI (that's a display concern, not core SLA logic). Response-clock vs resolution-clock: define both as separate SLA target types, each stopped/paused by the appropriate ticket status (e.g. clock pauses while `Pending`).

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- New bounded concern `Sla` — mirror the existing convention: `src/SupportCrm.Domain/Entities/Sla*.cs`, `src/SupportCrm.Application/Sla/`, `src/SupportCrm.Infrastructure/Persistence/Sla*.cs`, `src/SupportCrm.Api/Controllers/SlaController.cs`.
- Reuse `Ticket.CreatedAtUtc`, `Ticket.Priority` (`TicketPriority`), `Ticket.CategoryId` (`TicketCategory`), and `Ticket.Status` (`TicketStatus`) from TM-1/TM-2/TM-4 as calculation inputs — do not duplicate ticket fields.
- `TimeProvider` is already used for testable UTC time in `TicketAssignmentService` — reuse the same pattern for the breach-time clock so it stays testable.

## Out of scope

- Auto-assignment (SA-2), escalation actions/rules (SA-3), and alert/notification delivery (SA-4) — each is its own story below; this story is target configuration + breach-time calculation only.
- Full contracts/billing/tier management — only a minimal tier marker on `Customer`.
