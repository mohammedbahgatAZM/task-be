# Story intake

- Folder: `.squad/stories/reports-management/RM-2/intake.md`

---

## Feature

- **Feature name (display):** Reports & Management
- **Feature slug (folder under `plans/`):** `reports-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `RM-2`
- **Work item type:** `Story`

---

## Title

```
SLA performance
```

---

## Description

```
Role: Support Manager
As a support manager, I want SLA compliance reports, so that I can monitor whether targets are being met.
```

---

## Acceptance criteria

```
- Reports show percentage of tickets meeting response and resolution SLAs.
- Breaches can be broken down by team, agent, category, and priority.
- Trends over time are visualized to spot degrading performance early.
- Report figures reconcile with the SLA data shown on individual tickets.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Story 40 (RM-1) — `Reports` bounded concern, `ITicketRepository.GetAllAsync`.
- **Depends on code areas or other stories:** SLA & Automation's `SlaCalculationService`, `SlaTargetService`, `BusinessCalendarService` (Stories 21/22) — reused, not reimplemented.

## Extra notes (optional)

- **"Reconcile with individual-ticket SLA data" is the reason this story does NOT write a parallel compliance calculator from scratch.** For currently-open tickets, it calls the exact same `SlaCalculationService.GetStatusesAsync` that `ticket-sla-status`/`AgentDashboardService` already use — an open ticket's breach flag in this report is computed by literally the same code path as the one shown on that ticket's own page, so the two can never disagree.
- Closed/resolved tickets need a different calculation `SlaCalculationService` doesn't provide (it only ever compares against "now" — a resolved ticket needs its *actual* response/resolution timestamp compared against target, not "now"). A new `SlaComplianceService` (this story) adds that one missing piece — reusing `SlaTargetService.ResolveAsync` and `BusinessCalendarService.CalculateBusinessMinutesBetweenAsync` directly, plus a small Pending-pause helper shaped like (but not sharing code with) `SlaCalculationService`'s own private `GetPendingBusinessMinutesAsync` — that existing method is bounded to "now"; this story's version is bounded to the historical response/resolution event instead, which is different enough to not be a safe parameterization of the shipped real-time method. Flagged as intentional near-duplication, not an oversight.
- A ticket still open and not yet past its due date is neither a pass nor a fail yet — excluded from the compliance percentage, but counted separately (`InProgressNotYetEvaluableCount`) so the report is honest about what it did and didn't judge, rather than silently treating "not yet breached" as "compliant."
- The trend is fixed to **weekly** buckets (unlike RM-1's daily/weekly/monthly toggle) — SLA compliance is noisier at daily granularity with this app's typical ticket volumes, and the AC only asks to "spot degrading performance early," which a weekly trend already serves. Deliberate scope-narrowing, not an oversight.
- "Team" breakdown uses `Ticket.AssignedTeamId` → `Team.Name` (Ticket Management TM-3); tickets with no team assignment fall into an "Unassigned" bucket, not dropped.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#` / .NET 10.
- `src/SupportCrm.Application/Sla/SlaCalculationService.cs` — read in full; this story's service is a sibling, not a replacement.

## Out of scope

- Agent performance (RM-3), CSAT (RM-4), consolidated dashboard (RM-5).
