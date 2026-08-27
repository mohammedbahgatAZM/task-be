# Story intake

- Folder: `.squad/stories/sla-automation/SA-3/intake.md`

---

## Feature

- **Feature name (display):** SLA & Automation
- **Feature slug (folder under `plans/`):** `sla-automation`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `SA-3`
- **Work item type:** `Story`
- **Status:** ``
- **Assignee:** ``
- **Labels:** ``

---

## Title

```
Escalation rules
```

---

## Description

```
Role: Support Manager
As a support manager, I want to configure automatic escalation rules for tickets at risk of breaching SLA, so that no ticket is missed.
```

---

## Acceptance criteria

```
- An escalation rule can be set to trigger at a configurable percentage of time-to-breach (e.g. 80%).
- Escalation can reassign the ticket, raise its priority, and/or notify a supervisor.
- Multiple escalation tiers can be configured for repeated breaches.
- All automatic escalations are logged with the rule that triggered them.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** SA-1 (SLA target + time-to-breach calculation), TM-3/SA-2 (assignment/reassignment), TM-4 (manual escalation action, `Ticket.MarkEscalated`/`LastEscalatedAtUtc`, `TicketPriority`).
- **Depends on code areas or other stories:** backend SA-1 (breach-time source), TM-3 `TicketAssignmentService` (reassignment), TM-4 escalation action and `Ticket.SetPriority`.

## Extra notes (optional)

- TM-4 explicitly deferred this: its intake's Out-of-scope says "SLA timers / automatic escalation rules — this story is a manual, one-action escalation only." This story is that deferred work — it builds automatic, time-driven escalation on top of TM-4's existing one-action `MarkEscalated`/reassign primitives, it does not replace them.
- Requires a periodic evaluation mechanism (no background job/scheduler infrastructure exists yet in this codebase). Add a minimal recurring check (e.g. a hosted service polling on an interval) as a stand-in for a real job scheduler (Hangfire/Quartz/etc.), flagged explicitly — do not introduce a new scheduling dependency unless trivial.
- "Notify a supervisor" — no supervisor/role concept exists on `Agent` (no manager hierarchy). Add a minimal `IsSupervisor` flag (or a `SupervisorAgentId` on `Team`) as a stand-in, flagged explicitly, and route the notification through SA-4's alerting seam (or TM-3/SA-2's `IAssignmentNotifier`-style seam if SA-4 isn't planned yet — coordinate the interface shape so SA-4 can implement it for real).
- "Multiple escalation tiers for repeated breaches" — model as an ordered list of tiers per SLA policy (each with its own trigger percentage, action set, and target), tracked per-ticket so a ticket doesn't re-trigger the same tier twice; define the exact "repeated breach" semantics in the plan (e.g. tier 2 triggers only after tier 1 has fired and the ticket is still open past tier 2's threshold).
- "Logged with the rule that triggered them" — persist an audit entry (rule id/tier, action taken, timestamp, resulting priority/assignee) rather than only emitting a log line, so it's queryable later (ties into TM-5's ticket-history view).

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- New entities/services under the `Sla` bounded concern started in SA-1: `src/SupportCrm.Domain/Entities/EscalationRule.cs`, `EscalationLogEntry.cs`, `src/SupportCrm.Application/Sla/EscalationEvaluationService.cs` (or similar), persistence under `src/SupportCrm.Infrastructure/Persistence/`.
- Reuse SA-1's time-to-breach calculation, TM-3's `TicketAssignmentService.AssignAsync` for the reassign action, and `Ticket.SetPriority`/`Ticket.MarkEscalated` for the priority-raise/escalate actions — do not duplicate this logic.

## Out of scope

- SLA target configuration and time-to-breach calculation itself (SA-1) — this story only reacts to it.
- Alert/notification delivery mechanics (SA-4) — this story decides *when* to escalate and logs it; SA-4 owns how a human is actually alerted, though a supervisor-notify action here may call into SA-4's seam once it exists.
- A general-purpose job scheduler — only the minimal recurring check this story needs.
