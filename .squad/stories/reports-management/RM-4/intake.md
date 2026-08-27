# Story intake

- Folder: `.squad/stories/reports-management/RM-4/intake.md`

---

## Feature

- **Feature name (display):** Reports & Management
- **Feature slug (folder under `plans/`):** `reports-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `RM-4`
- **Work item type:** `Story`

---

## Title

```
Customer satisfaction
```

---

## Description

```
Role: Support Manager
As a support manager, I want to see CSAT/NPS scores and trends, so that I can measure and improve service quality.
```

---

## Acceptance criteria

```
- Overall and segmented (by category/agent/channel) CSAT scores are available.
- Trend charts show satisfaction over time with the ability to drill into low-scoring periods.
- Negative feedback comments are listed alongside the score for context.
- Satisfaction data can be exported for further analysis.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Story 40 (RM-1) — `Reports` bounded concern, `IReportExporter`.
- **Depends on code areas or other stories:** `TicketFeedback` (Customer Portal CP-5) — the sole data source; `CustomerPortalOptions.LowRatingThreshold` (CP-5), reused as the "negative feedback" cutoff rather than inventing a second threshold.

## Extra notes (optional)

- **NPS is explicitly out of scope for this story and is not fabricated from CSAT data.** Real NPS is a 0–10 "how likely are you to recommend us" question with promoter/passive/detractor segmentation — this codebase has never collected that; the only satisfaction data anywhere is Customer Portal CP-5's 1–5 post-resolution `TicketFeedback` rating. This story implements the CSAT half of the AC in full (overall/segmented scores, trend, drill-in via low-scoring periods, negative-comment listing, export) and does **not** derive a synthetic NPS number from a 1–5 scale, which would misrepresent what was actually measured — matching the same "honest proxy, not a fabricated causal claim" discipline Customer Portal CP-4's deflection-rate metric already established.
- "Drill into low-scoring periods" — the trend response carries enough (`PeriodStart`, average, count) for the frontend to let a manager click a low point and re-query the same report with `From`/`To` narrowed to that period; no separate drill-in endpoint, the existing filtered query already does this.
- "Negative feedback… listed alongside the score" reuses `CustomerPortalOptions.LowRatingThreshold` (default 2) as the cutoff for what counts as "negative" — one threshold, one meaning, rather than a second manager-facing definition of "low" diverging from the one that already triggers CP-5's supervisor follow-up task.
- Export reuses RM-1's `IReportExporter` — the CSAT report's negative-feedback list and segment breakdowns are exported the same way RM-1's ticket list is (`.xlsx`/PDF), not a second export mechanism.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#` / .NET 10.
- `src/SupportCrm.Application/CustomerPortal/TicketFeedbackService.cs` — the only other consumer of `TicketFeedback` today; this story adds `ITicketFeedbackRepository.GetAllAsync` (a plain listing method, same gap-filling shape as RM-1's other repository additions) rather than routing through that per-ticket service.

## Out of scope

- NPS (see above).
- Ticket volume (RM-1), SLA compliance (RM-2), per-agent metrics beyond the `ByAgent` CSAT segment (RM-3's own, richer concern), consolidated dashboard (RM-5).
