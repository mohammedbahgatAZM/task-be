# reports-management — plan overview

Entry point for the **reports-management** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 40 | [40-story-RM-1.md](40-story-RM-1.md) | Ticket reports | RM-1 | — |
| 41 | [41-story-RM-2.md](41-story-RM-2.md) | SLA performance | RM-2 | Story 40, SLA & Automation Stories 21/22 |
| 42 | [42-story-RM-3.md](42-story-RM-3.md) | Agent performance | RM-3 | Story 40, Customer Portal Story 39 |
| 43 | [43-story-RM-4.md](43-story-RM-4.md) | Customer satisfaction | RM-4 | Story 40, Customer Portal Story 39 |
| 44 | [44-story-RM-5.md](44-story-RM-5.md) | Management dashboards | RM-5 | Stories 40–43 |

## Dependency notes

- Story 40 introduces the whole `SupportCrm.Application.Reports` bounded concern: `ReportDtos.cs`, the `IReportExporter`/`ReportExporter` export abstraction (ClosedXML + QuestPDF, both real NuGet packages), and three small repository gap-fills (`ITicketRepository`/`ICustomerRepository`/`ITicketCategoryRepository` each gain a plain `GetAllAsync`) that every later RM story relies on.
- Story 41 adds no new tables — it reuses SLA & Automation's `SlaCalculationService` for currently-open tickets (so an open ticket's breach status in the report is computed by the literal same code as the ticket's own page) and adds one new historical-compliance calculation only for already-resolved tickets, which `SlaCalculationService` has no way to answer (it only ever compares against "now").
- Stories 42/43 both read `TicketFeedback` (Customer Portal Story 39) as the only CSAT data source in this codebase; NPS is explicitly not fabricated from it (see Story 43's own note).
- Story 44 adds zero new metric calculations — it is a pure composition over Stories 40–43's services.
- No EF Core migration is needed anywhere in this feature — every report is computed from existing tables.
