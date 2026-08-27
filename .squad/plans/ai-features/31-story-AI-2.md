# Story 31 — Suggested replies (Story: AI-2)

---

## Prerequisites

- Story 30 completed: [`30-story-AI-1.md`](30-story-AI-1.md) — the `Ai` bounded concern, mock-provider pattern.
- Knowledge Base Story 28 completed ([`../knowledge-base/28-story-KB-4.md`](../knowledge-base/28-story-KB-4.md)) — `KbSearchService.SearchAsync`, reused for grounding.

---

## Story Goal

1. `POST /api/tickets/{id}/ai-reply-draft` returns a draft reply string plus its detected language — grounded in the ticket's latest customer message and Knowledge Base search results.
2. No new "send"/"accept" endpoint — an agent pastes/edits the draft into the existing reply composer and sends via Communication Channels CC-6's `POST /api/tickets/{id}/reply`, which is untouched.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/KnowledgeBase/KbSearchService.cs`, `SearchAsync`'s signature (Knowledge Base Story 28) — called directly, not reimplemented.
2. `src/SupportCrm.Application/Ai/MockAiSummaryProvider.cs` (Story 30) — the extractive/template, no-network-call precedent this story's `MockAiReplyDraftProvider` follows.
3. `src/SupportCrm.Domain/Entities/TicketMessage.cs`, `AuthorKind` — used to find "the latest customer message" this story grounds on.

---

## Backend Tasks

### 1 — Domain: none

No new entities — drafts are ephemeral, never persisted, per the intake's explicit note.

### 2 — Application: `AiLanguageDetector`, `IAiReplyDraftProvider`, `MockAiReplyDraftProvider`, `AiReplyDraftService`

**Create file: `src/SupportCrm.Application/Ai/AiLanguageDetector.cs`**

```csharp
namespace SupportCrm.Application.Ai;

// A simple Arabic-Unicode-range heuristic, not a language-ID model. Shared by this story and
// Story 34's chatbot — do not duplicate this character-range check anywhere else.
public static class AiLanguageDetector
{
    public static string Detect(string text) =>
        text.Any(c => c is >= '\u0600' and <= '\u06FF') ? "ar" : "en";
}
```

**File: `src/SupportCrm.Application/Ai/AiDtos.cs`** — append:

```csharp
public record AiReplyDraftDto(string DraftText, string DetectedLanguage);
```

**Create file: `src/SupportCrm.Application/Ai/IAiReplyDraftProvider.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using SupportCrm.Application.KnowledgeBase;

/// <summary>
/// Drafts a reply. No real LLM exists in this codebase — register
/// <see cref="MockAiReplyDraftProvider"/> until one does. Template-based, not generative.
/// </summary>
public interface IAiReplyDraftProvider
{
    string Draft(string latestCustomerMessage, IReadOnlyList<KbSearchResultDto> groundingResults, string language);
}
```

**Create file: `src/SupportCrm.Application/Ai/MockAiReplyDraftProvider.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using SupportCrm.Application.KnowledgeBase;

public class MockAiReplyDraftProvider : IAiReplyDraftProvider
{
    public string Draft(string latestCustomerMessage, IReadOnlyList<KbSearchResultDto> groundingResults, string language)
    {
        var top = groundingResults.FirstOrDefault();
        if (top is null)
        {
            return language == "ar"
                ? "شكرًا لتواصلك معنا. نحن ننظر في مشكلتك وسنرد عليك في أقرب وقت ممكن."
                : "Thank you for reaching out. We're looking into your issue and will get back to you shortly.";
        }

        return language == "ar"
            ? $"شكرًا لتواصلك معنا. بناءً على قاعدة المعرفة لدينا، إليك ما وجدناه بخصوص \"{top.Title}\": {top.Snippet} نأمل أن يساعدك هذا؛ أخبرنا إذا كنت بحاجة إلى مزيد من المساعدة."
            : $"Thank you for reaching out. Based on our knowledge base, here's what we found regarding \"{top.Title}\": {top.Snippet} We hope this helps — let us know if you need further assistance.";
    }
}
```

**Create file: `src/SupportCrm.Application/Ai/AiReplyDraftService.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;
using SupportCrm.Application.KnowledgeBase;

public class AiReplyDraftService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    KbSearchService kbSearchService,
    IAiReplyDraftProvider draftProvider)
{
    public async Task<AiReplyDraftDto> DraftAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundForAiException(ticketId.ToString());
        var messages = await messageRepository.GetMessagesAsync(ticketId, ct);

        var latestCustomerMessage = messages
            .Where(m => m.AuthorKind == "Customer")
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => m.Body)
            .FirstOrDefault() ?? ticket.Description ?? ticket.Subject;

        var language = AiLanguageDetector.Detect(latestCustomerMessage);
        var grounding = await kbSearchService.SearchAsync(latestCustomerMessage, take: 3, ct);
        var draftText = draftProvider.Draft(latestCustomerMessage, grounding.Results, language);

        return new AiReplyDraftDto(draftText, language);
    }
}
```

### 3 — Infrastructure: DI

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<IAiReplyDraftProvider, MockAiReplyDraftProvider>();
        services.AddScoped<AiReplyDraftService>();
```

### 4 — Api: `TicketsController` addition

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add:

```csharp

    [HttpPost("{id:guid}/ai-reply-draft")]
    public async Task<ActionResult<AiReplyDraftDto>> GetAiReplyDraft(Guid id, [FromServices] AiReplyDraftService draftService, CancellationToken ct)
    {
        try { return await draftService.DraftAsync(id, ct); }
        catch (TicketNotFoundForAiException) { return NotFound(); }
    }
```

---

## Edge Cases & Failure Modes

- **Ticket with no customer messages yet** — falls back to `ticket.Description ?? ticket.Subject` for both the grounding query and the language-detection input, same fallback chain as Story 30's summary.
- **Zero Knowledge Base results** — `MockAiReplyDraftProvider.Draft` returns the generic acknowledgment template, in the detected language, rather than an empty/error draft.
- **Arabic message with no Arabic content in the matched KB article** (article only has English fields) — `top.Title`/`top.Snippet` are whatever `KbSearchService` picked (its own `PickField` already prefers whichever language actually matched); the reply template's *surrounding* text is still Arabic even if the cited snippet itself is English — flagged as an acceptable mixed-language result, not a defect, since the underlying content genuinely is only in one language.
- **Unknown ticket id** — `TicketNotFoundForAiException` → `404`.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Ai/AiLanguageDetectorTests.cs`**:
   - `Detect_ArabicText_ReturnsAr`
   - `Detect_EnglishText_ReturnsEn`
2. **Unit — `tests/SupportCrm.Application.Tests/Ai/AiReplyDraftServiceTests.cs`**:
   - `DraftAsync_NoGroundingResults_ReturnsGenericTemplate`
   - `DraftAsync_UsesLatestCustomerMessageNotFirst`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Regression:** confirm `POST /api/tickets/{id}/reply` (Communication Channels CC-6) is unmodified — this story only adds a new endpoint, never touches the send path.

---

## Done Criteria

- [ ] `POST /api/tickets/{id}/ai-reply-draft` returns a KB-grounded draft + detected language.
- [ ] No new send/accept endpoint exists — sending still goes through the existing reply endpoint unmodified.
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 32.**
