# customer-management — plan overview

Entry point for the **customer-management** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 01 | [01-story-CM-1.md](01-story-CM-1.md) | Customer profiles | CM-1 | None |
| 02 | [02-story-CM-2.md](02-story-CM-2.md) | Contact details | CM-2 | Story 01 |
| 03 | [03-story-CM-3.md](03-story-CM-3.md) | Interaction history | CM-3 | Story 01 |
| 04 | [04-story-CM-4.md](04-story-CM-4.md) | Notes and attachments | CM-4 | Story 01 |

## Dependency notes

- Stories 02–04 all depend on the `Customer` aggregate and `SupportCrmDbContext` introduced in Story 01 — execute Story 01 first.
- Story 03's interaction timeline is designed to consume notes from Story 04; either order works for 02–04 once 01 lands, but 03 gets more value once 04 exists.
- The matching frontend repo (`../../frontend/.squad/plans/customer-management/`) has its own story sequence per layer; each frontend story depends on its same-numbered backend story's API contract.
