# agent-dashboard — plan overview

Entry point for the **agent-dashboard** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 16 | [16-story-AD-1.md](16-story-AD-1.md) | Assigned tickets | AD-1 | Ticket Management Stories 05–09 |
| 17 | [17-story-AD-2.md](17-story-AD-2.md) | Customer information | AD-2 | Story 16, Customer Management Stories 01–03 |
| 18 | [18-story-AD-3.md](18-story-AD-3.md) | Tasks and reminders | AD-3 | Story 16 |
| 19 | [19-story-AD-4.md](19-story-AD-4.md) | Quick replies | AD-4 | Communication Channels Story 15 |
| 20 | [20-story-AD-5.md](20-story-AD-5.md) | Team collaboration | AD-5 | Story 18 |

## Dependency notes

- Story 16 is foundational for this feature: it introduces the "acting as" agent-identity mechanism (an explicit request parameter, not real auth) and the SLA-state computation every other agent-facing screen in this feature reuses or displays.
- **Explicit, team-approved scope decisions across this entire feature** (see AD-1's intake for the full rationale):
  - **No authentication exists and this feature does not add one.** "The logged-in agent" is passed explicitly as a request parameter (`agentId`/`requestingAgentId`), mirroring how `changedBy`/`authorName` are already passed explicitly everywhere else in this app. A real login system is a future feature.
  - **SLA due-at is derived, not stored:** `CreatedAtUtc + window(Priority)` (Urgent 4h / High 8h / Medium 24h / Low 72h). No new column, no migration for the deadline itself.
  - **"Near real time" and "reminder/notification" are both polling-based**, consistent with Communication Channels CC-3's live-chat decision — no WebSockets/SignalR, no background job scheduler, no real push/email/SMS. Story 18's `AgentNotification` mechanism computes due-task notifications lazily on each poll and is reused as-is by Story 20's @-mentions — one shared mechanism, not two.
  - **Permissions are two narrowly-scoped booleans, not a roles system:** `Agent.CanViewSensitiveData` (Story 17) and — explicitly **not** added — a gate on Story 19's template admin (documented as a deliberate scope decision in that story's own plan, consistent with Communication Channels CC-5's ungated web-form field admin).
- Story 16 also introduces the first frontend-facing Agent CRUD/admin surface in the app (an "Acting as" switcher + a minimal agent list); Stories 17 and 19 each add one more toggle column to that same list rather than building separate admin screens.
