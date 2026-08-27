# Story intake

- Folder: `.squad/stories/agent-dashboard/AD-1/intake.md`

---

## Feature

- **Feature name (display):** Agent Dashboard
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `AD-1`
- **Work item type:** `Story`

---

## Title

```
Assigned tickets
```

---

## Description

```
Role: Support Agent
As a support agent, I want a dashboard of tickets assigned to me, so that I can prioritize and manage my workload.
```

---

## Acceptance criteria

```
- The dashboard lists all tickets assigned to the logged-in agent, sorted by priority/due date by default.
- Tickets nearing or past their SLA are visually highlighted.
- The agent can filter the list by status, priority, and category.
- The dashboard updates in near real time as new tickets are assigned.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Ticket Management TM-2 (`TicketCategory`, `TicketPriority`), TM-3 (`Agent`, assignment), TM-4 (`TicketStatus`).
- **Depends on code areas or other stories:** backend TM-2/TM-3/TM-4 (`Ticket.AssignedAgentId`, `Ticket.Priority`, `Ticket.Status`, `Ticket.CategoryId`), the existing `GET/POST /api/agents` endpoints (TM-3).

## Extra notes (optional)

- **Team decision — "logged-in agent":** the app has no authentication system. This story introduces a frontend-only "Acting as: [Agent ▼]" switcher, backed by the existing `Agent` entity, persisted in the browser. Every Agent Dashboard endpoint that needs "who is asking" takes the agent id as an explicit request parameter (the same pattern already used for `changedBy`/`authorName` everywhere else in this app) — there is no session/cookie/JWT. Real authentication is a future feature, not something this story blocks on or fakes convincingly; it's a clearly-labeled stand-in.
- **Team decision — SLA rule:** no SLA concept exists yet. SLA due-at is *derived*, not stored: `CreatedAtUtc + window(Priority)`, where window = Urgent 4h, High 8h, Medium 24h, Low 72h. "Nearing" = remaining time ≤ 20% of the total window (and not yet breached). "Breached" = now past due-at. Only meaningful for tickets not already `Closed`. This needs no new column/migration for the deadline itself — it's computed from `CreatedAtUtc` + `Priority`, both of which already exist.
- **Team decision — "near real time":** polling, not WebSockets/SignalR, consistent with Communication Channels CC-3's live-chat decision — the dashboard re-polls every few seconds while the page is open.
- **Default scope of the list:** excludes `Closed` tickets by default (an agent's *workload* dashboard, not a full history); the status filter can still select `Closed` explicitly to see them.
- This story is also where the `Agent` entity becomes a first-class frontend concept for the first time (previously only a dropdown value in ticket assignment) — the agent switcher introduced here is the attachment point later stories (AD-2's permission flag, AD-4's permission flag) extend with a minimal agent-admin list, rather than each building a separate screen.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Builds on `Ticket` (TM-1..TM-4), `Agent`/`AgentService` (TM-3).
- Sort: priority descending (Urgent → Low), then SLA due-at ascending, by default.

## Out of scope

- Real authentication/authorization (session, JWT, login screen).
- Push notifications / WebSockets for "near real time" — polling only.
- SLA policy configuration UI (the priority→window mapping is a fixed constant for now).
