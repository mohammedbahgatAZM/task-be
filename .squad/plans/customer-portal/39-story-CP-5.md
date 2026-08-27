# Story 39 — Submit feedback (Story: CP-5)

---

## Prerequisites

- Story 37 completed: [`37-story-CP-3.md`](37-story-CP-3.md) — `CustomerPortalOptions` (this story adds `LowRatingThreshold`, already stubbed onto that class).
- SLA & Automation Story 23 completed ([`../sla-automation/23-story-SA-3.md`](../sla-automation/23-story-SA-3.md)) — `Agent.IsSupervisor`.
- Agent Dashboard Story 18 completed ([`../agent-dashboard/18-story-AD-3.md`](../agent-dashboard/18-story-AD-3.md)) — `TicketTaskService.CreateAsync`/`CreateTicketTaskRequest`, reused verbatim.

---

## Story Goal

1. `POST /api/tickets/{id}/feedback` — one rating (1–5) + optional comment per ticket, ownership-checked, write-once.
2. `GET /api/tickets/{id}/feedback` — unrestricted read (no RBAC exists anywhere in this codebase).
3. A rating at or below a configurable threshold creates one `TicketTask` for the first staffed supervisor — reusing Agent Dashboard AD-3's task entity directly, not a new one.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Tickets/TicketTaskService.cs` (all ~45 lines) — `CreateAsync(Guid ticketId, CreateTicketTaskRequest request, CancellationToken ct)`, called with `changedBy`/`createdBy: "System"`.
2. `src/SupportCrm.Application/Tickets/TicketTaskDtos.cs`, lines 3–4 — `CreateTicketTaskRequest(string Note, DateTimeOffset DueAtUtc, Guid AssignedAgentId, string CreatedBy)`, the exact shape this story's follow-up task is built from.
3. `src/SupportCrm.Application/Tickets/AssignmentRuleEngine.cs` / `src/SupportCrm.Application/Ai/TicketCategorizationService.cs` — precedent for `agentRepository.GetAllAsync(ct)` + in-memory `.Where(a => a.IsSupervisor)`/`.FirstOrDefault(...)` filtering (small agent counts, same pattern reused here, first-match not all-matches — see this story's own note on why that differs from SLA & Automation's "notify every supervisor" escalation behavior).

---

## Backend Tasks

### 1 — Domain: `TicketFeedback`

**Create file: `src/SupportCrm.Domain/Entities/TicketFeedback.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// One row per ticket, write-once (enforced at the service layer, not here) — a customer can't
// silently erase a low rating by resubmitting.
public class TicketFeedback
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset SubmittedAtUtc { get; private set; }

    private TicketFeedback() { } // EF Core

    public TicketFeedback(Guid ticketId, int rating, string? comment, DateTimeOffset submittedAtUtc)
    {
        if (rating is < 1 or > 5)
            throw new ArgumentException("Rating must be between 1 and 5.", nameof(rating));

        Id = Guid.NewGuid();
        TicketId = ticketId;
        Rating = rating;
        Comment = comment;
        SubmittedAtUtc = submittedAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ITicketFeedbackRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketFeedbackRepository
{
    Task<TicketFeedback?> GetByTicketAsync(Guid ticketId, CancellationToken ct);
    Task AddAsync(TicketFeedback feedback, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: `TicketFeedbackService`

**File: `src/SupportCrm.Application/CustomerPortal/CustomerPortalDtos.cs`** — append:

```csharp
public record SubmitTicketFeedbackRequest(Guid CustomerId, int Rating, string? Comment);
public record TicketFeedbackDto(Guid TicketId, int Rating, string? Comment, DateTimeOffset SubmittedAtUtc);
```

**Create file: `src/SupportCrm.Application/CustomerPortal/TicketFeedbackService.cs`**

```csharp
namespace SupportCrm.Application.CustomerPortal;

using Microsoft.Extensions.Options;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;

public class TicketFeedbackService(
    ITicketRepository ticketRepository,
    ITicketFeedbackRepository feedbackRepository,
    IAgentRepository agentRepository,
    TicketTaskService taskService,
    IOptions<CustomerPortalOptions> options,
    TimeProvider timeProvider)
{
    public async Task<TicketFeedbackDto> SubmitAsync(Guid ticketId, SubmitTicketFeedbackRequest request, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        if (ticket.CustomerId != request.CustomerId)
            throw new TicketOwnershipException(ticketId);

        if (await feedbackRepository.GetByTicketAsync(ticketId, ct) is not null)
            throw new InvalidOperationException("Feedback has already been submitted for this ticket.");

        var feedback = new TicketFeedback(ticketId, request.Rating, request.Comment?.Trim(), timeProvider.GetUtcNow());
        await feedbackRepository.AddAsync(feedback, ct);
        await feedbackRepository.SaveChangesAsync(ct);

        if (request.Rating <= options.Value.LowRatingThreshold)
            await CreateSupervisorFollowUpAsync(ticket, feedback, ct);

        return ToDto(feedback);
    }

    public async Task<TicketFeedbackDto?> GetAsync(Guid ticketId, CancellationToken ct)
    {
        var feedback = await feedbackRepository.GetByTicketAsync(ticketId, ct);
        return feedback is null ? null : ToDto(feedback);
    }

    // Assigns ONE supervisor, not all of them — a task needs a single clear owner, unlike SLA &
    // Automation's escalation tiers (Story 23), which deliberately notify every supervisor.
    private async Task CreateSupervisorFollowUpAsync(Ticket ticket, TicketFeedback feedback, CancellationToken ct)
    {
        var agents = await agentRepository.GetAllAsync(ct);
        var supervisor = agents.FirstOrDefault(a => a.IsSupervisor);
        if (supervisor is null) return; // no supervisor staffed — skipped, not queued/retried

        await taskService.CreateAsync(ticket.Id, new CreateTicketTaskRequest(
            $"Low CSAT rating ({feedback.Rating}/5) on ticket {ticket.ReferenceNumber} — follow up.",
            timeProvider.GetUtcNow().AddDays(1),
            supervisor.Id,
            "System"), ct);
    }

    private static TicketFeedbackDto ToDto(TicketFeedback f) => new(f.TicketId, f.Rating, f.Comment, f.SubmittedAtUtc);
}
```

### 3 — Infrastructure: EF config, repository, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add a `DbSet` after Story 38's:

```csharp
    public DbSet<TicketFeedback> TicketFeedback => Set<TicketFeedback>();
```

Add an `OnModelCreating` block after Story 38's:

```csharp

        modelBuilder.Entity<TicketFeedback>(entity =>
        {
            entity.ToTable("TicketFeedback");
            entity.HasKey(f => f.Id);
            entity.HasIndex(f => f.TicketId).IsUnique();
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/TicketFeedbackRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketFeedbackRepository(SupportCrmDbContext dbContext) : ITicketFeedbackRepository
{
    public Task<TicketFeedback?> GetByTicketAsync(Guid ticketId, CancellationToken ct) =>
        dbContext.TicketFeedback.FirstOrDefaultAsync(f => f.TicketId == ticketId, ct);

    public Task AddAsync(TicketFeedback feedback, CancellationToken ct)
    {
        dbContext.TicketFeedback.Add(feedback);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<ITicketFeedbackRepository, TicketFeedbackRepository>();
        services.AddScoped<TicketFeedbackService>();
```

- After creating these files, run `dotnet ef migrations add AddTicketFeedback --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: `TicketsController` additions

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add:

```csharp

    [HttpPost("{id:guid}/feedback")]
    public async Task<ActionResult<TicketFeedbackDto>> SubmitFeedback(Guid id, [FromBody] SubmitTicketFeedbackRequest request, [FromServices] TicketFeedbackService feedbackService, CancellationToken ct)
    {
        try { return await feedbackService.SubmitAsync(id, request, ct); }
        catch (TicketNotFoundException) { return NotFound(); }
        catch (TicketOwnershipException) { return Forbid(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("{id:guid}/feedback")]
    public async Task<ActionResult<TicketFeedbackDto>> GetFeedback(Guid id, [FromServices] TicketFeedbackService feedbackService, CancellationToken ct)
    {
        var feedback = await feedbackService.GetAsync(id, ct);
        return feedback is null ? NotFound() : feedback;
    }
```

---

## Edge Cases & Failure Modes

- **Rating outside 1–5** — rejected by `TicketFeedback`'s constructor (`ArgumentException` → `400`).
- **Submitting feedback twice for the same ticket** — the second call throws `InvalidOperationException` → `400` before any write, enforced by an existence check plus a unique index on `TicketId` as a second line of defense against a race.
- **Rating exactly at the threshold** — `<=` includes it (same inclusive-boundary convention as Story 37's reopen window and SLA & Automation's escalation tiers).
- **No agent is currently flagged `IsSupervisor`** — feedback still saves successfully; the follow-up task is silently skipped (not queued/retried) — flagged explicitly in the intake, not a bug to fix here.
- **Feedback submitted on a ticket that isn't `Resolved`** — this story does **not** enforce that server-side (the AC's "prompted when marked resolved" is a frontend gating decision, per the intake) — any ticket the caller owns can receive feedback; flagged as a deliberate scope boundary, not an oversight.
- **Ownership mismatch** — `TicketOwnershipException` → `403`, same pattern as Stories 36/37.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/TicketFeedbackTests.cs`**:
   - `Constructor_RatingOutOfRange_Throws`
2. **Unit — `tests/SupportCrm.Application.Tests/CustomerPortal/TicketFeedbackServiceTests.cs`**:
   - `SubmitAsync_SecondSubmission_Throws`
   - `SubmitAsync_LowRating_CreatesTaskForFirstSupervisor`
   - `SubmitAsync_LowRating_NoSupervisorStaffed_SkipsTaskWithoutThrowing`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/TicketsControllerFeedbackTests.cs`**:
   - `Post_Feedback_RatingOutOfRange_Returns400`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddTicketFeedback --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.

---

## Done Criteria

- [ ] `POST /api/tickets/{id}/feedback` accepts a 1–5 rating + optional comment, write-once, ownership-checked.
- [ ] `GET /api/tickets/{id}/feedback` is readable by anyone who can already see the ticket.
- [ ] A low rating creates exactly one `TicketTask` for a staffed supervisor.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
