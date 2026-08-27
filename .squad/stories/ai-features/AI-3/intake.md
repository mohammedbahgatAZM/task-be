# Story intake

- Folder: `.squad/stories/ai-features/AI-3/intake.md`

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `AI-3`
- **Work item type:** `Story`

---

## Title

```
Automatic categorization
```

---

## Description

```
Role: System
As a support manager, I want tickets to be automatically categorized by AI, so that they are routed correctly without manual tagging.
```

---

## Acceptance criteria

```
- New tickets are automatically assigned a category and priority suggestion on creation.
- An agent can override the AI-assigned category, and the correction is logged.
- Categorization accuracy can be reviewed in a report over time.
- Confidence below a configurable threshold routes the ticket for manual categorization instead.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Story AI-1 (`AiFeaturesOptions`, extended with this story's confidence threshold), Ticket Management TM-2 (`TicketCategory`, `Ticket.SetCategory`/`SetPriority`, `TicketFieldChangeEntry`).
- **Depends on code areas or other stories:** backend TM-1 (`TicketService.CreateAsync` — the hook point), TM-2's existing `PUT /api/tickets/{id}/category` (reused unchanged for the override AC).

## Extra notes (optional)

- **"An agent can override... and the correction is logged" needs zero new backend work** — Ticket Management TM-2's `PUT /api/tickets/{id}/category` already writes a `TicketFieldChangeEntry` on every category change, `ChangedBy` and all. This story only needs to make sure its own automatic categorization writes `ChangedBy: "AI"` on the entry it creates at ticket-creation time, so the existing field-history view already distinguishes AI-applied from human-corrected categories with no new column.
- **Mock provider, not real ML**: `IAiCategorizationProvider` matches naively — keyword-overlap scoring between the ticket subject/body and each active `TicketCategory`'s name, plus a small fixed keyword list for priority hints (e.g. "urgent"/"down"/"asap" → higher priority). The resulting "confidence" is a normalized overlap score (0–100), not a real classifier's calibrated probability — flag this explicitly in the provider's doc comment.
- **Applying the suggestion** happens inside `TicketService.CreateAsync`, in the same unit of work as ticket creation (no second `SaveChangesAsync` round-trip) — call the categorization service, and if `ConfidencePercentage >= AiFeaturesOptions.CategorizationConfidenceThresholdPercentage` and a category was matched, set it directly on the in-memory `Ticket` plus append the `TicketFieldChangeEntry`/priority-change entries before the single save.
- **Below-threshold tickets** are simply left uncategorized (`CategoryId == null`) — "routed for manual categorization" is satisfied by that null state being discoverable, not a new workflow queue: a `GET /api/ai/categorization/pending` lists tickets with a recorded suggestion but no applied category, for a manager view.
- **Accuracy report**: compares each `TicketCategorizationSuggestion.SuggestedCategoryId` to that same ticket's *current* `CategoryId` — a match means the AI's original pick was never overridden (proxy for "correct"); a mismatch means an agent corrected it. Grouped by day. This is an honest proxy, not ground truth (a ticket nobody ever reviewed could be silently wrong) — flag that limitation explicitly rather than presenting the number as certain.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Continue the `Ai` bounded concern: `src/SupportCrm.Domain/Entities/TicketCategorizationSuggestion.cs`, `src/SupportCrm.Application/Ai/TicketCategorizationService.cs`, `IAiCategorizationProvider.cs`, `MockAiCategorizationProvider.cs`, new `src/SupportCrm.Api/Controllers/AiController.cs` for the cross-ticket report/pending-list endpoints (these aren't actions *on* one ticket, so they don't belong on `TicketsController`).

## Out of scope

- Ticket summaries (AI-1, done), suggested replies (AI-2, done), suggested solutions (AI-4), and the AI chatbot (AI-5) — each is its own story.
- A real, trainable/tunable classifier — the mock's keyword-overlap heuristic is the full and final scope of "AI" here.
