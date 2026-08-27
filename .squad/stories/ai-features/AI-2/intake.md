# Story intake

- Folder: `.squad/stories/ai-features/AI-2/intake.md`

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `AI-2`
- **Work item type:** `Story`

---

## Title

```
Suggested replies
```

---

## Description

```
Role: Support Agent
As a support agent, I want AI-suggested reply drafts, so that I can respond faster while keeping quality consistent.
```

---

## Acceptance criteria

```
- The system suggests a draft reply based on the ticket content and relevant knowledge base articles.
- The agent can edit, accept, or discard the suggestion before sending.
- Suggestions match the language of the customer's message (Arabic or English).
- No AI-suggested reply is sent to a customer without agent review and approval.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Story AI-1 (`Ai` bounded concern, mock-provider pattern), Knowledge Base KB-4 (`KbSearchService`, reused for grounding).
- **Depends on code areas or other stories:** backend KB-4 (`KbSearchService.SearchAsync`), Ticket Management TM-5 (`ITicketMessageRepository`), Communication Channels CC-6 (`POST /api/tickets/{id}/reply` — where an accepted draft is actually sent).

## Extra notes (optional)

- **"Edit, accept, or discard before sending" and "no reply sent without agent review" need zero new backend work** — this story returns a draft string and nothing else; the agent pastes/edits it into the existing reply composer and sends via the existing `POST /api/tickets/{id}/reply` (Communication Channels CC-6). There is no "accept" endpoint, because accepting *is* calling the reply endpoint that already exists — inventing a parallel one would be redundant and risk drifting out of sync with the real send path.
- **Grounding reuses Knowledge Base KB-4's search directly** — `KbSearchService.SearchAsync(latestCustomerMessage, take: 3, ct)` — not a second retrieval mechanism.
- **Language detection is a simple heuristic, not a language-ID model**: if the latest customer message contains any Arabic-range Unicode characters, treat it as Arabic; otherwise English. Extract this as a small reusable static helper (`AiLanguageDetector`) since Story AI-5's chatbot needs the identical check — do not duplicate the character-range logic.
- The mock draft provider (`IAiReplyDraftProvider`) is template-based, not generative: if grounding results exist, assemble a short "here's what I found" reply citing the top match's title/snippet in the detected language; otherwise a generic acknowledgment. Two fixed templates (en/ar), not a translation engine.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Continue the `Ai` bounded concern from Story AI-1: `src/SupportCrm.Application/Ai/AiReplyDraftService.cs`, `IAiReplyDraftProvider.cs`, `MockAiReplyDraftProvider.cs`. New endpoint on the existing `TicketsController`.

## Out of scope

- Ticket summaries (AI-1, done), automatic categorization (AI-3), suggested solutions (AI-4), and the AI chatbot (AI-5) — each is its own story.
- Persisting drafts — ephemeral, regenerated on request, never stored.
