# Story intake

- Folder: `.squad/stories/reports-management/RM-1/intake.md`

---

## Feature

- **Feature name (display):** Reports & Management
- **Feature slug (folder under `plans/`):** `reports-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `RM-1`
- **Work item type:** `Story`

---

## Title

```
Ticket reports
```

---

## Description

```
Role: Support Manager
As a support manager, I want reports on ticket volume, categories, and trends, so that I can understand support demand.
```

---

## Acceptance criteria

```
- Reports can be filtered by date range, channel, category, and department/branch.
- Reports show volume trends over time (daily/weekly/monthly).
- Reports can be exported to Excel or PDF.
- Report data matches the underlying ticket records (no discrepancy on spot-check).
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** none new.
- **Depends on code areas or other stories:** `Ticket`, `TicketCategory`, `Customer.Branch` (this codebase's only organizational-grouping field — see Extra notes).

## Extra notes (optional)

- New bounded concern `SupportCrm.Application.Reports` — shared by every RM story (RM-1..5), one namespace per the established per-feature convention (mirrors `Sla`/`KnowledgeBase`/`Ai`/`CustomerPortal`).
- "Department/branch" — this codebase has no separate department concept; `Customer.Branch` (Customer Management CM-1) is the only organizational-grouping field that exists, so the branch filter is that field, unmodified.
- Two small repository gaps get fixed to support this (and every other RM story): `ITicketRepository`/`ICustomerRepository`/`ITicketCategoryRepository` gain a plain `GetAllAsync` — none of the three had a way to list everything, only scoped queries (by customer, by agent, active-only, etc.). Loaded once per report request and filtered/grouped in memory — the same "acceptable at this app's demo scale, flagged not hidden" standard already used for Customer Portal's `GetTicketsForCustomerAsync` N+1 note and the supervisor lookup in CP-5.
- Export is a shared `IReportExporter` abstraction (Application) with a concrete `ReportExporter` (Infrastructure, using ClosedXML for `.xlsx` and QuestPDF for PDF) — mirrors the `IAttachmentStorage`/`IEmailSender` interface-in-Application, implementation-in-Infrastructure shape used throughout this codebase. Both packages are real, network-restored NuGet packages (not mocked) — export produces genuine, openable files, unlike the AI features' mock-provider pattern (there's no "fake Excel file" equivalent that would make sense here).
- "No discrepancy on spot-check" — the report's `TotalCount` and the JSON report endpoint are built from the exact same in-memory-filtered ticket list the export endpoint serializes, so the two can never drift relative to each other; matching the underlying `Ticket` table is a property of not double-transforming data, not a separate reconciliation step.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#` / .NET 10.
- `src/SupportCrm.Application/Tickets/TicketService.cs`, `GetGroupedCountsAsync` — the existing (much simpler) precedent this story's richer, filterable, trend-aware report supersedes for manager-facing reporting; that endpoint is left untouched (still used by the agent-facing `ticket-reports` page).

## Out of scope

- SLA compliance (RM-2), agent performance (RM-3), CSAT (RM-4), the consolidated dashboard (RM-5) — each is its own story.
