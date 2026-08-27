# Story 30 — Ticket summaries (Story: AI-1)

---

## Prerequisites

- Ticket Management Stories 05–09 completed ([`../ticket-management/05-story-TM-1.md`](../ticket-management/05-story-TM-1.md) .. [`09-story-TM-5.md`](../ticket-management/09-story-TM-5.md)) — provide `Ticket`, `TicketMessage`/`TicketNote`, `ITicketMessageRepository`.

---

## Story Goal

1. A configurable message-count threshold (`AiFeaturesOptions.SummaryThresholdMessageCount`, default 5) that the frontend uses to decide when to show a "Summarize" option — exposed via a new lightweight message-count endpoint.
2. An on-demand, regeneratable AI summary per ticket (extractive/heuristic, not generative) capturing the customer's issue, key actions, and current status.
3. The summary is clearly a separate artifact (its own table, its own DTO with a `generatedAtUtc`/`sourceMessageCount`) — the original `TicketMessage` thread is never touched.

**Not in scope:** a real LLM integration — `MockAiSummaryProvider` is the full and final "AI" here.

---

## Context — Read These Files First

1. `src/SupportCrm.Domain/Repositories/ITicketMessageRepository.cs` (all 15 lines) — `GetMessagesAsync`/`GetNotesAsync`, the summary's input data; this story adds `CountByTicketAsync`.
2. `src/SupportCrm.Domain/Entities/TicketMessage.cs` (all 31 lines) — `AuthorKind` ("Customer"|"Agent"|"System"), the field the mock provider uses to separate "the customer's issue" from "key actions taken."
3. `src/SupportCrm.Infrastructure/Storage/LocalDiskAttachmentStorage.cs`, lines 6–10 (`LocalDiskAttachmentStorageOptions`) — the exact `SectionName` constant + POCO-options shape `AiFeaturesOptions` follows.
4. `src/SupportCrm.Api/Program.cs`, lines 30–31 (the two existing `Configure<...Options>` calls) — insertion point for `AiFeaturesOptions`'s registration.
5. `src/SupportCrm.Api/Controllers/TicketsController.cs`, lines 123–140 (`SetStatus`/`Escalate`/`GetEscalations`) — precedent for adding ticket-scoped AI actions directly onto this controller.

---

## Backend Tasks

### 1 — Domain: `TicketAiSummary`, `ITicketMessageRepository` extension

**Create file: `src/SupportCrm.Domain/Entities/TicketAiSummary.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// One row per ticket (upserted on regenerate) — the AC asks for "the summary," current and
// singular, not a version history (unlike Knowledge Base's ContentVersionEntry).
public class TicketAiSummary
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string SummaryText { get; private set; } = default!;
    public int SourceMessageCount { get; private set; }
    public DateTimeOffset GeneratedAtUtc { get; private set; }

    private TicketAiSummary() { } // EF Core

    public TicketAiSummary(Guid ticketId, string summaryText, int sourceMessageCount, DateTimeOffset generatedAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        SummaryText = summaryText;
        SourceMessageCount = sourceMessageCount;
        GeneratedAtUtc = generatedAtUtc;
    }

    public void Regenerate(string summaryText, int sourceMessageCount, DateTimeOffset generatedAtUtc)
    {
        SummaryText = summaryText;
        SourceMessageCount = sourceMessageCount;
        GeneratedAtUtc = generatedAtUtc;
    }
}
```

**Extend file: `src/SupportCrm.Domain/Repositories/ITicketMessageRepository.cs`** — add:

```csharp
    Task<int> CountByTicketAsync(Guid ticketId, CancellationToken ct);
```

**Create file: `src/SupportCrm.Domain/Repositories/ITicketAiSummaryRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketAiSummaryRepository
{
    Task<TicketAiSummary?> GetByTicketAsync(Guid ticketId, CancellationToken ct);
    Task AddAsync(TicketAiSummary summary, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: `AiFeaturesOptions`, `IAiSummaryProvider`, `MockAiSummaryProvider`, `TicketSummaryService`

**Create file: `src/SupportCrm.Application/Ai/AiFeaturesOptions.cs`**

```csharp
namespace SupportCrm.Application.Ai;

// Shared config for every AI Features story — one options class, not one per story.
public class AiFeaturesOptions
{
    public const string SectionName = "AiFeatures";
    public int SummaryThresholdMessageCount { get; set; } = 5;
    public int CategorizationConfidenceThresholdPercentage { get; set; } = 60; // set by Story 32
}
```

**Create file: `src/SupportCrm.Application/Ai/AiDtos.cs`**

```csharp
namespace SupportCrm.Application.Ai;

public record TicketAiSummaryDto(Guid TicketId, string SummaryText, int SourceMessageCount, DateTimeOffset GeneratedAtUtc, bool IsAiGenerated = true);

public class TicketNotFoundForAiException(string id) : Exception($"Ticket '{id}' was not found.");
```

`IsAiGenerated` defaults `true` and always serializes as `true` — it exists purely so the DTO itself carries the "clearly labeled as AI-generated" signal end-to-end, not just a frontend-side assumption.

**Create file: `src/SupportCrm.Application/Ai/IAiSummaryProvider.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;

/// <summary>
/// Produces a ticket summary. No real LLM exists in this codebase — register
/// <see cref="MockAiSummaryProvider"/> until one does. That implementation is
/// extractive/heuristic (picks the first customer message, counts agent replies,
/// states current status) — it does not call any external AI service.
/// </summary>
public interface IAiSummaryProvider
{
    string Summarize(Ticket ticket, IReadOnlyList<TicketMessage> messages, IReadOnlyList<TicketNote> notes);
}
```

**Create file: `src/SupportCrm.Application/Ai/MockAiSummaryProvider.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Entities;

public class MockAiSummaryProvider : IAiSummaryProvider
{
    public string Summarize(Ticket ticket, IReadOnlyList<TicketMessage> messages, IReadOnlyList<TicketNote> notes)
    {
        var ordered = messages.OrderBy(m => m.CreatedAtUtc).ToList();
        var firstCustomerMessage = ordered.FirstOrDefault(m => m.AuthorKind == "Customer");
        var agentReplyCount = ordered.Count(m => m.AuthorKind == "Agent");

        var issue = firstCustomerMessage is not null
            ? Truncate(firstCustomerMessage.Body, 240)
            : Truncate(ticket.Description ?? ticket.Subject, 240);

        return $"Customer issue: {issue} " +
               $"Agent activity: {agentReplyCount} repl{(agentReplyCount == 1 ? "y" : "ies")} so far, {notes.Count} internal note(s). " +
               $"Current status: {ticket.Status} (priority {ticket.Priority}).";
    }

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "…";
}
```

**Create file: `src/SupportCrm.Application/Ai/TicketSummaryService.cs`**

```csharp
namespace SupportCrm.Application.Ai;

using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;

public class TicketSummaryService(
    ITicketRepository ticketRepository,
    ITicketMessageRepository messageRepository,
    ITicketAiSummaryRepository summaryRepository,
    IAiSummaryProvider summaryProvider,
    TimeProvider timeProvider)
{
    public async Task<TicketAiSummaryDto?> GetAsync(Guid ticketId, CancellationToken ct)
    {
        var summary = await summaryRepository.GetByTicketAsync(ticketId, ct);
        return summary is null ? null : ToDto(summary);
    }

    public async Task<TicketAiSummaryDto> GenerateAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundForAiException(ticketId.ToString());
        var messages = await messageRepository.GetMessagesAsync(ticketId, ct);
        var notes = await messageRepository.GetNotesAsync(ticketId, ct);

        var summaryText = summaryProvider.Summarize(ticket, messages, notes);
        var now = timeProvider.GetUtcNow();

        var existing = await summaryRepository.GetByTicketAsync(ticketId, ct);
        if (existing is null)
        {
            var created = new Domain.Entities.TicketAiSummary(ticketId, summaryText, messages.Count, now);
            await summaryRepository.AddAsync(created, ct);
            await summaryRepository.SaveChangesAsync(ct);
            return ToDto(created);
        }

        existing.Regenerate(summaryText, messages.Count, now);
        await summaryRepository.SaveChangesAsync(ct);
        return ToDto(existing);
    }

    private static TicketAiSummaryDto ToDto(Domain.Entities.TicketAiSummary s) => new(s.TicketId, s.SummaryText, s.SourceMessageCount, s.GeneratedAtUtc);
}
```

### 3 — Infrastructure: EF config, repositories, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add a `DbSet` after the last existing one:

```csharp
    public DbSet<TicketAiSummary> TicketAiSummaries => Set<TicketAiSummary>();
```

Add an `OnModelCreating` block after the last existing one:

```csharp

        modelBuilder.Entity<TicketAiSummary>(entity =>
        {
            entity.ToTable("TicketAiSummaries");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.SummaryText).IsRequired();
            entity.HasIndex(s => s.TicketId).IsUnique();
        });
```

**File: `src/SupportCrm.Infrastructure/Persistence/TicketMessageRepository.cs`** — add:

```csharp
    public Task<int> CountByTicketAsync(Guid ticketId, CancellationToken ct) =>
        dbContext.TicketMessages.CountAsync(m => m.TicketId == ticketId, ct);
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/TicketAiSummaryRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketAiSummaryRepository(SupportCrmDbContext dbContext) : ITicketAiSummaryRepository
{
    public Task<TicketAiSummary?> GetByTicketAsync(Guid ticketId, CancellationToken ct) =>
        dbContext.TicketAiSummaries.FirstOrDefaultAsync(s => s.TicketId == ticketId, ct);

    public Task AddAsync(TicketAiSummary summary, CancellationToken ct)
    {
        dbContext.TicketAiSummaries.Add(summary);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.Configure<AiFeaturesOptions>(configuration.GetSection(AiFeaturesOptions.SectionName));
        services.AddScoped<ITicketAiSummaryRepository, TicketAiSummaryRepository>();
        services.AddScoped<IAiSummaryProvider, MockAiSummaryProvider>();
        services.AddScoped<TicketSummaryService>();
```

(Add `using SupportCrm.Application.Ai;` to this file's `using` block. Note: `services.Configure<AiFeaturesOptions>` binds directly here rather than in `Program.cs`, since `AddInfrastructure` already receives `IConfiguration` — unlike the two storage-options classes, which are bound in `Program.cs` because their registration lives in `Infrastructure.Storage`, a different assembly boundary; either location is valid, this one avoids an extra `Program.cs` edit.)

**File: `src/SupportCrm.Api/appsettings.json`** — add a top-level section:

```json
  "AiFeatures": {
    "SummaryThresholdMessageCount": 5,
    "CategorizationConfidenceThresholdPercentage": 60
  }
```

- After creating these files, run `dotnet ef migrations add AddAiSummaries --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: `TicketsController` additions

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add, and add `using SupportCrm.Application.Ai;`:

```csharp

    [HttpGet("{id:guid}/message-count")]
    public async Task<ActionResult<int>> GetMessageCount(Guid id, [FromServices] ITicketMessageRepository messageRepository, CancellationToken ct) =>
        Ok(await messageRepository.CountByTicketAsync(id, ct));

    [HttpGet("{id:guid}/ai-summary")]
    public async Task<ActionResult<TicketAiSummaryDto>> GetAiSummary(Guid id, [FromServices] TicketSummaryService summaryService, CancellationToken ct)
    {
        var summary = await summaryService.GetAsync(id, ct);
        return summary is null ? NotFound() : summary;
    }

    [HttpPost("{id:guid}/ai-summary")]
    public async Task<ActionResult<TicketAiSummaryDto>> GenerateAiSummary(Guid id, [FromServices] TicketSummaryService summaryService, CancellationToken ct)
    {
        try { return await summaryService.GenerateAsync(id, ct); }
        catch (TicketNotFoundForAiException) { return NotFound(); }
    }
```

---

## Edge Cases & Failure Modes

- **`GET .../ai-summary` before any summary has been generated** — `GetAsync` returns `null`; the controller maps that to `404`, not an empty/default DTO — the frontend uses this to decide whether to show "Generate" vs. "Regenerate."
- **`POST .../ai-summary` on a ticket with zero messages** — `Summarize` falls back to `ticket.Description ?? ticket.Subject` when no customer message exists yet; never throws on an empty message list.
- **Regenerating repeatedly** — `Regenerate` mutates the existing row in place (one row per ticket, enforced by the unique index on `TicketId`), never accumulates a growing history table.
- **Unknown ticket id** — `TicketNotFoundForAiException` → `404` on `POST`; `GET` never even reaches ticket lookup since it queries the summary table directly by `ticketId` (a summary for a deleted/nonexistent ticket simply doesn't exist, same `404`).

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Ai/MockAiSummaryProviderTests.cs`**:
   - `Summarize_NoCustomerMessage_FallsBackToTicketDescription`
   - `Summarize_CountsOnlyAgentAuthoredMessages`
2. **Unit — `tests/SupportCrm.Application.Tests/Ai/TicketSummaryServiceTests.cs`**:
   - `GenerateAsync_FirstCall_CreatesRow`
   - `GenerateAsync_SecondCall_RegeneratesSameRow`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/TicketsControllerAiSummaryTests.cs`**:
   - `Get_BeforeGenerate_Returns404`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddAiSummaries --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] `GET /api/tickets/{id}/message-count` lets the frontend decide when to show the summary option.
- [ ] `POST /api/tickets/{id}/ai-summary` generates/regenerates a summary; `GET` fetches the current one (`404` if none).
- [ ] The summary captures issue/actions/status and is clearly flagged `isAiGenerated: true`.
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 31.**
