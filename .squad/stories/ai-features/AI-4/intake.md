# Story intake

- Folder: `.squad/stories/ai-features/AI-4/intake.md`

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `AI-4`
- **Work item type:** `Story`

---

## Title

```
Suggested solutions
```

---

## Description

```
Role: Support Agent
As a support agent, I want the AI to suggest relevant knowledge base solutions based on ticket content, so that I can resolve issues faster.
```

---

## Acceptance criteria

```
- The system surfaces the top matching articles/solutions alongside the open ticket.
- Suggestions update as the conversation develops with new information.
- An agent can insert a suggested solution into the reply with one click.
- Agents can flag an irrelevant suggestion to improve future matching.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Knowledge Base KB-4 (`KbSearchService`).
- **Depends on code areas or other stories:** backend KB-4, Ticket Management TM-5 (`ITicketMessageRepository`).

## Extra notes (optional)

- **This story is not a new AI call — it's KB-4's existing relevance-ranked search, reused.** Query text is built from the ticket subject plus its messages; results are Knowledge Base's already-ranked `Article`/`Guide` matches (FAQs excluded — "solutions" maps to Articles/Guides in this codebase's own terminology, not FAQs). Calling this "AI-powered" is honest only insofar as KB-4's own matching (trigram similarity, if available) is itself already a lightweight-ML technique — no new model is introduced here.
- **"Suggestions update as the conversation develops"** needs no caching/diffing logic — every call re-runs the search against the ticket's *current* messages; the frontend just re-fetches after each new message, same as it already does for other per-ticket panels.
- **"Insert with one click"** is entirely a frontend concern (copy title/link into the reply composer) — no backend endpoint needed beyond the suggestions list itself.
- **"Flag an irrelevant suggestion"** is logged (`SolutionSuggestionFeedback`) but explicitly does **not** feed back into ranking in this story — flag that clearly as a stand-in for a future relevance-tuning pass, not a working feedback loop, matching this codebase's convention of being honest about what a "stand-in" seam does and doesn't do yet.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- New file: `src/SupportCrm.Application/Ai/TicketSolutionSuggestionService.cs`, `src/SupportCrm.Domain/Entities/SolutionSuggestionFeedback.cs`. Endpoints on the existing `TicketsController` (an action on one ticket).
- Reuses `KbSearchService.SearchAsync` (Knowledge Base Story 28, `src/SupportCrm.Application/KnowledgeBase/KbSearchService.cs`) directly — do not duplicate its matching logic.

## Out of scope

- Ticket summaries (AI-1), suggested replies (AI-2), automatic categorization (AI-3) — done. AI chatbot (AI-5) is its own story.
- Actually improving future matching from flagged feedback — logged only, per the note above.
