# Story intake

- Folder: `.squad/stories/ai-features/AI-1/intake.md`

---

## Feature

- **Feature name (display):** AI Features
- **Feature slug (folder under `plans/`):** `ai-features`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `AI-1`
- **Work item type:** `Story`

---

## Title

```
Ticket summaries
```

---

## Description

```
Role: Support Agent
As a support agent, I want an AI-generated summary of a long ticket thread, so that I can get up to speed quickly.
```

---

## Acceptance criteria

```
- Opening a ticket with more than a configurable number of messages shows an AI summary option.
- The summary captures the customer's issue, key actions taken, and current status.
- The agent can regenerate the summary if new messages are added.
- The summary is clearly labeled as AI-generated and never overwrites the original thread.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** None — first, foundational story in this feature.
- **Depends on code areas or other stories:** backend `TicketMessage`/`TicketNote` (Ticket Management TM-5's `ITicketMessageRepository`), `Ticket` (TM-1..TM-4 for subject/category/priority/status).

## Extra notes (optional)

- **No real LLM/AI provider exists anywhere in this codebase.** Model an `IAiSummaryProvider` seam with a heuristic, deterministic `MockAiSummaryProvider` implementation — extractive, not generative (picks the first customer message, counts agent replies, states current status) — as an explicit stand-in for a real OpenAI/Anthropic/etc. integration, following the exact same "no real X exists yet, register a Mock/NoOp implementation" pattern already used throughout this codebase (`MockEmailSender`, `MockWhatsAppSender`, `MockSmsSender`, `NoOpAssignmentNotifier`). Flag this explicitly — do not claim real AI capability anywhere in code comments or DTOs.
- "A configurable number of messages" — a new `AiFeaturesOptions` (bound from an `"AiFeatures"` config section, same pattern as `AttachmentOptions`) with a `SummaryThresholdMessageCount` (default 5). This option section is shared by every AI-Features story that needs a threshold (AI-3 adds `CategorizationConfidenceThresholdPercentage` to the same class) — do not create a second options class per story.
- "Shows an AI summary option" is a frontend rendering decision based on a message count the backend exposes — this story adds a minimal `GET /api/tickets/{id}/message-count` endpoint rather than bloating the existing `TicketDto`.
- "Regenerate if new messages are added" needs no auto-detection logic server-side — the summary DTO returns both `sourceMessageCount` (how many messages existed when generated) and the ticket's current count is available via the endpoint above; the frontend compares the two to decide whether to hint "this may be stale."
- "Never overwrites the original thread" is true by construction — the summary lives in its own table, `TicketMessage` rows are never touched.
- One summary per ticket (upserted on regenerate), not a version history — the AC asks for "the summary," singular and current, not an audit trail (unlike Knowledge Base's `ContentVersionEntry`).

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#`.
- New bounded concern `Ai` — `src/SupportCrm.Domain/Entities/TicketAiSummary.cs`, `src/SupportCrm.Application/Ai/`, `src/SupportCrm.Infrastructure/Persistence/TicketAiSummaryRepository.cs`, endpoints added to the existing `TicketsController` (a summary is an action *on* a ticket, matching how SLA & Automation added `/sla-status` directly onto `TicketsController` rather than a new controller).
- `src/SupportCrm.Infrastructure/Storage/LocalDiskAttachmentStorage.cs`'s `LocalDiskAttachmentStorageOptions` (`SectionName` constant + `IOptions<T>` injection) is the exact precedent `AiFeaturesOptions` follows.

## Out of scope

- Suggested replies (AI-2), automatic categorization (AI-3), suggested solutions (AI-4), and the AI chatbot (AI-5) — each is its own story below.
- A real LLM API integration — the mock provider is the full and final scope of "AI" here.
