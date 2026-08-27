# Story intake

- Folder: `.squad/stories/ticket-management/TM-2/intake.md`

---

## Feature

- **Feature name (display):** Ticket Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `TM-2`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Categories and priorities
```

---

## Description

```
Role: Support Agent
As a support agent, I want to categorize and prioritize tickets, so that urgent or specialized issues are handled appropriately.
```

---

## Acceptance criteria

```
- Tickets can be assigned a category/sub-category from a configurable list.
- Tickets can be assigned a priority level (e.g. Low, Medium, High, Urgent).
- Category and priority can be changed after creation, with the change logged.
- Reports can be filtered and grouped by category and priority.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** TM-1 (Create and track tickets) — needs the `Ticket` aggregate.
- **Depends on code areas or other stories:** backend TM-1 (`.squad/plans/ticket-management/NN-story-TM-1.md`).

## Extra notes (optional)

- "Configurable list" means category/sub-category are stored rows an admin can add to, not a hardcoded enum — model `TicketCategory` (with an optional `ParentCategoryId` for sub-categories) as its own table, seeded with a small starter set.
- "Change logged" reuses the same change-log pattern Customer Management's `ContactDetailChangeLogEntry` established (CM-2) — follow that precedent rather than inventing a new audit shape.
- "Reports... filtered and grouped" only needs a query endpoint returning counts grouped by category/priority for this story — a full reporting/BI view is not implied.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on the `Ticket` aggregate from TM-1.

## Out of scope

- A reporting/analytics UI beyond a basic grouped-count endpoint — dashboards are TM-3's concern (per-agent load) and general reporting is not requested beyond this story's filter/group AC.
