# Story intake

- Folder: `.squad/stories/reports-management/RM-5/intake.md`

---

## Feature

- **Feature name (display):** Reports & Management
- **Feature slug (folder under `plans/`):** `reports-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `RM-5`
- **Work item type:** `Story`

---

## Title

```
Management dashboards
```

---

## Description

```
Role: Executive / Manager
As a manager or executive, I want a consolidated dashboard of key support metrics, so that I can make informed decisions at a glance.
```

---

## Acceptance criteria

```
- The dashboard combines ticket volume, SLA compliance, CSAT, and agent performance in one view.
- Widgets can be customized or rearranged per user role.
- The dashboard can be filtered by department, branch, or date range.
- Dashboard data refreshes automatically or on demand without a full page reload.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Stories 40–43 (RM-1..4) — this story composes their services, adding no new metric calculations of its own.
- **Depends on code areas or other stories:** none beyond the four above.

## Extra notes (optional)

- `ManagementDashboardService` is a thin composition over `TicketReportService`, `SlaComplianceService`, `AgentPerformanceService`, and `CsatReportService` — one call fans out to all four and assembles the result; no metric is computed twice, and a change to any one report's calculation automatically flows into the dashboard with zero dashboard-side code changes.
- **"Widgets can be customized/rearranged per user role"** — this codebase has no per-user account or persisted-preference system anywhere (agents/customers are both client-tracked-identity stand-ins, not real accounts — see `AgentContextService`/`CustomerContextService`). Building server-side per-user layout persistence would be new infrastructure well beyond this story's ask. Layout (which widgets, what order) is stored **client-side only**, in `localStorage`, the same "no server persistence for a per-browser preference" convention `AgentContextService` already established — flagged as the deliberate, consistent choice, not a shortcut.
- "Refreshes automatically or on demand" is short-interval polling plus a manual refresh button — the same pattern Customer Portal CP-2's ticket list and Agent Dashboard's own poll-based reload already use; no WebSockets/SignalR introduced.
- Filter by department/branch/date reuses RM-1's `Branch`/date-range query shape directly — one filter vocabulary across every RM report, not a dashboard-specific variant.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#` / .NET 10.
- `src/SupportCrm.Application/Tickets/AgentDashboardService.cs` — naming precedent only (`*DashboardService`); no code reused from it (different bounded concern).

## Out of scope

- Per-user server-side layout persistence (see above — client-side only, by design).
- Any new metric not already produced by RM-1..4.
