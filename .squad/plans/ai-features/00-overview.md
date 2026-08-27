# ai-features — plan overview

Entry point for the **ai-features** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 30 | [30-story-AI-1.md](30-story-AI-1.md) | Ticket summaries | AI-1 | Ticket Management TM-5 |
| 31 | [31-story-AI-2.md](31-story-AI-2.md) | Suggested replies | AI-2 | Story 30, Knowledge Base Story 28 |
| 32 | [32-story-AI-3.md](32-story-AI-3.md) | Automatic categorization | AI-3 | Story 30, Ticket Management TM-2 |
| 33 | [33-story-AI-4.md](33-story-AI-4.md) | Suggested solutions | AI-4 | Knowledge Base Story 28 |
| 34 | [34-story-AI-5.md](34-story-AI-5.md) | AI chatbot | AI-5 | Communication Channels CC-3, Story 31, Knowledge Base Story 28 |

## Dependency notes

- **No real LLM/AI provider exists anywhere in this codebase.** Every story here defines a narrow `IAiXxxProvider` seam with a heuristic, deterministic `MockAiXxxProvider` implementation (extractive summaries, keyword-overlap categorization, template-based drafts) — an explicit stand-in for a real OpenAI/Anthropic/etc. integration, following the exact "no real X yet, register a Mock" pattern this codebase already uses for `MockEmailSender`/`MockWhatsAppSender`/`MockSmsSender`. None of these mocks make network calls or claim real ML capability.
- Story 30 introduces the `Ai` bounded concern and `AiFeaturesOptions` (a single shared options class every later story's threshold/config lives on, not one class per story).
- Stories 31 and 34 both ground their AI output in Knowledge Base Story 28's `KbSearchService` — no second retrieval mechanism, and both share a `AiLanguageDetector` static helper (Arabic-Unicode-range heuristic) introduced in Story 31.
- Story 32's "agent can override, correction is logged" AC needs no new backend work — it reuses Ticket Management TM-2's existing `PUT /api/tickets/{id}/category`, which already writes a `TicketFieldChangeEntry`.
- Story 33 is not a new AI call at all — it's Story 28's search reused directly against ticket content, filtered to Article/Guide.
- Story 34 reuses Communication Channels CC-3's `ChatSession`/`ChatMessage` tables directly (adding a `Mode` flag) rather than a parallel bot-conversation schema, and factors two blocks of CC-3's `ChatService` logic (agent assignment, transcript-to-ticket folding) into shared helpers both stories call.
