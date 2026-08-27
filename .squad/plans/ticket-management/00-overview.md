# ticket-management — plan overview

Entry point for the **ticket-management** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 05 | [05-story-TM-1.md](05-story-TM-1.md) | Create and track tickets | TM-1 | Customer Management Stories 01–04 |
| 06 | [06-story-TM-2.md](06-story-TM-2.md) | Categories and priorities | TM-2 | Story 05 |
| 07 | [07-story-TM-3.md](07-story-TM-3.md) | Assign tickets to agents | TM-3 | Story 05 |
| 08 | [08-story-TM-4.md](08-story-TM-4.md) | Status and escalation | TM-4 | Story 05, Story 07 |
| 09 | [09-story-TM-5.md](09-story-TM-5.md) | Ticket history | TM-5 | Story 05, Story 07, Story 08 |

## Dependency notes

- Story 05 is foundational for this feature and also closes two long-standing Customer Management gaps: it implements CM-1's "ticket links to customer" assumption, and replaces CM-1's `StubCustomerActivitySummaryProvider` with a real implementation backed by ticket + interaction data.
- Stories 06–09 all depend on Story 05's `Ticket` aggregate. Story 08 additionally depends on Story 07's `Agent`/`Team` entities and `TicketAssignmentService` (escalation reuses reassignment). Story 09 merges audit tables from Stories 05, 07, and 08 — execute those first.
- No PDF-generation dependency is added anywhere in this feature — Story 09's "export" AC is implemented as a frontend print-to-PDF view.
