# Story intake

- Folder: `.squad/stories/reports-management/RM-3/intake.md`

---

## Feature

- **Feature name (display):** Reports & Management
- **Feature slug (folder under `plans/`):** `reports-management`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `RM-3`
- **Work item type:** `Story`

---

## Title

```
Agent performance
```

---

## Description

```
Role: Support Manager
As a support manager, I want agent performance metrics such as tickets resolved, response time, and CSAT, so that I can manage team performance fairly.
```

---

## Acceptance criteria

```
- A per-agent report shows tickets handled, average response/resolution time, and CSAT score.
- Metrics can be compared across agents and over selected time periods.
- Agents can view their own performance metrics from their dashboard.
- Metrics exclude tickets reassigned away from the agent before resolution, or clearly flag them.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Story 40 (RM-1) — `Reports` bounded concern.
- **Depends on code areas or other stories:** `TicketAssignmentChangeEntry` (Ticket Management TM-3), `TicketFeedback` (Customer Portal CP-5) — the only CSAT data source that exists.

## Extra notes (optional)

- "Tickets handled" is scoped to a ticket's **current** `AssignedAgentId` — a ticket reassigned away from Agent A to Agent B before resolution is credited to B, not A, purely because it's no longer assigned to A by the time it resolves. This single rule already satisfies the AC's "exclude tickets reassigned away… before resolution" with no extra bookkeeping: a reassigned-away ticket simply isn't in A's current-assignment set any more.
- A separate, explicitly-surfaced `ReassignedAwayCount` per agent additionally answers "how many tickets did I work on that finished under someone else" — built from `TicketAssignmentChangeEntry` rows where the agent appears as `OldAgentId`/`NewAgentId` but the ticket's *current* `AssignedAgentId` is someone else and the ticket is Resolved/Closed. This is transparency, not a metric correction — it's reported alongside `TicketsResolvedCount`, never merged into it.
- Known, flagged limitation: a ticket that ping-pongs A → B → A and resolves under A credits A fully even if B did some of the work — not solved here; flagged in the plan's Edge Cases, not silently accepted as correct.
- Response/resolution time here use **raw wall-clock elapsed minutes**, not the business-hours-adjusted math SLA & Automation uses for due-dates (RM-2 does use that adjustment, because a pass/fail breach determination has real stakes tied to a configured target; a plain "how long did this typically take" average doesn't carry the same stakes, so the simpler calculation is intentional, not an inconsistency with RM-2).
- "Agents view their own metrics from their dashboard" needs **no new backend endpoint** — the same `GET /api/reports/agent-performance?agentId=...` this story adds is called by the frontend with the logged-in agent's own id (via the existing `AgentContextService`), same shape a manager would use with no filter.
- CSAT per agent joins `TicketFeedback` to tickets by the ticket's **current** `AssignedAgentId`, same ownership rule as "tickets handled" — consistent with itself rather than using a different attribution rule per metric.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#` / .NET 10.
- `src/SupportCrm.Application/Tickets/AgentDashboardService.cs` — closest existing precedent for per-agent ticket queries; this story's service is a new sibling in `Reports`, not an extension of that file (different bounded concern: workload display vs. performance analytics).

## Out of scope

- CSAT segmented/trend reporting beyond the per-agent average (that's RM-4's own, richer concern).
- Consolidated dashboard (RM-5).
