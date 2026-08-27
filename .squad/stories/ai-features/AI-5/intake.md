# Story intake

- Folder: `.squad/stories/ai-features/AI-5/intake.md`

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `AI-5`
- **Work item type:** `Story`

---

## Title

```
AI chatbot
```

---

## Description

```
Role: Customer
As a customer, I want to interact with an AI chatbot for common questions, so that I can get instant answers without waiting for an agent.
```

---

## Acceptance criteria

```
- The chatbot answers common questions using the knowledge base, in Arabic or English.
- The chatbot can create a ticket on behalf of the customer when it cannot resolve the issue.
- The customer can request a human agent at any point in the conversation.
- Chatbot conversations are logged and viewable by agents if escalated.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Communication Channels CC-3 (`ChatSession`/`ChatMessage`, reused directly rather than a parallel schema), Knowledge Base KB-4 (`KbSearchService`), AI-2 (`AiLanguageDetector`).
- **Depends on code areas or other stories:** backend CC-3 (`ChatService`, `IChatRepository`), `TicketIngestionService` (ticket-on-behalf-of-customer creation), KB-4.

## Extra notes (optional)

- **Reuses Communication Channels CC-3's `ChatSession`/`ChatMessage` tables directly, does not introduce a parallel bot-conversation schema.** This is what makes "conversations are logged and viewable by agents if escalated" true for free — an escalated bot session *is* a `ChatSession` row an agent can already read via CC-3's existing `GET /api/chat-sessions/{id}/messages`/`/status`. Add a `ChatSessionMode` (`Bot` | `Human`) to `ChatSession`, defaulting to `Human` via an optional constructor parameter so CC-3's existing `ChatService.StartAsync` call site needs zero changes.
- **A new, separate `api/chatbot` route surface** (not `api/chat-sessions`) keeps the bot-specific flow (start, send-message-get-bot-reply, request-human, create-ticket) distinct from CC-3's human-queue flow at the API level, even though both operate on the same domain tables underneath.
- **"Answers using the knowledge base"** — grounds every bot reply in `KbSearchService.SearchAsync(customerMessage, take: 3, ct)` (Knowledge Base KB-4), same reuse as AI-2's suggested replies. The mock `IAiChatbotProvider` assembles a templated answer from the top result(s) if any are found above a nominal relevance bar, or a "I couldn't find an answer, want a human agent or a ticket?" fallback otherwise — not a real conversational model.
- **Arabic/English** — reuses AI-2's `AiLanguageDetector` static helper unchanged (same Arabic-Unicode-range heuristic).
- **"Request a human agent at any point"** transitions the session's `Mode` from `Bot` to `Human` and reuses CC-3's own "FIFO to any available agent, else queued" assignment logic (extracted from `ChatService.StartAsync` into a small shared private helper both call, rather than duplicated).
- **"Create a ticket on behalf of the customer"** reuses the exact transcript-fold-into-one-inbound-event pattern `ChatService.CompleteAsync` already uses via `TicketIngestionService.IngestInboundMessageAsync` — extracted into a shared `ChatTranscriptFormatter` static helper so both call sites build the "Customer: ...\nAgent: ..." transcript identically, not two copies of the same logic.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- Extend `src/SupportCrm.Domain/Entities/ChatSession.cs` (`ChatSessionMode` enum, `Mode` property, a `RequestHuman(...)` method) and `src/SupportCrm.Domain/Repositories/IChatRepository.cs`/`ChatRepository.cs` only if a new query is needed (none anticipated — every existing method already works by `ChatSession`/`ChatMessage` id, mode-agnostic).
- New: `src/SupportCrm.Application/Ai/AiChatbotService.cs`, `IAiChatbotProvider.cs`, `MockAiChatbotProvider.cs`, `src/SupportCrm.Api/Controllers/ChatbotController.cs`.
- `src/SupportCrm.Application/Tickets/ChatService.cs`'s `StartAsync` (agent-assignment) and `CompleteAsync` (transcript fold) are the two blocks of logic this story extracts into shared helpers rather than copy-pasting.

## Out of scope

- Ticket summaries (AI-1), suggested replies (AI-2), automatic categorization (AI-3), suggested solutions (AI-4) — done.
- A real conversational/generative model — the mock provider, grounded in KB-4 search, is the full and final scope of "AI" here.
- Voice/multimodal input — text chat only, matching CC-3's own scope.
