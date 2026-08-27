# Story 12 — Live chat (Story: CC-3)

---

## Prerequisites

- Story 10 completed: [`10-story-CC-1.md`](10-story-CC-1.md) — `TicketIngestionService` (converting a completed chat into a ticket reuses it).
- Ticket Management Story 07 completed: [`../ticket-management/07-story-TM-3.md`](../ticket-management/07-story-TM-3.md) — `Agent` entity, extended here with `IsAvailable`.

---

## Story Goal

1. A customer can start a chat session (one click, no ticket exists yet at this point — a `ChatSession` is its own aggregate, not a `Ticket`).
2. The session is routed to an available agent via **FIFO queueing** — the first available agent picks up the longest-waiting queued session. Skill-based routing needs a skills taxonomy that doesn't exist in this codebase; this story does **not** build one (flagged explicitly, not silently narrowed).
3. While queued, the customer can poll for queue position (used to compute a naive estimated wait time) and a typing indicator — **polling, not WebSockets**, per team decision.
4. On completion, every `ChatMessage` becomes a `TicketMessage` via CC-1's shared ingestion path (`Channel: Chat`), and the chat session is marked completed.

---

## Context — Read These Files First

1. [`10-story-CC-1.md`](10-story-CC-1.md), `## Backend Tasks` → `### 2` (`TicketIngestionService.IngestInboundMessageAsync`) — this story calls it once per chat message when converting a completed session to a ticket (or once with the concatenated transcript — see Backend Tasks `### 2` below for the exact approach).
2. `../ticket-management/07-story-TM-3.md`, `## Backend Tasks` → `### 1` (`Agent` entity) — add `IsAvailable` here; this story's routing query filters on it.
3. `src/SupportCrm.Application/Tickets/TicketCustomerResolver.cs` (from Ticket Management TM-1) — a chat session may start with only a name (no verified contact value) if the customer doesn't type one; confirm this resolver already tolerates a null contact value (it does, per its existing null-check) before assuming new work is needed here.

---

## Backend Tasks

### 1 — Domain: `ChatSession`, `ChatMessage`, `Agent.IsAvailable`

**Create file: `src/SupportCrm.Domain/Entities/ChatSessionStatus.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public enum ChatSessionStatus
{
    Queued,
    Active,
    Completed
}
```

**Create file: `src/SupportCrm.Domain/Entities/ChatSession.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class ChatSession
{
    public Guid Id { get; private set; }
    public string CustomerName { get; private set; } = default!;
    public string? CustomerContactValue { get; private set; }
    public ChatSessionStatus Status { get; private set; }
    public Guid? AssignedAgentId { get; private set; }
    public Guid? ResultingTicketId { get; private set; }
    public bool CustomerIsTyping { get; private set; }
    public bool AgentIsTyping { get; private set; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public DateTimeOffset? EndedAtUtc { get; private set; }

    private ChatSession() { } // EF Core

    public ChatSession(string customerName, string? customerContactValue, DateTimeOffset startedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(customerName))
            throw new ArgumentException("Customer name is required.", nameof(customerName));

        Id = Guid.NewGuid();
        CustomerName = customerName;
        CustomerContactValue = customerContactValue;
        Status = ChatSessionStatus.Queued;
        StartedAtUtc = startedAtUtc;
    }

    public void AssignAgent(Guid agentId)
    {
        AssignedAgentId = agentId;
        Status = ChatSessionStatus.Active;
    }

    public void Complete(Guid resultingTicketId, DateTimeOffset atUtc)
    {
        Status = ChatSessionStatus.Completed;
        ResultingTicketId = resultingTicketId;
        EndedAtUtc = atUtc;
    }

    public void SetTyping(bool isCustomer, bool isTyping)
    {
        if (isCustomer) CustomerIsTyping = isTyping;
        else AgentIsTyping = isTyping;
    }
}
```

**Create file: `src/SupportCrm.Domain/Entities/ChatMessage.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class ChatMessage
{
    public Guid Id { get; private set; }
    public Guid ChatSessionId { get; private set; }
    public string Body { get; private set; } = default!;
    public bool IsFromCustomer { get; private set; }
    public DateTimeOffset SentAtUtc { get; private set; }

    private ChatMessage() { } // EF Core

    public ChatMessage(Guid chatSessionId, string body, bool isFromCustomer, DateTimeOffset sentAtUtc)
    {
        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Message body is required.", nameof(body));

        Id = Guid.NewGuid();
        ChatSessionId = chatSessionId;
        Body = body;
        IsFromCustomer = isFromCustomer;
        SentAtUtc = sentAtUtc;
    }
}
```

**File: `src/SupportCrm.Domain/Entities/Agent.cs`** (from Ticket Management TM-3) — add:

```csharp
    public bool IsAvailable { get; private set; } = true;

    public void SetAvailability(bool isAvailable) => IsAvailable = isAvailable;
```

**Create file: `src/SupportCrm.Domain/Repositories/IChatRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IChatRepository
{
    Task<ChatSession?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ChatSession>> GetQueuedAsync(CancellationToken ct);
    Task<int> CountQueuedAheadOfAsync(DateTimeOffset startedAtUtc, CancellationToken ct);
    Task AddAsync(ChatSession session, CancellationToken ct);
    Task<IReadOnlyList<ChatMessage>> GetMessagesAsync(Guid chatSessionId, CancellationToken ct);
    Task AddMessageAsync(ChatMessage message, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, `ChatService` (start/route/message/typing/complete)

**Create file: `src/SupportCrm.Application/Tickets/ChatDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record StartChatRequest(string CustomerName, string? CustomerContactValue);
public record ChatSessionDto(Guid Id, ChatSessionStatus Status, Guid? AssignedAgentId, Guid? ResultingTicketId, DateTimeOffset StartedAtUtc);
public record ChatQueueStatusDto(int QueuePosition, int EstimatedWaitSeconds, bool CustomerIsTyping, bool AgentIsTyping);
public record SendChatMessageRequest(string Body, bool IsFromCustomer);
public record ChatMessageDto(Guid Id, string Body, bool IsFromCustomer, DateTimeOffset SentAtUtc);
public record SetTypingRequest(bool IsCustomer, bool IsTyping);

public class ChatSessionNotFoundException(Guid id) : Exception($"Chat session '{id}' was not found.");
```

**Create file: `src/SupportCrm.Application/Tickets/ChatService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class ChatService(
    IChatRepository chatRepository,
    IAgentRepository agentRepository,
    TicketIngestionService ingestionService,
    TimeProvider timeProvider)
{
    private const int AverageHandlingSecondsPerQueuedChat = 180; // naive constant, not a statistical estimate

    public async Task<ChatSessionDto> StartAsync(StartChatRequest request, CancellationToken ct)
    {
        var session = new ChatSession(request.CustomerName.Trim(), request.CustomerContactValue?.Trim(), timeProvider.GetUtcNow());
        await chatRepository.AddAsync(session, ct);

        // FIFO-to-any-available-agent — no skill matching (no skills taxonomy exists).
        var agents = await agentRepository.GetAllAsync(ct);
        var availableAgent = agents.FirstOrDefault(a => a.IsAvailable);
        if (availableAgent is not null)
            session.AssignAgent(availableAgent.Id);

        await chatRepository.SaveChangesAsync(ct);
        return ToDto(session);
    }

    public async Task<ChatQueueStatusDto> GetQueueStatusAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        var position = session.Status == ChatSessionStatus.Queued
            ? await chatRepository.CountQueuedAheadOfAsync(session.StartedAtUtc, ct) + 1
            : 0;
        return new ChatQueueStatusDto(position, position * AverageHandlingSecondsPerQueuedChat, session.CustomerIsTyping, session.AgentIsTyping);
    }

    public async Task<ChatMessageDto> AddMessageAsync(Guid sessionId, SendChatMessageRequest request, CancellationToken ct)
    {
        _ = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        var message = new ChatMessage(sessionId, request.Body.Trim(), request.IsFromCustomer, timeProvider.GetUtcNow());
        await chatRepository.AddMessageAsync(message, ct);
        await chatRepository.SaveChangesAsync(ct);
        return new ChatMessageDto(message.Id, message.Body, message.IsFromCustomer, message.SentAtUtc);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(Guid sessionId, CancellationToken ct) =>
        (await chatRepository.GetMessagesAsync(sessionId, ct))
            .OrderBy(m => m.SentAtUtc)
            .Select(m => new ChatMessageDto(m.Id, m.Body, m.IsFromCustomer, m.SentAtUtc))
            .ToList();

    public async Task SetTypingAsync(Guid sessionId, SetTypingRequest request, CancellationToken ct)
    {
        var session = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        session.SetTyping(request.IsCustomer, request.IsTyping);
        await chatRepository.SaveChangesAsync(ct);
    }

    public async Task<Guid> CompleteAsync(Guid sessionId, CancellationToken ct)
    {
        var session = await chatRepository.GetByIdAsync(sessionId, ct) ?? throw new ChatSessionNotFoundException(sessionId);
        var messages = await chatRepository.GetMessagesAsync(sessionId, ct);

        // Fold the transcript into the shared ingestion path as one inbound event whose
        // "Body" is the full transcript — simpler and more robust than replaying each
        // ChatMessage through ingestion individually (which would create N ticket messages
        // interleaved with the ticket's own creation semantics in awkward ways).
        var transcript = string.Join("\n", messages.OrderBy(m => m.SentAtUtc).Select(m => $"{(m.IsFromCustomer ? "Customer" : "Agent")}: {m.Body}"));
        var ticket = await ingestionService.IngestInboundMessageAsync(
            new IngestInboundMessageRequest(TicketChannel.Chat, session.CustomerName, session.CustomerContactValue, "Live chat transcript", transcript), ct);

        var now = timeProvider.GetUtcNow();
        session.Complete(ticket.Id, now);
        await chatRepository.SaveChangesAsync(ct);
        return ticket.Id;
    }

    private static ChatSessionDto ToDto(ChatSession s) => new(s.Id, s.Status, s.AssignedAgentId, s.ResultingTicketId, s.StartedAtUtc);
}
```

**Design note for the executor:** `CompleteAsync` folds the whole transcript into a single ingested message rather than replaying each `ChatMessage` through `TicketIngestionService` one at a time. Replaying individually would call `IngestInboundMessageAsync` — which only ever adds **customer**-authored messages via the ingestion path — for every message including the agent's, which doesn't fit that method's "inbound from a customer" contract. Flag this design choice in review: the resulting ticket has one long transcript message rather than N interleaved messages; if per-message fidelity in the unified timeline is wanted instead, `TicketIngestionService` would need a second entry point for agent-authored inbound-adjacent messages — treat that as a follow-up, not a defect in this story.

### 3 — Infrastructure: EF config, repository, DI

**File: `SupportCrmDbContext.cs`** — add `DbSet<ChatSession>`, `DbSet<ChatMessage>` + `OnModelCreating` blocks (standard style); extend the `Agent` block with `entity.Property(a => a.IsAvailable).IsRequired();`.

**Create file: `src/SupportCrm.Infrastructure/Persistence/ChatRepository.cs`** — straightforward EF implementation; `CountQueuedAheadOfAsync` is `dbContext.ChatSessions.CountAsync(s => s.Status == ChatSessionStatus.Queued && s.StartedAtUtc < startedAtUtc, ct)`.

**File: `DependencyInjection.cs`** — add `IChatRepository/ChatRepository`, `ChatService`.

- After creating these files, run `dotnet ef migrations add AddLiveChat --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: controller

**Create file: `src/SupportCrm.Api/Controllers/ChatController.cs`** — `[Route("api/chat-sessions")]`:
- `POST` — `StartAsync`.
- `GET {id:guid}/status` — `GetQueueStatusAsync`.
- `POST {id:guid}/messages` — `AddMessageAsync`.
- `GET {id:guid}/messages` — `GetMessagesAsync` (customer/agent widget polls this).
- `PUT {id:guid}/typing` — `SetTypingAsync`.
- `POST {id:guid}/complete` — `CompleteAsync`, returns `{ ticketId }`.
- All catch `ChatSessionNotFoundException` → `404`.

---

## Edge Cases & Failure Modes

- **No available agent when a chat starts** — `StartAsync` leaves the session `Queued` with `AssignedAgentId: null`; the customer sees a queue position via `GetQueueStatusAsync` rather than an error.
- **Completing a session with zero messages** — `transcript` is an empty string; `IngestInboundMessageAsync` still creates/updates a ticket with an empty-string message body — the domain layer doesn't reject empty ticket messages at the entity level for this path (the `TicketMessage` constructor does reject blank bodies!). **Flag explicitly:** this is a real edge case the executor must resolve — either guard `CompleteAsync` to substitute a placeholder body (`"(no messages)"`) when the transcript is empty, or accept that completing an empty chat throws; do not leave this ambiguous.
- **Typing indicator polling** — `SetTyping`/`GetQueueStatusAsync` have no expiry (a "stuck" typing indicator if the setter never clears it) — acceptable simplification for polling-based chat; a real-time implementation would use a timeout, this one relies on the client explicitly clearing it.
- **Unknown chat session id on any endpoint** — `ChatSessionNotFoundException` → `404`.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/ChatServiceTests.cs`**:
   - `StartAsync_WithAvailableAgent_AssignsImmediately`
   - `StartAsync_WithNoAvailableAgent_StaysQueued`
   - `GetQueueStatusAsync_QueuePositionCountsOnlyEarlierQueuedSessions`
   - `CompleteAsync_CallsIngestionServiceWithTranscript`
2. **Integration — `tests/SupportCrm.Api.Tests/Controllers/ChatControllerTests.cs`**:
   - `Post_CompleteChat_ReturnsTicketId`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddLiveChat --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] A chat session can be started and is routed to an available agent (FIFO) or queued.
- [ ] Queue position, estimated wait, and typing indicators are pollable.
- [ ] Completing a chat creates/updates a ticket with the transcript, via the shared ingestion path.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
