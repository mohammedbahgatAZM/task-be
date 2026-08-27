# Story 10 — Email (Story: CC-1)

---

## Prerequisites

- Ticket Management Stories 05–09 completed (`../ticket-management/05-story-TM-1.md`..`09-story-TM-5.md`) — provides `Ticket`, `TicketMessage`, `TicketCustomerResolver`, `TicketService`, `ITicketMessageRepository`.

---

## Story Goal

1. **Introduce the shared, channel-agnostic ingestion path** every Communication Channels story (CC-2..CC-5) reuses: given a channel + requester identity + message body, resolve the customer, then find an existing **open** ticket for that customer or create a new one — never blindly create a new ticket per inbound event. This is the concrete mechanism behind CC-6's "switching channels mid-conversation does not create a duplicate ticket," built here because email is the first channel that needs it.
2. Add `TicketMessage.Channel` (which channel a message came in on/went out on) — every later channel story depends on this field existing; add it now rather than retrofitting it in CC-6.
3. Email-specific mechanics on top of that shared path: inbound webhook → ticket, agent reply → mocked "send," ticket-level attachments preserved both directions, bounce flagging.

**Explicit, team-approved scope decision:** no real mailbox exists. `IEmailSender` is a mock/logging seam — it records what it would have sent (to, subject, body, attachment ids) and returns a fake provider message id; it does not deliver real email. The inbound and bounce endpoints are webhook-shaped stand-ins for where a real provider (SendGrid Inbound Parse, Mailgun Routes, a polled IMAP mailbox, …) would call in. Wiring a real provider is an explicit future story, not implied by this one.

---

## Context — Read These Files First

1. `../ticket-management/05-story-TM-1.md` `## Backend Tasks` → `### 2` — `TicketCustomerResolver`/`TicketService` patterns this story's `TicketIngestionService` builds on directly (calls `TicketCustomerResolver.ResolveCustomerIdAsync`, calls `TicketService.CreateAsync` for the "no existing open ticket" branch — does not duplicate reference-number generation).
2. `src/SupportCrm.Domain/Entities/TicketMessage.cs` (28 lines, whole file, from TM-5) — add a `Channel` property here (nullable `TicketChannel` — `null` for internal/system messages that aren't tied to a customer-facing channel).
3. `src/SupportCrm.Domain/Repositories/ITicketRepository.cs` (25 lines, whole file) — add `FindOpenTicketForCustomerAsync` here, following the existing member style.
4. `../customer-management/04-story-CM-4.md` `## Backend Tasks` → `### 2`–`### 3` — the `IAttachmentStorage`/`LocalDiskAttachmentStorage` seam pattern this story's `ITicketAttachmentStorage`/`LocalDiskTicketAttachmentStorage` mirrors. **Do not reuse the CM-4 interface directly** — its methods are named/shaped around a `customerId`; a parallel ticket-scoped interface keeps both call sites simple. Flag this as a deliberate near-duplicate, not an oversight; a shared generic "owner-scoped attachment storage" abstraction is a reasonable future refactor, out of scope here.
5. `src/SupportCrm.Application/Tickets/TicketService.cs` (107 lines, whole file, as extended through TM-4) — `CreateAsync`'s signature/behavior; `TicketIngestionService` calls this, it does not reimplement ticket creation.
6. `src/SupportCrm.Api/Controllers/TicketsController.cs` (129 lines, whole file, as extended through TM-5) — controller conventions; this story adds a **new** `ChannelsController` for webhook-shaped endpoints (they don't belong under `api/tickets`) plus a few email-specific additions to `TicketsController` itself (reply, attachments).

---

## Backend Tasks

### 1 — Domain: `TicketMessage.Channel`, `TicketAttachment`, `TicketMessageDeliveryStatus`, open-ticket lookup

**File: `src/SupportCrm.Domain/Entities/TicketMessage.cs`** — add:

```csharp
    public TicketChannel? Channel { get; private set; }

    public void SetChannel(TicketChannel? channel) => Channel = channel;
```

(Constructor stays as-is; callers set the channel via `SetChannel` right after construction — adding a channel parameter to the constructor would ripple into every existing call site across TM-5 for no benefit, since most of those calls are channel-less internal notes.)

**Create file: `src/SupportCrm.Domain/Entities/TicketAttachment.cs`** — same shape as Customer Management's `CustomerAttachment` (CM-4, 32 lines), scoped to `TicketId` instead of `CustomerId`:

```csharp
namespace SupportCrm.Domain.Entities;

public class TicketAttachment
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string FileName { get; private set; } = default!;
    public string ContentType { get; private set; } = default!;
    public long SizeBytes { get; private set; }
    public string StorageKey { get; private set; } = default!;
    public string UploadedByName { get; private set; } = default!;
    public DateTimeOffset UploadedAtUtc { get; private set; }

    private TicketAttachment() { } // EF Core

    public TicketAttachment(Guid ticketId, string fileName, string contentType, long sizeBytes, string storageKey, string uploadedByName, DateTimeOffset uploadedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name is required.", nameof(fileName));
        if (sizeBytes <= 0)
            throw new ArgumentException("File size must be positive.", nameof(sizeBytes));

        Id = Guid.NewGuid();
        TicketId = ticketId;
        FileName = fileName;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType;
        SizeBytes = sizeBytes;
        StorageKey = storageKey;
        UploadedByName = string.IsNullOrWhiteSpace(uploadedByName) ? "unknown" : uploadedByName;
        UploadedAtUtc = uploadedAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Entities/TicketMessageDeliveryStatus.cs`** — shared across CC-1 (bounce), CC-2 (delivery/read), CC-4 (failure), so it isn't rebuilt three times:

```csharp
namespace SupportCrm.Domain.Entities;

public class TicketMessageDeliveryStatus
{
    public Guid Id { get; private set; }
    public Guid TicketMessageId { get; private set; }
    public string Status { get; private set; } = default!; // "Sent" | "Delivered" | "Read" | "Bounced" | "Failed"
    public string? Detail { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    private TicketMessageDeliveryStatus() { } // EF Core

    public TicketMessageDeliveryStatus(Guid ticketMessageId, string status, string? detail, DateTimeOffset occurredAtUtc)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Status is required.", nameof(status));

        Id = Guid.NewGuid();
        TicketMessageId = ticketMessageId;
        Status = status;
        Detail = detail;
        OccurredAtUtc = occurredAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ITicketAttachmentRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketAttachmentRepository
{
    Task<TicketAttachment?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TicketAttachment>> GetByTicketAsync(Guid ticketId, CancellationToken ct);
    Task AddAsync(TicketAttachment attachment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Extend file: `src/SupportCrm.Domain/Repositories/ITicketRepository.cs`** — add:

```csharp
    Task<Ticket?> FindOpenTicketForCustomerAsync(Guid customerId, CancellationToken ct);
```

**Extend file: `src/SupportCrm.Domain/Repositories/ITicketMessageRepository.cs`** — add:

```csharp
    Task<TicketMessage?> GetMessageByIdAsync(Guid messageId, CancellationToken ct);
    Task AddDeliveryStatusAsync(TicketMessageDeliveryStatus status, CancellationToken ct);
    Task<IReadOnlyList<TicketMessageDeliveryStatus>> GetDeliveryStatusesAsync(Guid ticketId, CancellationToken ct);
```

(`GetDeliveryStatusesAsync` takes a **ticket** id and joins across that ticket's messages — implemented as a join in Infrastructure, see below — because the frontend wants "all delivery statuses for this ticket," not one message at a time.)

### 2 — Application: ingestion service, email seam, attachment service, DTOs

**Create file: `src/SupportCrm.Application/Tickets/TicketIngestionDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record IngestInboundMessageRequest(
    TicketChannel Channel,
    string RequesterName,
    string? RequesterContactValue,
    string Subject,
    string Body);

public record SendEmailReplyRequest(string Body, string ChangedBy, IReadOnlyList<Guid>? AttachmentIds);
public record RecordEmailBounceRequest(Guid TicketMessageId, string Reason);
public record TicketAttachmentDto(Guid Id, string FileName, string ContentType, long SizeBytes, string UploadedByName, DateTimeOffset UploadedAtUtc);
public record TicketMessageDeliveryStatusDto(Guid Id, Guid TicketMessageId, string Status, string? Detail, DateTimeOffset OccurredAtUtc);
```

**Create file: `src/SupportCrm.Application/Tickets/TicketIngestionService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

/// <summary>
/// Shared, channel-agnostic entry point every Communication Channels story's inbound
/// webhook calls. Resolves the customer, then reuses an existing OPEN ticket for that
/// customer instead of always creating a new one — this is what makes CC-6's "switching
/// channels mid-conversation does not create a duplicate ticket" true by construction.
/// </summary>
public class TicketIngestionService(
    ITicketRepository ticketRepository,
    TicketService ticketService,
    TicketCustomerResolver customerResolver,
    ITicketMessageRepository messageRepository,
    TimeProvider timeProvider)
{
    public async Task<Ticket> IngestInboundMessageAsync(IngestInboundMessageRequest request, CancellationToken ct)
    {
        var customerId = await customerResolver.ResolveCustomerIdAsync(request.RequesterName, request.RequesterContactValue, ct);
        var ticket = await ticketRepository.FindOpenTicketForCustomerAsync(customerId, ct);

        if (ticket is null)
        {
            var created = await ticketService.CreateAsync(
                new CreateTicketRequest(request.Channel, request.Subject, request.Body, request.RequesterName, request.RequesterContactValue, "System"), ct);
            ticket = await ticketRepository.GetByIdAsync(created.Id, ct);
        }

        var message = new TicketMessage(ticket!.Id, request.Body, request.RequesterName, "Customer", timeProvider.GetUtcNow());
        message.SetChannel(request.Channel);
        await messageRepository.AddMessageAsync(message, ct);
        await messageRepository.SaveChangesAsync(ct);

        return ticket;
    }
}
```

**Design note for the executor:** when `ticket` was just created by `TicketService.CreateAsync`, this method still adds a **second** `TicketMessage` for the same inbound content that also became the ticket's `Subject`/`Description`. This is deliberate, not redundant: the ticket's `Subject`/`Description` are summary fields (shown in the header), while the `TicketMessage` is what makes the content appear in the unified timeline (TM-5) as the conversation's first entry — without it, a brand-new ticket's first message would be invisible in the timeline until a second message arrived.

**Create file: `src/SupportCrm.Application/Tickets/IEmailSender.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

/// <summary>
/// Sends an outbound email reply. No real mailbox/SMTP provider exists in this codebase —
/// register <see cref="MockEmailSender"/> until one does. Returns a fake provider message id.
/// </summary>
public interface IEmailSender
{
    Task<string> SendReplyAsync(string toAddress, string subject, string body, IReadOnlyList<TicketAttachmentDto> attachments, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Tickets/MockEmailSender.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using Microsoft.Extensions.Logging;

public class MockEmailSender(ILogger<MockEmailSender> logger) : IEmailSender
{
    public Task<string> SendReplyAsync(string toAddress, string subject, string body, IReadOnlyList<TicketAttachmentDto> attachments, CancellationToken ct)
    {
        var fakeMessageId = $"mock-email-{Guid.NewGuid():N}";
        logger.LogInformation("Mock email send: to={To} subject={Subject} attachments={AttachmentCount} providerMessageId={MessageId}",
            toAddress, subject, attachments.Count, fakeMessageId);
        return Task.FromResult(fakeMessageId);
    }
}
```

**Create file: `src/SupportCrm.Application/Tickets/ITicketAttachmentStorage.cs`** — mirrors Customer Management's `IAttachmentStorage` (CM-4) but ticket-scoped, per the Context note above:

```csharp
namespace SupportCrm.Application.Tickets;

public interface ITicketAttachmentStorage
{
    Task<string> SaveAsync(Guid ticketId, Guid attachmentId, string fileName, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Tickets/TicketAttachmentService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketAttachmentService(
    ITicketRepository ticketRepository,
    ITicketAttachmentRepository attachmentRepository,
    ITicketAttachmentStorage storage,
    TimeProvider timeProvider)
{
    public async Task<TicketAttachmentDto> AddAsync(Guid ticketId, string fileName, string contentType, long sizeBytes, Stream content, string uploadedByName, CancellationToken ct)
    {
        _ = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());

        var attachmentId = Guid.NewGuid();
        var storageKey = await storage.SaveAsync(ticketId, attachmentId, fileName, content, ct);

        var attachment = new TicketAttachment(ticketId, fileName, contentType, sizeBytes, storageKey, uploadedByName, timeProvider.GetUtcNow());
        await attachmentRepository.AddAsync(attachment, ct);
        await attachmentRepository.SaveChangesAsync(ct);
        return ToDto(attachment);
    }

    public async Task<IReadOnlyList<TicketAttachmentDto>> GetForTicketAsync(Guid ticketId, CancellationToken ct) =>
        (await attachmentRepository.GetByTicketAsync(ticketId, ct)).Select(ToDto).ToList();

    public async Task<(Stream Content, TicketAttachment Attachment)> OpenAsync(Guid attachmentId, CancellationToken ct)
    {
        var attachment = await attachmentRepository.GetByIdAsync(attachmentId, ct) ?? throw new KeyNotFoundException($"Attachment '{attachmentId}' was not found.");
        var stream = await storage.OpenReadAsync(attachment.StorageKey, ct);
        return (stream, attachment);
    }

    private static TicketAttachmentDto ToDto(TicketAttachment a) => new(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByName, a.UploadedAtUtc);
}
```

**Create file: `src/SupportCrm.Application/Tickets/EmailChannelService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class EmailChannelService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ITicketAttachmentRepository attachmentRepository,
    IEmailSender emailSender,
    TimeProvider timeProvider)
{
    public async Task<TicketMessageDto> SendReplyAsync(Guid ticketId, SendEmailReplyRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        if (string.IsNullOrWhiteSpace(ticket.RequesterContactValue))
            throw new InvalidOperationException("This ticket has no requester contact value to email.");

        var attachments = request.AttachmentIds is { Count: > 0 }
            ? (await attachmentRepository.GetByTicketAsync(ticketId, ct))
                .Where(a => request.AttachmentIds.Contains(a.Id))
                .Select(a => new TicketAttachmentDto(a.Id, a.FileName, a.ContentType, a.SizeBytes, a.UploadedByName, a.UploadedAtUtc))
                .ToList()
            : new List<TicketAttachmentDto>();

        await emailSender.SendReplyAsync(ticket.RequesterContactValue, ticket.Subject, request.Body, attachments, ct);

        var now = timeProvider.GetUtcNow();
        var message = new TicketMessage(ticketId, request.Body, request.ChangedBy, "Agent", now);
        message.SetChannel(TicketChannel.Email);
        await messageRepository.AddMessageAsync(message, ct);
        await messageRepository.AddDeliveryStatusAsync(new TicketMessageDeliveryStatus(message.Id, "Sent", null, now), ct);
        await messageRepository.SaveChangesAsync(ct);

        return new TicketMessageDto(message.Id, message.Body, message.AuthorName, message.AuthorKind, message.CreatedAtUtc);
    }

    public async Task RecordBounceAsync(RecordEmailBounceRequest request, CancellationToken ct)
    {
        var message = await messageRepository.GetMessageByIdAsync(request.TicketMessageId, ct)
            ?? throw new KeyNotFoundException($"Ticket message '{request.TicketMessageId}' was not found.");
        await messageRepository.AddDeliveryStatusAsync(
            new TicketMessageDeliveryStatus(message.Id, "Bounced", request.Reason, timeProvider.GetUtcNow()), ct);
        await messageRepository.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TicketMessageDeliveryStatusDto>> GetDeliveryStatusesAsync(Guid ticketId, CancellationToken ct) =>
        (await messageRepository.GetDeliveryStatusesAsync(ticketId, ct))
            .Select(s => new TicketMessageDeliveryStatusDto(s.Id, s.TicketMessageId, s.Status, s.Detail, s.OccurredAtUtc))
            .ToList();
}
```

### 3 — Infrastructure: EF config, repositories, local-disk storage, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet<TicketAttachment>`, `DbSet<TicketMessageDeliveryStatus>`; extend the `TicketMessage` entity block with `entity.Property(m => m.Channel).HasConversion<string?>();`; add `OnModelCreating` blocks for the two new entities following the file's existing style.

**File: `src/SupportCrm.Infrastructure/Persistence/TicketRepository.cs`** — implement `FindOpenTicketForCustomerAsync`:

```csharp
    public Task<Ticket?> FindOpenTicketForCustomerAsync(Guid customerId, CancellationToken ct) =>
        dbContext.Tickets
            .Where(t => t.CustomerId == customerId && OpenStatuses.Contains(t.Status))
            .OrderByDescending(t => t.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
```

**File: `src/SupportCrm.Infrastructure/Persistence/TicketMessageRepository.cs`** — implement the 3 new members:

```csharp
    public Task<TicketMessage?> GetMessageByIdAsync(Guid messageId, CancellationToken ct) =>
        dbContext.TicketMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);

    public Task AddDeliveryStatusAsync(TicketMessageDeliveryStatus status, CancellationToken ct)
    {
        dbContext.TicketMessageDeliveryStatuses.Add(status);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<TicketMessageDeliveryStatus>> GetDeliveryStatusesAsync(Guid ticketId, CancellationToken ct) =>
        await dbContext.TicketMessageDeliveryStatuses
            .Where(s => dbContext.TicketMessages.Any(m => m.Id == s.TicketMessageId && m.TicketId == ticketId))
            .ToListAsync(ct);
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/TicketAttachmentRepository.cs`** — straightforward EF implementation of `ITicketAttachmentRepository`, mirroring `NoteAndAttachmentRepository`'s attachment half (CM-4).

**Create file: `src/SupportCrm.Infrastructure/Storage/LocalDiskTicketAttachmentStorage.cs`** — mirrors `LocalDiskAttachmentStorage` (CM-4) exactly, but stores under a separate configured root (e.g. `App_Data/ticket-attachments`) and its own options class `LocalDiskTicketAttachmentStorageOptions` (`RootPath`), registered from the same `Attachments` config section's sibling — or its own `TicketAttachments` section; either is fine, pick one and be consistent with the naming.

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add registrations for `ITicketAttachmentRepository`, `ITicketAttachmentStorage → LocalDiskTicketAttachmentStorage`, `TicketAttachmentService`, `IEmailSender → MockEmailSender`, `TicketIngestionService`, `EmailChannelService`.

- After creating these files, run `dotnet ef migrations add AddEmailChannel --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: webhook controller + ticket controller additions

**Create file: `src/SupportCrm.Api/Controllers/ChannelsController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/channels/email")]
public class ChannelsController(TicketIngestionService ingestionService, EmailChannelService emailChannelService) : ControllerBase
{
    // Stub webhook: stands in for a real provider's inbound-parse callback (e.g. SendGrid
    // Inbound Parse, Mailgun Routes). Accepts multipart/form-data the way those providers do.
    [HttpPost("inbound")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Inbound(
        [FromForm] string fromAddress, [FromForm] string fromName, [FromForm] string subject, [FromForm] string body,
        CancellationToken ct)
    {
        var ticket = await ingestionService.IngestInboundMessageAsync(
            new IngestInboundMessageRequest(SupportCrm.Domain.Entities.TicketChannel.Email, fromName, fromAddress, subject, body), ct);
        return Ok(new { ticketId = ticket.Id, referenceNumber = ticket.ReferenceNumber });
    }

    // Stub webhook: stands in for a real provider's bounce/undeliverable callback.
    [HttpPost("bounce")]
    public async Task<IActionResult> Bounce([FromBody] RecordEmailBounceRequest request, CancellationToken ct)
    {
        try { await emailChannelService.RecordBounceAsync(request, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
```

**Note for the executor:** this story's inbound endpoint does not yet accept file attachments in the same request — per the AC, inbound attachments must be preserved too. Extend the `[FromForm]` parameters with `IFormFileCollection? attachments`, save each via `TicketAttachmentService.AddAsync` against the resulting `ticket.Id` after ingestion, in the same action. This was simplified out of the signature above for readability; it is required for AC completeness, not optional.

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add:

```csharp
    [HttpPost("{id:guid}/email-replies")]
    public async Task<ActionResult<TicketMessageDto>> SendEmailReply(Guid id, [FromBody] SendEmailReplyRequest request, [FromServices] EmailChannelService emailChannelService, CancellationToken ct)
    {
        try { return await emailChannelService.SendReplyAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:guid}/delivery-statuses")]
    public async Task<ActionResult<IReadOnlyList<TicketMessageDeliveryStatusDto>>> GetDeliveryStatuses(Guid id, [FromServices] EmailChannelService emailChannelService, CancellationToken ct) =>
        Ok(await emailChannelService.GetDeliveryStatusesAsync(id, ct));

    [HttpPost("{id:guid}/attachments")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TicketAttachmentDto>> UploadAttachment(Guid id, IFormFile file, [FromQuery] string? uploadedByName, [FromServices] TicketAttachmentService attachmentService, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("A file is required.");
        try
        {
            await using var stream = file.OpenReadStream();
            return await attachmentService.AddAsync(id, file.FileName, file.ContentType, file.Length, stream, uploadedByName ?? "unknown", ct);
        }
        catch (TicketNotFoundException) { return NotFound(); }
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IReadOnlyList<TicketAttachmentDto>>> GetAttachments(Guid id, [FromServices] TicketAttachmentService attachmentService, CancellationToken ct) =>
        Ok(await attachmentService.GetForTicketAsync(id, ct));

    [HttpGet("attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> DownloadAttachment(Guid attachmentId, [FromServices] TicketAttachmentService attachmentService, CancellationToken ct)
    {
        try
        {
            var (content, attachment) = await attachmentService.OpenAsync(attachmentId, ct);
            return File(content, attachment.ContentType, attachment.FileName);
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }
```

---

## Edge Cases & Failure Modes

- **Second inbound email arrives while the first ticket is still open** — `TicketIngestionService.IngestInboundMessageAsync` finds the existing open ticket via `FindOpenTicketForCustomerAsync` and appends a message instead of creating a second ticket — this is the core behavior the whole story exists to deliver; verify it directly in tests, don't just assume the query is right.
- **Customer's only open ticket was just Closed/Resolved between the resolver check and the message append** — not handled with a transaction/lock in this story; a rare race could append a message to a ticket that closed a moment ago. Documented as an accepted gap given the low collision likelihood for a mocked, non-concurrent-provider scenario.
- **Emailing a reply when the ticket has no `RequesterContactValue`** (e.g. it was created manually by an agent with no contact info) — `EmailChannelService.SendReplyAsync` throws `InvalidOperationException` → `400`, rather than silently no-op sending.
- **Bounce reported for an unknown `TicketMessageId`** — `KeyNotFoundException` → `404`.
- **Attachment upload for an unknown ticket** — `TicketNotFoundException` → `404`.
- **Inbound webhook with no attachments** — the `IFormFileCollection?` is null/empty; no attachment-save loop runs, per the executor note above.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketIngestionServiceTests.cs`**:
   - `IngestInboundMessageAsync_NoOpenTicket_CreatesNewTicketAndMessage`
   - `IngestInboundMessageAsync_ExistingOpenTicket_AppendsMessageWithoutCreatingNewTicket`
   - `IngestInboundMessageAsync_SetsMessageChannel`
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/EmailChannelServiceTests.cs`**:
   - `SendReplyAsync_NoRequesterContactValue_Throws`
   - `SendReplyAsync_RecordsSentDeliveryStatus`
   - `RecordBounceAsync_UnknownMessage_Throws`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/ChannelsControllerTests.cs`**:
   - `Post_InboundEmail_TwiceForSameCustomer_UpdatesSameTicket`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddEmailChannel --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.
3. **Manual smoke:** POST two inbound emails from the same address a few seconds apart; confirm the second one appends to the same ticket (`GET /api/tickets/{id}/timeline` shows two Message entries, `GET /api/tickets/reference/.../status` still shows one ticket).

---

## Done Criteria

- [ ] An inbound email (webhook stub) creates a ticket, or appends to the customer's existing open ticket.
- [ ] An agent can send a reply (mock send + recorded as an Agent `TicketMessage`, `Channel: Email`).
- [ ] Attachments upload/list/download at the ticket level, both directions.
- [ ] A bounce can be recorded and is visible via `GET /api/tickets/{id}/delivery-statuses`.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
