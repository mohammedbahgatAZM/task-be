# Story intake

- Folder: `.squad/stories/customer-portal/CP-5/intake.md`

---

## Feature

- **Feature name (display):** Customer Portal
- **Feature slug (folder under `plans/`):** `customer-portal`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `CP-5`
- **Work item type:** `Story`

---

## Title

```
Submit feedback
```

---

## Description

```
Role: Customer
As a customer, I want to submit feedback or a rating after my ticket is resolved, so that I can share my experience with the support team.
```

---

## Acceptance criteria

```
- The customer is prompted for a satisfaction rating (e.g. CSAT/star rating) when a ticket is marked resolved.
- The customer can optionally add a text comment with their rating.
- Feedback is linked to the specific ticket and visible to the assigned agent and manager.
- Low ratings can automatically trigger a follow-up task for a supervisor.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** SLA & Automation Story 23 (`Agent.IsSupervisor`, the follow-up task's assignee pool), Agent Dashboard Story 18 (`TicketTask`, reused directly for the follow-up — not a new entity).
- **Depends on code areas or other stories:** backend `IAgentRepository`, `ITicketTaskRepository`/`TicketTaskService` (Agent Dashboard AD-3).

## Extra notes (optional)

- **"Prompted... when a ticket is marked resolved"** is a frontend-only decision (gate the prompt on `ticket.status === 'Resolved'` and "no feedback submitted yet") — the backend just needs a way to create feedback and to say whether it already exists; it doesn't need to know about "prompting" as a concept.
- **New entity `TicketFeedback`** (`TicketId` unique, `Rating` 1–5, `Comment` nullable, `SubmittedAtUtc`) — one per ticket (a ticket only gets marked resolved-and-rated once in this flow; re-submission is rejected, not overwritten, so a customer can't silently erase a low rating by resubmitting).
- **"Visible to the assigned agent and manager"** — no real RBAC exists anywhere in this codebase; `GET /api/tickets/{id}/feedback` is unrestricted, same as every other ticket-scoped read endpoint. Whoever can already see the ticket can see its feedback.
- **"Low ratings automatically trigger a follow-up task for a supervisor"** reuses Agent Dashboard AD-3's `TicketTask` entity directly rather than inventing a parallel task type — on submission, if `Rating <= CustomerPortalOptions.LowRatingThreshold` (default 2, added to the same options class Story CP-3 introduced), find agents where `IsSupervisor == true` (SA-3's flag, `IAgentRepository.GetAllAsync` filtered in-memory — small agent count, same pattern already used elsewhere e.g. `EscalationRuleEngine`) and create **one** `TicketTask` assigned to the first supervisor found (a task needs one clear owner — notifying *every* supervisor, the way SLA & Automation's escalation tiers do, is right for an alert but wrong for an assignable to-do; flagged as a deliberate difference from that story's "notify all" pattern), due `+1 day`, note `"Low CSAT rating ({rating}/5) on ticket {referenceNumber} — follow up."`
- **No supervisors configured** — the feedback still saves; the task creation is skipped, not retried/queued (flagged, not silently different from what it looks like — a support manager should staff at least one `IsSupervisor` agent for this to have any effect).

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- New: `src/SupportCrm.Domain/Entities/TicketFeedback.cs`, `src/SupportCrm.Application/CustomerPortal/TicketFeedbackService.cs`. Endpoints on the existing `TicketsController`.
- `src/SupportCrm.Application/Tickets/TicketTaskService.cs` (Agent Dashboard AD-3) and its `CreateAsync`/`CreateTicketTaskRequest` — reused directly, not reimplemented, for the follow-up task.

## Out of scope

- Submitting tickets (CP-1), tracking (CP-2), history (CP-3), FAQ integration (CP-4) — done.
- Editing/deleting submitted feedback — write-once per ticket.
- A dedicated CSAT/NPS reporting dashboard beyond what's visible per-ticket — out of scope unless trivial (not planned here).
