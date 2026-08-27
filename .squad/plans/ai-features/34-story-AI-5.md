# Story 34 — AI chatbot (Story: AI-5)

---

## Prerequisites

- Communication Channels Story 12 completed ([`../communication-channels/12-story-CC-3.md`](../communication-channels/12-story-CC-3.md)) — `ChatSession`/`ChatMessage`/`ChatService`/`IChatRepository`, reused directly.
- Story 31 completed: [`31-story-AI-2.md`](31-story-AI-2.md) — `AiLanguageDetector`.
- Knowledge Base Story 28 completed ([`../knowledge-base/28-story-KB-4.md`](../knowledge-base/28-story-KB-4.md)) — `KbSearchService.SearchAsync`.

---

## Story Goal

1. A new `ChatSessionMode` (`Bot` | `Human`) on the existing `ChatSession` — defaulting to `Human` via an optional constructor parameter, so Communication Channels CC-3's existing human-queue flow needs zero call-site changes.
2. A new `api/chatbot` route surface (start / send-message-get-bot-reply / request-human / create-ticket) operating on those same `ChatSession`/`ChatMessage` tables — an escalated bot conversation is just a `ChatSession` an agent can already read via CC-3's existing `GET /api/chat-sessions/{id}/messages`.
3. Bot replies are grounded in Knowledge Base Story 28's search, in the detected language (Story 31's `AiLanguageDetector`, reused unchanged).
4. Two blocks of `ChatService`'s existing logic (agent assignment, transcript-to-ticket folding) extracted into shared static helpers both this story and `ChatService` call — not duplicated.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Tickets/ChatService.cs` (all 84 lines, post SLA & Automation edits if any — none expected here) — `StartAsync`'s agent-assignment block (lines 19–23) and `CompleteAsync`'s transcript-fold block (lines 68–71) are the two blocks this story extracts into `ChatAgentAssignment`/`ChatTranscriptFormatter`.
2. `src/SupportCrm.Domain/Entities/ChatSession.cs` (all 49 lines) — the entity this story extends; `AssignAgent`/`Complete`/`SetTyping` are the existing transition-method precedent `RequestHuman` follows.
3. `src/SupportCrm.Application/Tickets/ChatDtos.cs` (all 13 lines) — `ChatSessionDto`, extended with `Mode`; `ChatSessionNotFoundException(Guid id)`, reused unchanged (thrown by this story's service too).
4. `src/SupportCrm.Application/Tickets/TicketIngestionService.cs` (all 39 lines) — `IngestInboundMessageAsync`, called identically to how `ChatService.CompleteAsync` already calls it.
5. `src/SupportCrm.Application/Ai/AiLanguageDetector.cs` (Story 31, ~6 lines) — reused verbatim, not reimplemented.

---

## Backend Tasks

### 1 — Domain: `ChatSessionMode`, `ChatSession` extension

**Create file: `src/SupportCrm.Domain/Entities/ChatSessionMode.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public enum ChatSessionMode
{
    Human,
    Bot
}
```

**File: `src/SupportCrm.Domain/Entities/ChatSession.cs`** — add a property after `Status`:

```csharp
    public ChatSessionMode Mode { get; private set; } = ChatSessionMode.Human;
```

Replace the constructor (lines 18–28) with:

```csharp
    public ChatSession(string customerName, string? customerContactValue, DateTimeOffset startedAtUtc, ChatSessionMode mode = ChatSessionMode.Human)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name is required.", nameof(customerName));

        Id = Guid.NewGuid();
        CustomerName = customerName;
        CustomerContactValue = customerContactValue;
        Mode = mode;
        // Bot sessions are immediately "active" (the bot is always available); human sessions
        // keep the existing Queued-until-assigned behavior, unchanged for CC-3's call site.
        Status = mode == ChatSessionMode.Bot ? ChatSessionStatus.Active : ChatSessionStatus.Queued;
        StartedAtUtc = startedAtUtc;
    }
```

Add a transition method after `SetTyping`:

```csharp

    public void RequestHuman(Guid? assignedAgentId)
    {
        Mode = ChatSessionMode.Human;
        if (assignedAgentId is not null)
        {
            AssignedAgentId = assignedAgentId;
            Status = ChatSessionStatus.Active;
        }
        else
        {
            Status = ChatSessionStatus.Queued;
        }
    }
```

### 2 — Application: shared helpers, `IAiChatbotProvider`, `MockAiChatbotProvider`, `AiChatbotService`

**Create file: `src/SupportCrm.Application/Tickets/ChatAgentAssignment.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

// Shared by ChatService.StartAsync (human-queue entry, Communication Channels CC-3) and
// AiChatbotService.RequestHumanAsync (bot-to-human escalation, AI Features AI-5) — one
// FIFO-to-any-available-agent policy, not two copies of it.
public static class ChatAgentAssignment
{
    public static Agent? PickAvailable(IReadOnlyList<Agent> agents) => agents.FirstOrDefault(a => a.IsAvailable);
}
```

**Create file: `src/SupportCrm.Application/Tickets/ChatTranscriptFormatter.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

// Shared by ChatService.CompleteAsync (human chat -> ticket) and AiChatbotService's
// escalate-to-ticket action — one way to fold a chat transcript into a single inbound
// ticket-ingestion event. "Agent" labels any non-customer message, bot replies included —
// from the transcript's point of view, the bot spoke in the support side's voice.
public static class ChatTranscriptFormatter
{
    public static string Format(IReadOnlyList<ChatMessage> orderedMessages) =>
        orderedMessages.Count > 0
            ? string.Join("\n", orderedMessages.Select(m => $"{(m.IsFromCustomer ? "Customer" : "Agent")}: {m.Body}"))
            : "(no messages)";
}
```

**File: `src/SupportCrm.Application/Tickets/ChatService.cs`** — replace the agent-assignment block inside `StartAsync` (lines 19–23):

```csharp
        // FIFO-to-any-available-agent — no skill matching (no skills taxonomy exists).
        var agents = await agentRepository.GetAllAsync(ct);
        var availableAgent = ChatAgentAssignment.PickAvailable(agents);
        if (availableAgent is not null)
            session.AssignAgent(availableAgent.Id);
```

and replace `CompleteAsync`'s transcript-building block (lines 65–71):

```csharp
        var orderedMessages = messages.OrderBy(m => m.SentAtUtc).ToList();
        var transcript = ChatTranscriptFormatter.Format(orderedMessages);
```

**File: `src/SupportCrm.Application/Tickets/ChatDtos.cs`** — replace `ChatSessionDto`'s line:

```csharp
public record ChatSessionDto(Guid Id, ChatSessionStatus Status, ChatSessionMode Mode, Guid? AssignedAgentId, Guid? ResultingTicketId, DateTimeOffset StartedAtUtc);
```

(This adds one field — update `ChatService.ToDto`'s call site, `new(s.Id, s.Status, s.Mode, s.AssignedAgentId, s.ResultingTicketId, s.StartedAtUtc)`, to match.)

**File: `src/SupportCrm.Application/Ai/AiDtos.cs`** — append:

```csharp
public record StartChatbotRequest(string CustomerName, string? CustomerContactValue);
public record SendChatbotMessageRequest(string Body);
public record ChatbotReplyDto(string ResponseText, bool CanResolve, string DetectedLanguage);
public record AiChatbotAnswer(string ResponseText, bool CanResolve);
```

**Create file: `src/SupportCrm.Application/Ai/IAiChatbotProvider.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using SupportCrm.Application.KnowledgeBase;

/// <summary>
/// Answers a chatbot question. No real conversational model exists in this codebase —
/// register <see cref="MockAiChatbotProvider"/> until one does. Grounded entirely in
/// Knowledge Base search results, template-based, not generative.
/// </summary>
public interface IAiChatbotProvider
{
    AiChatbotAnswer Answer(string question, IReadOnlyList<KbSearchResultDto> groundingResults, string language);
}
```

**Create file: `src/SupportCrm.Application/Ai/MockAiChatbotProvider.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using SupportCrm.Application.KnowledgeBase;

public class MockAiChatbotProvider : IAiChatbotProvider
{
    public AiChatbotAnswer Answer(string question, IReadOnlyList<KbSearchResultDto> groundingResults, string language)
    {
        var top = groundingResults.FirstOrDefault();
        if (top is null)
        {
            return new AiChatbotAnswer(
                language == "ar"
                    ? "لم أجد إجابة مناسبة لسؤالك. هل ترغب في التحدث مع أحد الموظفين أو إنشاء تذكرة؟"
                    : "I couldn't find a good answer to that. Would you like to talk to a human agent, or should I create a ticket for you?",
                CanResolve: false);
        }

        return new AiChatbotAnswer(
            language == "ar"
                ? $"وجدت هذا بخصوص \"{top.Title}\": {top.Snippet} هل هذا يجيب على سؤالك؟"
                : $"I found this regarding \"{top.Title}\": {top.Snippet} Does this answer your question?",
            CanResolve: true);
    }
}
```

**Create file: `src/SupportCrm.Application/Ai/AiChatbotService.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;
using SupportCrm.Application.KnowledgeBase;

public class AiChatbotService(
    IChatRepository chatRepository,
    IAgentRepository agentRepository,
    TicketIngestionService ingestionService,
    KbSearchService kbSearchService,
    IAiChatbotProvider chatbotProvider,
    TimeProvider timeProvider)
{
    public async Task<ChatSessionDto> StartAsync(StartChatbotRequest request, CancellationToken ct)
    {
        var session = new ChatSession(request.CustomerName.Trim(), request.CustomerContactValue?.Trim(), timeProvider.GetUtcNow(), ChatSessionMode.Bot);
        await chatRepository.AddAsync(session, ct);
        await chatRepository.SaveChangesAsync(ct);
        return ToDto(session);
    }

    public async Task<ChatbotReplyDto> SendMessageAsync(Guid sessionId, SendChatbotMessageRequest request, CancellationToken ct)
    {
        var session = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        if (session.Mode != ChatSessionMode.Bot)
            throw new InvalidOperationException("This session has been escalated to a human agent — send further messages through the chat-sessions endpoint instead.");

        var body = request.Body.Trim();
        var customerMessage = new ChatMessage(sessionId, body, isFromCustomer: true, timeProvider.GetUtcNow());
        await chatRepository.AddMessageAsync(customerMessage, ct);

        var language = AiLanguageDetector.Detect(body);
        var grounding = await kbSearchService.SearchAsync(body, take: 3, ct);
        var answer = chatbotProvider.Answer(body, grounding.Results, language);

        var botMessage = new ChatMessage(sessionId, answer.ResponseText, isFromCustomer: false, timeProvider.GetUtcNow());
        await chatRepository.AddMessageAsync(botMessage, ct);
        await chatRepository.SaveChangesAsync(ct);

        return new ChatbotReplyDto(answer.ResponseText, answer.CanResolve, language);
    }

    public async Task<ChatSessionDto> RequestHumanAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        var agents = await agentRepository.GetAllAsync(ct);
        var availableAgent = ChatAgentAssignment.PickAvailable(agents);
        session.RequestHuman(availableAgent?.Id);
        await chatRepository.SaveChangesAsync(ct);
        return ToDto(session);
    }

    public async Task<Guid> CreateTicketAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        var messages = await chatRepository.GetMessagesAsync(sessionId, ct);
        var orderedMessages = messages.OrderBy(m => m.SentAtUtc).ToList();
        var transcript = ChatTranscriptFormatter.Format(orderedMessages);

        var ticket = await ingestionService.IngestInboundMessageAsync(
            new IngestInboundMessageRequest(TicketChannel.Chat, session.CustomerName, session.CustomerContactValue, "AI chatbot conversation", transcript), ct);

        var now = timeProvider.GetUtcNow();
        session.Complete(ticket.Id, now);
        await chatRepository.SaveChangesAsync(ct);
        return ticket.Id;
    }

    private static ChatSessionDto ToDto(ChatSession s) => new(s.Id, s.Status, s.Mode, s.AssignedAgentId, s.ResultingTicketId, s.StartedAtUtc);
}
```

### 3 — Infrastructure: EF config, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — extend the existing `ChatSession` block with one property line:

```csharp
            entity.Property(s => s.Mode).HasConversion<string>().HasMaxLength(16).IsRequired();
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<IAiChatbotProvider, MockAiChatbotProvider>();
        services.AddScoped<AiChatbotService>();
```

- After creating these files, run `dotnet ef migrations add AddChatbotMode --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: `ChatbotController`

**Create file: `src/SupportCrm.Api/Controllers/ChatbotController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Ai;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/chatbot")]
public class ChatbotController(AiChatbotService chatbotService) : ControllerBase
{
    [HttpPost("sessions")]
    public async Task<ActionResult<ChatSessionDto>> Start([FromBody] StartChatbotRequest request, CancellationToken ct) =>
        await chatbotService.StartAsync(request, ct);

    [HttpPost("sessions/{id:guid}/messages")]
    public async Task<ActionResult<ChatbotReplyDto>> SendMessage(Guid id, [FromBody] SendChatbotMessageRequest request, CancellationToken ct)
    {
        try { return await chatbotService.SendMessageAsync(id, request, ct); }
        catch (ChatSessionNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpPost("sessions/{id:guid}/request-human")]
    public async Task<ActionResult<ChatSessionDto>> RequestHuman(Guid id, CancellationToken ct)
    {
        try { return await chatbotService.RequestHumanAsync(id, ct); }
        catch (ChatSessionNotFoundException) { return NotFound(); }
    }

    [HttpPost("sessions/{id:guid}/create-ticket")]
    public async Task<IActionResult> CreateTicket(Guid id, CancellationToken ct)
    {
        try
        {
            var ticketId = await chatbotService.CreateTicketAsync(id, ct);
            return Ok(new { ticketId });
        }
        catch (ChatSessionNotFoundException) { return NotFound(); }
    }
}
```

**Note for the executor:** an escalated (or completed) bot session is read the same way as any human `ChatSession` — `GET /api/chat-sessions/{id}/messages` and `/status` (Communication Channels CC-3, `ChatController`, unmodified) already work for it. No new "view conversation" endpoint is added here.

---

## Edge Cases & Failure Modes

- **`SendMessageAsync` called after the session already escalated to `Human`** — rejected with a `400` and an explanatory message, not silently accepted as a bot reply; further messages must go through CC-3's `POST /api/chat-sessions/{id}/messages` instead.
- **`RequestHumanAsync` with no agent currently available** — `RequestHuman(null)` sets `Mode = Human`, `Status = Queued`, `AssignedAgentId` stays `null` — same "queued, no one yet" state CC-3's own `StartAsync` already produces when nobody's available; the existing `GET /api/chat-sessions/{id}/status` queue-position logic (keyed off `Status == Queued`, mode-agnostic) picks it up unchanged.
- **`CreateTicketAsync` on a session with zero messages** — `ChatTranscriptFormatter.Format` returns `"(no messages)"` rather than throwing or sending an empty string, same fallback `ChatService.CompleteAsync` already relies on.
- **Bot asked something with no KB grounding at all** — `MockAiChatbotProvider.Answer` returns `CanResolve: false` with a fallback offering a human agent or a ticket; the frontend, not the backend, decides whether to auto-suggest those actions — no server-side auto-escalation happens on a single unresolved answer.
- **`ChatSessionMode` added to `ChatSessionDto`** — every existing consumer of `ChatSessionDto` (CC-3's `ChatController.Start`) still compiles since the record only gained a field, not lost one; verify at build time that no positional-record construction elsewhere in the codebase relies on the old field order (this story's `ChatService.ToDto` is the only one and is updated above).

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/ChatSessionTests.cs`**:
   - `Constructor_BotMode_StartsActive`
   - `RequestHuman_NoAgentAvailable_SetsQueued`
2. **Unit — `tests/SupportCrm.Application.Tests/Ai/MockAiChatbotProviderTests.cs`**:
   - `Answer_NoGrounding_ReturnsCanResolveFalse`
3. **Unit — `tests/SupportCrm.Application.Tests/Ai/AiChatbotServiceTests.cs`**:
   - `SendMessageAsync_AfterEscalation_Throws`
   - `CreateTicketAsync_FoldsTranscriptIntoOneIngestionCall`
4. **Integration — `tests/SupportCrm.Api.Tests/Controllers/ChatbotControllerTests.cs`**:
   - `Post_SendMessage_UnknownSession_Returns404`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddChatbotMode --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.
3. **Regression:** confirm `POST /api/chat-sessions` (Communication Channels CC-3, human path) still creates a `Queued`/`Active` session exactly as before, with `Mode: "Human"` in its response.

---

## Done Criteria

- [ ] `POST /api/chatbot/sessions` starts a bot-mode session; `POST .../messages` answers using KB-grounded, language-matched replies.
- [ ] `POST .../request-human` escalates to the existing human-agent queue.
- [ ] `POST .../create-ticket` creates a ticket from the transcript.
- [ ] Escalated/completed bot sessions are readable via CC-3's existing, unmodified chat-session endpoints.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
