# sla-automation — plan overview

Entry point for the **sla-automation** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 21 | [21-story-SA-1.md](21-story-SA-1.md) | Response and resolution targets | SA-1 | Ticket Management Stories 05–08 |
| 22 | [22-story-SA-2.md](22-story-SA-2.md) | Automatic assignment | SA-2 | Story 21, Ticket Management Story 07 |
| 23 | [23-story-SA-3.md](23-story-SA-3.md) | Escalation rules | SA-3 | Story 21, Story 22, Ticket Management Story 08 |
| 24 | [24-story-SA-4.md](24-story-SA-4.md) | Alerts and notifications | SA-4 | Story 21, Story 23 |

## Dependency notes

- Story 21 is foundational for this feature: it replaces the fixed-window `SlaPolicy` static helper introduced by Agent Dashboard's Story 16 (`AD-1`) with real, configurable SLA targets (per priority/category/tier) and a business-hours/holiday-aware calculation, while preserving the `AgentDashboardTicketDto.SlaDueAtUtc`/`SlaState` contract Story 16 already shipped. Stories 22–24 all depend on Story 21's `SlaCalculationService`/`SlaTarget` resolution.
- Story 22 (auto-assignment) extends Story 07's (Ticket Management) `TicketAssignmentService` with a rule engine — it does not replace manual assignment.
- Story 23 (escalation rules) is the automatic counterpart to Story 08's (Ticket Management, `TM-4`) manual one-action escalation, explicitly deferred there ("SLA timers / automatic escalation rules — this story is a manual, one-action escalation only"). It reuses Story 21's breach calculation and Story 22/Ticket-Management-07's reassignment.
- Story 24 (alerts) delivers in-app alerts for real (via the existing `AgentNotificationService` from Agent Dashboard Story 20, `AD-5`) and stubs email/push behind a notifier seam, following the same no-op-seam pattern already used by `IAssignmentNotifier` (Story 07) and `ICustomerStatusNotifier` (Story 08).
- No background-job/scheduler infrastructure exists anywhere in this codebase. Stories 23 and 24 each need a periodic check (escalation-tier evaluation; digest generation) — Story 23 introduces the one recurring hosted-service mechanism; Story 24 reuses it rather than adding a second one.
