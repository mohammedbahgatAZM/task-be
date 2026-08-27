# Story 15 — Unified conversation view (Story: CC-6)

---

## Prerequisites

- Stories 10–14 completed (`10-story-CC-1.md`..`14-story-CC-5.md`) — every channel this story unifies.
- Ticket Management Story 09 completed: [`../ticket-management/09-story-TM-5.md`](../ticket-management/09-story-TM-5.md) — `TicketTimelineService`.

---

## Story Goal

**Plan-drafting note:** the backend CC-1 intake originally expected `TicketMessage.Channel` to be added by this story; while drafting CC-1's plan it became clear the field is needed from the very first channel (there's no point in a channel-agnostic ingestion path that can't record which channel a message came from), so **`TicketMessage.Channel` was added in Story 10 (CC-1), not here**. This story's real, new work is narrower than the intake implied:

1. **Reply-dispatch routing**: when an agent replies from the unified ticket thread, pick the right outbound seam (`IEmailSender`/`IWhatsAppSender`/`ISmsSender`, or a plain internal `TicketMessage` for Chat/WebForm — those channels have no "reply back through the same channel" concept once the originating session/submission is over) based on the ticket's most recent inbound channel, rather than requiring the agent to pick a channel-specific endpoint (`.../email-replies`, `.../whatsapp-messages`, `.../sms-messages`) by hand.
2. **Extend `TicketTimelineService`** so its entries expose the `Channel` that CC-1 added, for messages that have one.
3. **End-to-end verification** across all 5 channels feeding one ticket — this story doesn't re-derive the dedup logic (CC-1 already built and tested it), it exercises it across the full channel set.

---

## Context — Read These Files First

1. [`10-story-CC-1.md`](10-story-CC-1.md), `## Backend Tasks` → `### 2` (`TicketMessage.Channel`, `EmailChannelService`) — the field and the per-channel send-service pattern this story's dispatcher sits on top of.
2. [`11-story-CC-2.md`](11-story-CC-2.md) `### 2` (`WhatsAppChannelService`) and [`13-story-CC-4.md`](13-story-CC-4.md) `### 2` (`SmsChannelService`) — the two other per-channel send services the dispatcher routes between.
3. `../ticket-management/09-story-TM-5.md`, `## Backend Tasks` → `### 2` (`TicketTimelineService.GetTimelineAsync`, `TicketTimelineEntryDto`) — add a `Channel` property to the DTO and populate it from each `TicketMessage`'s `Channel` (status/assignment/escalation entries have no channel — leave it `null` for those, don't invent one).

---

## Backend Tasks

### 1 — Domain: nothing new

Confirmed no new entities — this story is routing logic + one DTO field, on top of Stories 10–14's tables.

### 2 — Application: reply dispatcher, timeline DTO extension

**Create file: `src/SupportCrm.Application/Tickets/ChannelReplyDispatcher.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public record DispatchReplyRequest(string Body, string ChangedBy);

/// <summary>
/// Picks the right outbound channel for a reply based on the ticket's most recent
/// customer-authored message's channel, so an agent replying from the unified thread
/// doesn't have to pick a channel-specific endpoint by hand. Chat and WebForm have no
/// "reply back through the same channel" concept once their originating session/submission
/// is over (there is no live chat connection or web-form response channel to reply
/// through) — for those, and for tickets with no channel history at all, the reply is
/// recorded as a plain internal `TicketMessage` with no outbound send, which is the
/// correct behavior, not a missing feature.
/// </summary>
public class ChannelReplyDispatcher(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    EmailChannelService emailChannelService,
    WhatsAppChannelService whatsAppChannelService,
    SmsChannelService smsChannelService,
    TimeProvider timeProvider)
{
    public async Task<TicketMessageDto> ReplyAsync(Guid ticketId, DispatchReplyRequest request, CancellationToken ct)
    {
        _ = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());

        var messages = await messageRepository.GetMessagesAsync(ticketId, ct);
        var lastInboundChannel = messages
            .Where(m => m.AuthorKind == "Customer")
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => m.Channel)
            .FirstOrDefault();

        return lastInboundChannel switch
        {
            TicketChannel.Email => await emailChannelService.SendReplyAsync(ticketId, new SendEmailReplyRequest(request.Body, request.ChangedBy, null), ct),
            TicketChannel.WhatsApp => await whatsAppChannelService.SendAsync(ticketId, new SendWhatsAppMessageRequest(request.Body, request.ChangedBy, null, IsTemplate: false), ct),
            TicketChannel.Sms => await smsChannelService.SendAsync(ticketId, new SendSmsRequest(request.Body, request.ChangedBy), ct),
            _ => await RecordPlainReplyAsync(ticketId, request, ct) // Chat, WebForm, Manual, or no channel history yet
        };
    }

    private async Task<TicketMessageDto> RecordPlainReplyAsync(Guid ticketId, DispatchReplyRequest request, CancellationToken ct)
    {
        var message = new TicketMessage(ticketId, request.Body, request.ChangedBy, "Agent", timeProvider.GetUtcNow());
        await messageRepository.AddMessageAsync(message, ct);
        await messageRepository.SaveChangesAsync(ct);
        return new TicketMessageDto(message.Id, message.Body, message.AuthorName, message.AuthorKind, message.CreatedAtUtc);
    }
}
```

**File: `src/SupportCrm.Application/Tickets/TicketMessageDtos.cs`** (from TM-5) — add a `Channel` property to `TicketTimelineEntryDto`:

```csharp
public record TicketTimelineEntryDto(
    Guid Id,
    string Kind,
    bool IsCustomerVisible,
    DateTimeOffset OccurredAtUtc,
    string Summary,
    string AuthorName,
    TicketChannel? Channel);
```

(This changes an existing record's shape — every call site that constructs a `TicketTimelineEntryDto` must be updated, not just the ones this story cares about; see Backend Tasks `### 2`'s next paragraph for the one call site that actually needs a real value.)

**File: `src/SupportCrm.Application/Tickets/TicketTimelineService.cs`** (from TM-5) — update the `messages.Select(...)` line to pass `m.Channel` as the new argument; update the other four `.Select(...)` calls (notes, statusChanges, assignments, escalations) to pass `null` for `Channel` explicitly — they have no channel concept, and passing `null` is a deliberate statement of that, not a placeholder to fill in later.

### 3 — Infrastructure: DI only

**File: `DependencyInjection.cs`** — add `services.AddScoped<ChannelReplyDispatcher>();`. No new EF config/migration (only a DTO field change, not a domain/table change).

### 4 — Api: controller

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add:

```csharp
    [HttpPost("{id:guid}/reply")]
    public async Task<ActionResult<TicketMessageDto>> Reply(Guid id, [FromBody] DispatchReplyRequest request, [FromServices] ChannelReplyDispatcher dispatcher, CancellationToken ct)
    {
        try { return await dispatcher.ReplyAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
```

**Frontend-facing note:** this new `POST /api/tickets/{id}/reply` is the single endpoint the unified UI should call going forward; the channel-specific endpoints (`.../email-replies`, `.../whatsapp-messages`, `.../sms-messages`) from CC-1/CC-2/CC-4 remain available (e.g. for a future "force this channel" override action) but are no longer the primary compose path once this story lands.

---

## Edge Cases & Failure Modes

- **Ticket with messages only from Chat/WebForm (no Email/WhatsApp/SMS ever)** — `lastInboundChannel` is `TicketChannel.Chat` or `.WebForm`, hits the `_` branch, records a plain internal message — correct: there's no live channel to reply through for either.
- **Ticket with zero messages at all** (e.g. created manually with no ingestion) — `lastInboundChannel` is `default(TicketChannel?)` = `null`, hits the `_` branch — same correct fallback.
- **Sending fails inside a channel-specific service** (e.g. CC-2's messaging-window check) — the underlying `InvalidOperationException` propagates up through the dispatcher unchanged; the controller's existing catch handles it — the dispatcher does not swallow or reinterpret channel-specific errors.
- **`TicketTimelineEntryDto` shape change breaks any other caller** — flagged explicitly above; the executor must grep for every construction site, not just the one in `TicketTimelineService`, before considering this story done.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/ChannelReplyDispatcherTests.cs`**:
   - `ReplyAsync_LastInboundEmail_CallsEmailChannelService`
   - `ReplyAsync_LastInboundWhatsApp_CallsWhatsAppChannelService`
   - `ReplyAsync_LastInboundSms_CallsSmsChannelService`
   - `ReplyAsync_LastInboundChat_RecordsPlainMessage`
   - `ReplyAsync_NoMessageHistory_RecordsPlainMessage`
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketTimelineServiceChannelTests.cs`**:
   - `GetTimelineAsync_MessageEntriesCarryChannel_OtherKindsAreNull`
3. **Integration — end-to-end, manual or scripted** — feed the same customer through email, then WhatsApp, then a web form; confirm all three land on one ticket (`GET /api/tickets/{id}/timeline` shows all three, each labeled with its channel) and no duplicate tickets were created.

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Manual smoke:** the 3-channel end-to-end scenario above.

---

## Done Criteria

- [ ] `POST /api/tickets/{id}/reply` routes to the correct channel-specific send service based on the last inbound channel.
- [ ] Chat/WebForm/no-history tickets get a plain internal reply, not an error.
- [ ] Timeline entries expose `Channel` for messages (null for status/assignment/escalation/note entries).
- [ ] End-to-end: email → WhatsApp → web form for the same customer lands on one ticket, not three.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
