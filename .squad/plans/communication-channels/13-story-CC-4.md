# Story 13 — SMS (Story: CC-4)

---

## Prerequisites

- Story 10 completed: [`10-story-CC-1.md`](10-story-CC-1.md) — `TicketIngestionService`, `TicketMessageDeliveryStatus`.
- Ticket Management Story 08 completed: [`../ticket-management/08-story-TM-4.md`](../ticket-management/08-story-TM-4.md) — `ICustomerStatusNotifier`/`NoOpCustomerStatusNotifier`, which this story replaces with a real (mocked) implementation.

---

## Story Goal

1. Ticket status changes can trigger an outbound SMS — this story provides the **first real implementation** of TM-4's `ICustomerStatusNotifier` seam (previously always a no-op).
2. Inbound SMS replies (stub webhook) append to the customer's ticket via CC-1's shared ingestion path (`Channel: Sms`).
3. Outbound messages over 1 segment (160 GSM-7 chars) are split into ordered parts with no data loss — a real splitter, not just a length check.
4. Delivery failures are recorded via CC-1's shared `TicketMessageDeliveryStatus`, with an SMS-specific `"Failed"` status settable via a stub webhook.

**Explicit, team-approved scope decision:** no real SMS gateway exists. `ISmsSender` is a mock/logging seam, same pattern as CC-1/CC-2.

---

## Context — Read These Files First

1. [`10-story-CC-1.md`](10-story-CC-1.md) and [`11-story-CC-2.md`](11-story-CC-2.md), `## Backend Tasks` → `### 2` — the sender-seam/mock/channel-service three-piece shape this story's `ISmsSender`/`MockSmsSender`/`SmsChannelService` follows for the fourth time; by now the pattern should require no new design decisions, only following precedent.
2. `../ticket-management/08-story-TM-4.md`, `## Backend Tasks` → `### 2` (`ICustomerStatusNotifier`, `TicketService.SetStatusAsync`) — this story's `SmsCustomerStatusNotifier` implements that exact interface; the DI registration swap is the only change to existing wiring.

---

## Backend Tasks

### 1 — Domain: nothing new

Same as CC-2 — no new entities; SMS reuses `TicketMessageDeliveryStatus` and `TicketMessage.Channel` from CC-1.

### 2 — Application: sender seam, segment splitter, channel service, status notifier

**Create file: `src/SupportCrm.Application/Tickets/ISmsSender.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

public interface ISmsSender
{
    Task<string> SendAsync(string toPhoneNumber, string body, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Tickets/MockSmsSender.cs`** — mirrors `MockEmailSender`/`MockWhatsAppSender`: logs `to`, segment count (via `SmsSegmenter.Split`, below), returns a fake provider message id.

**Create file: `src/SupportCrm.Application/Tickets/SmsSegmenter.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

/// <summary>
/// Splits an outbound SMS body into ordered, non-data-losing segments using the standard
/// GSM-7 160-character-per-segment limit (single segment) — multi-segment messages use a
/// smaller 153-character-per-segment budget to leave room for concatenation headers, matching
/// how real carriers segment long SMS (this codebase doesn't need to send the real UDH bytes
/// since no real gateway exists, but the character-budget behavior itself is real).
/// </summary>
public static class SmsSegmenter
{
    private const int SingleSegmentLimit = 160;
    private const int MultiSegmentLimit = 153;

    public static IReadOnlyList<string> Split(string body)
    {
        if (body.Length <= SingleSegmentLimit)
            return new[] { body };

        var segments = new List<string>();
        for (var offset = 0; offset < body.Length; offset += MultiSegmentLimit)
            segments.Add(body.Substring(offset, Math.Min(MultiSegmentLimit, body.Length - offset)));
        return segments;
    }
}
```

**Create file: `src/SupportCrm.Application/Tickets/SmsChannelDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

public record SendSmsRequest(string Body, string ChangedBy);
public record RecordSmsDeliveryFailureRequest(Guid TicketMessageId, string Reason);
```

**Create file: `src/SupportCrm.Application/Tickets/SmsChannelService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SmsChannelService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ISmsSender smsSender,
    TimeProvider timeProvider)
{
    public async Task<TicketMessageDto> SendAsync(Guid ticketId, SendSmsRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        if (string.IsNullOrWhiteSpace(ticket.RequesterContactValue))
            throw new InvalidOperationException("This ticket has no requester phone number to text.");

        var segments = SmsSegmenter.Split(request.Body);
        foreach (var segment in segments)
            await smsSender.SendAsync(ticket.RequesterContactValue, segment, ct);

        var now = timeProvider.GetUtcNow();
        var message = new TicketMessage(ticketId, request.Body, request.ChangedBy, "Agent", now);
        message.SetChannel(TicketChannel.Sms);
        await messageRepository.AddMessageAsync(message, ct);
        await messageRepository.AddDeliveryStatusAsync(new TicketMessageDeliveryStatus(message.Id, "Sent", $"{segments.Count} segment(s)", now), ct);
        await messageRepository.SaveChangesAsync(ct);

        return new TicketMessageDto(message.Id, message.Body, message.AuthorName, message.AuthorKind, message.CreatedAtUtc);
    }

    public async Task RecordDeliveryFailureAsync(RecordSmsDeliveryFailureRequest request, CancellationToken ct)
    {
        _ = await messageRepository.GetMessageByIdAsync(request.TicketMessageId, ct)
            ?? throw new KeyNotFoundException($"Ticket message '{request.TicketMessageId}' was not found.");
        await messageRepository.AddDeliveryStatusAsync(
            new TicketMessageDeliveryStatus(request.TicketMessageId, "Failed", request.Reason, timeProvider.GetUtcNow()), ct);
        await messageRepository.SaveChangesAsync(ct);
    }
}
```

**Create file: `src/SupportCrm.Application/Tickets/SmsCustomerStatusNotifier.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SmsCustomerStatusNotifier(ITicketRepository ticketRepository, ISmsSender smsSender) : ICustomerStatusNotifier
{
    public async Task NotifyStatusChangedAsync(Guid ticketId, TicketStatus newStatus, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct);
        if (ticket?.RequesterContactValue is null) return; // no phone number on file — nothing to notify, not an error
        await smsSender.SendAsync(ticket.RequesterContactValue, $"Your ticket {ticket.ReferenceNumber} status is now {newStatus}.", ct);
    }
}
```

### 3 — Infrastructure: DI (replaces the no-op notifier)

**File: `DependencyInjection.cs`** — add `ISmsSender → MockSmsSender`, `SmsChannelService`; **replace** the existing `services.AddScoped<ICustomerStatusNotifier, NoOpCustomerStatusNotifier>();` line with:

```csharp
        services.AddScoped<ICustomerStatusNotifier, SmsCustomerStatusNotifier>();
```

No new EF config/migration — SMS reuses CC-1's tables.

### 4 — Api: webhook + ticket controller addition

**Create file: `src/SupportCrm.Api/Controllers/SmsChannelController.cs`** — `[Route("api/channels/sms")]`:
- `POST inbound` — `[FromForm]` `fromPhoneNumber`, `body`; calls `TicketIngestionService.IngestInboundMessageAsync` with `Channel: Sms` and `RequesterName` defaulted to the phone number itself (SMS senders have no separate "name" field, unlike email/WhatsApp) — flag this as a real, deliberate difference from the other channels' inbound shape, not an oversight.
- `POST delivery-failure` — `[FromBody] RecordSmsDeliveryFailureRequest`, `204`/`404`.

**File: `TicketsController.cs`** — add `POST {id:guid}/sms-messages` calling `SmsChannelService.SendAsync`, same try/catch shape as CC-1/CC-2's send endpoints.

---

## Edge Cases & Failure Modes

- **Body exactly 160 characters** — `SmsSegmenter.Split` returns a single segment (boundary is `<=`, not `<`) — verified, not assumed.
- **Body of 161 characters** — splits into 2 segments at the 153-character multi-segment budget, not 160 — per the real-world convention that multi-part SMS uses a smaller per-segment budget to leave room for concatenation metadata.
- **Empty body** — `SmsSegmenter.Split("")` returns `[""]` (one empty segment) since `"".Length <= 160`; `TicketMessage`'s constructor still rejects a blank overall body at the point `SendAsync` constructs it, so this never reaches the sender in practice — verify this ordering (segment split happens before the `TicketMessage` blank-check would fire) doesn't accidentally send a blank segment first. **Flag for the executor:** validate `request.Body` is non-blank before segmenting/sending, not only when constructing the `TicketMessage` afterward.
- **`ICustomerStatusNotifier` called for a ticket with no phone number** — `SmsCustomerStatusNotifier` returns silently (no exception) — a missing phone number is an expected, non-error state, not a failure.
- **Delivery-failure callback for an unknown message** — `KeyNotFoundException` → `404`.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/SmsSegmenterTests.cs`**:
   - `Split_ExactlySingleSegmentLimit_ReturnsOneSegment`
   - `Split_OverLimit_ReturnsMultipleSegmentsUsingMultiSegmentBudget`
   - `Split_ConcatenatedSegments_ReconstructOriginalBody` (no data loss, checked directly)
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/SmsChannelServiceTests.cs`**:
   - `SendAsync_BlankBody_ThrowsBeforeSending` (per the flagged edge case)
3. **Unit — `tests/SupportCrm.Application.Tests/Tickets/SmsCustomerStatusNotifierTests.cs`**:
   - `NotifyStatusChangedAsync_NoPhoneNumber_DoesNotThrow`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Manual smoke:** change a ticket's status with "notify customer" checked (TM-4 UI) on a ticket with a phone number; confirm the mock sender logs an SMS send.

---

## Done Criteria

- [ ] Status changes trigger an outbound SMS via the real (mocked) `ICustomerStatusNotifier`.
- [ ] Inbound SMS replies append to the customer's ticket via the shared ingestion path.
- [ ] Long messages split into multiple segments with no data loss.
- [ ] Delivery failures are recorded and visible via CC-1's shared delivery-statuses endpoint.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
