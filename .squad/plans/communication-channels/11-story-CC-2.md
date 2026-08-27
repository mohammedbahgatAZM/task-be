# Story 11 — WhatsApp (Story: CC-2)

---

## Prerequisites

- Story 10 completed: [`10-story-CC-1.md`](10-story-CC-1.md) — provides `TicketIngestionService`, `TicketAttachment`/`ITicketAttachmentStorage`, `TicketMessageDeliveryStatus`, and `TicketMessage.Channel` — this story reuses all four rather than rebuilding them.

---

## Story Goal

1. Inbound WhatsApp messages (stub webhook) go through CC-1's shared ingestion path (`Channel: WhatsApp`) — same dedup-to-open-ticket behavior, no new mechanism needed.
2. Agents send text, images, and documents via WhatsApp from the ticket — text via a message body, images/documents via CC-1's `TicketAttachment` upload, referenced in the same send call.
3. Delivery/read status per outbound WhatsApp message, using CC-1's `TicketMessageDeliveryStatus` with WhatsApp-specific status values (`Delivered`, `Read` in addition to `Sent`/`Failed`), updated via a stub status-callback webhook.
4. A real domain rule for the 24-hour customer-service messaging window: outside the window, a non-template send is rejected (not silently allowed) — even though no real WhatsApp Business API enforces it yet, the rule itself is genuine and enforced server-side.

**Explicit, team-approved scope decision:** no real WhatsApp Business API account exists. `IWhatsAppSender` is a mock/logging seam, same pattern as CC-1's `IEmailSender`.

---

## Context — Read These Files First

1. [`10-story-CC-1.md`](10-story-CC-1.md), `## Backend Tasks` → `### 2` (`TicketIngestionService`, `IEmailSender`/`MockEmailSender`, `EmailChannelService`) — this story's `IWhatsAppSender`/`MockWhatsAppSender`/`WhatsAppChannelService` follow the identical three-piece shape (seam, mock, per-channel service).
2. `src/SupportCrm.Domain/Entities/TicketMessageDeliveryStatus.cs` (from CC-1) — the `Status` field is a free-form string; this story just writes `"Delivered"`/`"Read"` into existing rows via a second webhook rather than adding new columns.
3. `src/SupportCrm.Application/Tickets/TicketIngestionService.cs` (from CC-1) — call this directly from the WhatsApp inbound webhook, passing `Channel: WhatsApp`; do not write a second ingestion path.

---

## Backend Tasks

### 1 — Domain: nothing new

No new entities — this story only adds application-layer logic and one computed rule on top of CC-1's tables. (If the executor finds a genuine need for a new column while implementing, flag it in review rather than silently adding one — the plan's intent is zero new domain types for this story.)

### 2 — Application: sender seam, messaging-window rule, channel service

**Create file: `src/SupportCrm.Application/Tickets/IWhatsAppSender.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

public interface IWhatsAppSender
{
    Task<string> SendAsync(string toPhoneNumber, string body, IReadOnlyList<TicketAttachmentDto> attachments, bool isTemplate, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Tickets/MockWhatsAppSender.cs`** — mirrors `MockEmailSender` (CC-1): logs `to`, `body` length, attachment count, `isTemplate`, returns a fake provider message id.

**Create file: `src/SupportCrm.Application/Tickets/WhatsAppChannelDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

public record SendWhatsAppMessageRequest(string Body, string ChangedBy, IReadOnlyList<Guid>? AttachmentIds, bool IsTemplate);
public record RecordWhatsAppStatusRequest(Guid TicketMessageId, string Status, string? Detail); // Status: "Delivered" | "Read" | "Failed"
```

**Create file: `src/SupportCrm.Application/Tickets/WhatsAppMessagingWindow.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

/// <summary>
/// WhatsApp Business API's real 24-hour customer-service window rule: a free-form
/// (non-template) message may only be sent within 24 hours of the customer's last
/// inbound message. No real provider enforces this yet, but the rule itself is real —
/// it is not a decorative check.
/// </summary>
public static class WhatsAppMessagingWindow
{
    public static bool IsOpen(DateTimeOffset? lastInboundAtUtc, DateTimeOffset nowUtc) =>
        lastInboundAtUtc is not null && nowUtc - lastInboundAtUtc.Value <= TimeSpan.FromHours(24);
}
```

**Create file: `src/SupportCrm.Application/Tickets/WhatsAppChannelService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class WhatsAppChannelService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ITicketAttachmentRepository attachmentRepository,
    IWhatsAppSender whatsAppSender,
    TimeProvider timeProvider)
{
    public async Task<TicketMessageDto> SendAsync(Guid ticketId, SendWhatsAppMessageRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        if (string.IsNullOrWhiteSpace(ticket.RequesterContactValue))
            throw new InvalidOperationException("This ticket has no requester phone number to message.");

        if (!request.IsTemplate)
        {
            var messages = await messageRepository.GetMessagesAsync(ticketId, ct);
            var lastInbound = messages.Where(m => m.AuthorKind == "Customer").Select(m => (DateTimeOffset?)m.CreatedAtUtc).Max();
            if (!WhatsAppMessagingWindow.IsOpen(lastInbound, timeProvider.GetUtcNow()))
                throw new InvalidOperationException(
                    "Outside the 24-hour messaging window — send a template message instead of a free-form reply.");
        }

        var attachments = request.AttachmentIds is { Count: > 0 }
            ? (await attachmentRepository.GetByTicketAsync(ticketId, ct))
                .Where(a => request.AttachmentIds.Contains(a.Id))
                .Select(a => new TicketAttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByName, a.UploadedAtUtc))
                .ToList()
            : new List<TicketAttachmentDto>();

        await whatsAppSender.SendAsync(ticket.RequesterContactValue, request.Body, attachments, request.IsTemplate, ct);

        var now = timeProvider.GetUtcNow();
        var message = new TicketMessage(ticketId, request.Body, request.ChangedBy, "Agent", now);
        message.SetChannel(TicketChannel.WhatsApp);
        await messageRepository.AddMessageAsync(message, ct);
        await messageRepository.AddDeliveryStatusAsync(new TicketMessageDeliveryStatus(message.Id, "Sent", null, now), ct);
        await messageRepository.SaveChangesAsync(ct);

        return new TicketMessageDto(message.Id, message.Body, message.AuthorName, message.AuthorKind, message.CreatedAtUtc);
    }

    public async Task RecordStatusAsync(RecordWhatsAppStatusRequest request, CancellationToken ct)
    {
        _ = await messageRepository.GetMessageByIdAsync(request.TicketMessageId, ct)
            ?? throw new KeyNotFoundException($"Ticket message '{request.TicketMessageId}' was not found.");
        await messageRepository.AddDeliveryStatusAsync(
            new TicketMessageDeliveryStatus(request.TicketMessageId, request.Status, request.Detail, timeProvider.GetUtcNow()), ct);
        await messageRepository.SaveChangesAsync(ct);
    }
}
```

### 3 — Infrastructure: DI only

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add `IWhatsAppSender → MockWhatsAppSender`, `WhatsAppChannelService`. No new EF config or migration is needed for this story (no new tables).

### 4 — Api: webhook + ticket controller additions

**Create file: `src/SupportCrm.Api/Controllers/WhatsAppChannelController.cs`** — `[Route("api/channels/whatsapp")]`:
- `POST inbound` — same shape as CC-1's email inbound webhook, calling `TicketIngestionService.IngestInboundMessageAsync` with `Channel: WhatsApp`, `[FromForm]` fields `fromPhoneNumber`, `fromName`, `body`, plus optional `IFormFileCollection? attachments` saved via `TicketAttachmentService` after ingestion (same executor note as CC-1's inbound endpoint).
- `POST status` — `[FromBody] RecordWhatsAppStatusRequest`, calls `WhatsAppChannelService.RecordStatusAsync`, `204`/`404`.

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add:

```csharp
    [HttpPost("{id:guid}/whatsapp-messages")]
    public async Task<ActionResult<TicketMessageDto>> SendWhatsAppMessage(Guid id, [FromBody] SendWhatsAppMessageRequest request, [FromServices] WhatsAppChannelService whatsAppChannelService, CancellationToken ct)
    {
        try { return await whatsAppChannelService.SendAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }
```

(Delivery/read status is read via CC-1's existing `GET /api/tickets/{id}/delivery-statuses` — no new read endpoint needed, since the shared table already covers WhatsApp's status values.)

---

## Edge Cases & Failure Modes

- **Sending a free-form message with zero inbound messages ever on the ticket** — `lastInbound` is `null`, `WhatsAppMessagingWindow.IsOpen` returns `false` → `400` requiring a template — correct behavior: a ticket with no prior customer message can't be "within 24 hours of the last one."
- **Sending a template message** — `request.IsTemplate: true` skips the window check entirely, per the real WhatsApp rule (templates are allowed outside the window).
- **Status callback for an unknown `TicketMessageId`** — `KeyNotFoundException` → `404`.
- **Sending with no requester phone number on the ticket** — `InvalidOperationException` → `400`, same pattern as CC-1's email path.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/WhatsAppMessagingWindowTests.cs`**:
   - `IsOpen_NullLastInbound_ReturnsFalse`
   - `IsOpen_Within24Hours_ReturnsTrue`
   - `IsOpen_MoreThan24HoursAgo_ReturnsFalse`
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/WhatsAppChannelServiceTests.cs`**:
   - `SendAsync_OutsideWindowNonTemplate_Throws`
   - `SendAsync_TemplateOutsideWindow_Succeeds`
   - `RecordStatusAsync_UnknownMessage_Throws`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Manual smoke:** inbound WhatsApp webhook for a new phone number creates a ticket; send a free-form reply within the window (succeeds); attempt one after simulating >24h (fails with 400 asking for a template).

---

## Done Criteria

- [ ] Inbound WhatsApp messages create/update a ticket via the shared ingestion path.
- [ ] Agents can send text + attachments via WhatsApp (`POST /api/tickets/{id}/whatsapp-messages`).
- [ ] Delivery/read status is recorded and readable via CC-1's shared delivery-statuses endpoint.
- [ ] The 24-hour window rule is enforced server-side for non-template sends.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
