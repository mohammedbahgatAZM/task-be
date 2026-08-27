# Story 24 — Alerts and notifications (Story: SA-4)

---

## Prerequisites

- Story 21 completed: [`21-story-SA-1.md`](21-story-SA-1.md) — provides `SlaCalculationService.GetStatusAsync`, the breach/remaining-time signal this story alerts on.
- Story 23 completed: [`23-story-SA-3.md`](23-story-SA-3.md) — provides `SlaEscalationHostedService`, the one recurring background job this story extends rather than duplicating (per its own intake note: *"reuse whatever minimal recurring-check mechanism SA-3 introduces rather than adding a second one"*), and `Ticket Repository.GetOpenAsync`, reused here unchanged.
- Agent Dashboard Story 20 completed ([`../agent-dashboard/20-story-AD-5.md`](../agent-dashboard/20-story-AD-5.md)) — provides `AgentNotificationService.NotifyAsync`, the real in-app delivery mechanism this story uses directly (not a stub) for the "in-app" half of the AC.
- Ticket Management Story 07 ([`../ticket-management/07-story-TM-3.md`](../ticket-management/07-story-TM-3.md)) and Story 08 ([`../ticket-management/08-story-TM-4.md`](../ticket-management/08-story-TM-4.md)) — precedent for this story's `ISlaAlertNotifier`/`NoOpSlaAlertNotifier` seam, which follows the identical shape as their `IAssignmentNotifier`/`NoOpAssignmentNotifier` and `ICustomerStatusNotifier`/`NoOpCustomerStatusNotifier`.

---

## Story Goal

1. In-app SLA warning/breach alerts are delivered for real (via the existing `AgentNotificationService`) the moment a ticket crosses its assigned agent's configured warning threshold, and again when it breaches — each at most once per ticket, not once per evaluation tick.
2. Email/push delivery is configurable but stubbed behind a no-op notifier seam (`ISlaAlertNotifier`), since no real email/push channel exists in this codebase — same documented gap as every other notifier here.
3. Each alert identifies the ticket (reference number + `RelatedTicketId`), its remaining time, and a frontend deep-link path (`/tickets/{id}`) the client resolves — not a hosted URL this backend serves.
4. Alert channels (email/push) and the warning threshold percentage are configurable per agent; any agent may additionally opt into a daily or weekly digest.
5. A digest of currently at-risk (breached or nearing-breach) tickets is available both on demand (`GET`) and via the same scheduled channel seam, on the cadence each subscriber configured.

**Assumption (no user/role/RBAC system exists yet):** alert preferences are keyed by `Agent` (same stand-in used throughout this codebase for "user"); nothing in the backend restricts digest opt-in to managers specifically — that's a UI/product convention on top of a preference any agent can set, consistent with every other "no real auth" gap already flagged in this codebase.

**Not in scope:** real email/push delivery infrastructure — only the seam + stub. Full RBAC/user-preference management. SLA calculation and escalation-trigger logic themselves (Stories 21, 23) — this story only delivers alerts about them.

---

## Context — Read These Files First

1. [`23-story-SA-3.md`](23-story-SA-3.md), `## Backend Tasks` → `### 2` — `SlaEscalationHostedService`'s exact current shape (`PeriodicTimer`, `IServiceScopeFactory`, one scope per tick); this story extends its `ExecuteAsync` loop, it does not create a second hosted service.
2. `src/SupportCrm.Application/Tickets/AgentNotificationService.cs`, lines 30–34 — `NotifyAsync(agentId, kind, message, relatedTicketId, ct)`, called directly (not through a seam) for the real in-app delivery half of this story.
3. `src/SupportCrm.Application/Tickets/IAssignmentNotifier.cs` and `NoOpAssignmentNotifier.cs` (both files, ~12 and ~7 lines) — the exact seam-with-stub shape `ISlaAlertNotifier`/`NoOpSlaAlertNotifier` follows.
4. [`21-story-SA-1.md`](21-story-SA-1.md), `## Backend Tasks` → `### 2` — `TicketSlaStatusDto`'s fields (`ResolutionRemainingMinutes`, `ResolutionTargetMinutes`, `IsResolutionBreached`); this story derives the same `elapsedPercentage` formula Story 23 uses (`100 - ResolutionRemainingMinutes * 100 / ResolutionTargetMinutes`).
5. `src/SupportCrm.Domain/Entities/Ticket.cs`, lines 1–20 — `AssignedAgentId`, `ReferenceNumber`; alerts are only ever sent for tickets that currently have an assigned agent — an unassigned ticket has no one to alert individually (it still counts toward the manager-facing at-risk digest).
6. `src/SupportCrm.Infrastructure/DependencyInjection.cs`, line 52 — `services.AddScoped<ICustomerStatusNotifier, SmsCustomerStatusNotifier>(); // replaces NoOpCustomerStatusNotifier (CC-4)` — precedent showing a no-op seam is expected to later be swapped for a real implementation without touching its consumers; this story registers the `NoOp` variant, same as that line originally did.

---

## Backend Tasks

### 1 — Domain: `AlertPreference`, `DigestFrequency`, `SlaAlertLog`, `DigestLogEntry`

**Create file: `src/SupportCrm.Domain/Entities/DigestFrequency.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public enum DigestFrequency
{
    None,
    Daily,
    Weekly
}
```

**Create file: `src/SupportCrm.Domain/Entities/AlertPreference.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class AlertPreference
{
    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public bool EmailEnabled { get; private set; }
    public bool PushEnabled { get; private set; }
    public int WarningThresholdPercentage { get; private set; } = 80;
    public DigestFrequency DigestFrequency { get; private set; } = DigestFrequency.None;

    private AlertPreference() { } // EF Core

    public AlertPreference(Guid agentId)
    {
        Id = Guid.NewGuid();
        AgentId = agentId;
    }

    public void Update(bool emailEnabled, bool pushEnabled, int warningThresholdPercentage, DigestFrequency digestFrequency)
    {
        if (warningThresholdPercentage is <= 0 or > 100)
            throw new ArgumentException("Warning threshold must be between 1 and 100.", nameof(warningThresholdPercentage));
        EmailEnabled = emailEnabled;
        PushEnabled = pushEnabled;
        WarningThresholdPercentage = warningThresholdPercentage;
        DigestFrequency = digestFrequency;
    }
}
```

**Create file: `src/SupportCrm.Domain/Entities/SlaAlertLog.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// One row per (ticket, kind) ever sent — the dedupe guard so a ticket's Warning/Breach alert
// fires exactly once each, not once per SlaEscalationHostedService tick.
public class SlaAlertLog
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string Kind { get; private set; } = default!; // "Warning" | "Breach"
    public DateTimeOffset SentAtUtc { get; private set; }

    private SlaAlertLog() { } // EF Core

    public SlaAlertLog(Guid ticketId, string kind, DateTimeOffset sentAtUtc)
    {
        Id = Guid.NewGuid();
        TicketId = ticketId;
        Kind = kind;
        SentAtUtc = sentAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Entities/DigestLogEntry.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class DigestLogEntry
{
    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public DateTimeOffset SentAtUtc { get; private set; }

    private DigestLogEntry() { } // EF Core

    public DigestLogEntry(Guid agentId, DateTimeOffset sentAtUtc)
    {
        Id = Guid.NewGuid();
        AgentId = agentId;
        SentAtUtc = sentAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IAlertPreferenceRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IAlertPreferenceRepository
{
    Task<AlertPreference?> GetByAgentIdAsync(Guid agentId, CancellationToken ct);
    Task<IReadOnlyList<AlertPreference>> GetWithDigestEnabledAsync(CancellationToken ct);
    Task UpsertAsync(AlertPreference preference, CancellationToken ct);
    Task<bool> HasAlertBeenSentAsync(Guid ticketId, string kind, CancellationToken ct);
    Task AddAlertLogAsync(SlaAlertLog entry, CancellationToken ct);
    Task<DateTimeOffset?> GetLastDigestSentAsync(Guid agentId, CancellationToken ct);
    Task AddDigestLogAsync(DigestLogEntry entry, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, `ISlaAlertNotifier`/`NoOpSlaAlertNotifier`, `SlaAlertService`

**Create file: `src/SupportCrm.Application/Tickets/SlaAlertDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;

public record AlertPreferenceDto(Guid AgentId, bool EmailEnabled, bool PushEnabled, int WarningThresholdPercentage, DigestFrequency DigestFrequency);
public record SetAlertPreferenceRequest(bool EmailEnabled, bool PushEnabled, int WarningThresholdPercentage, DigestFrequency DigestFrequency);
public record AtRiskTicketDto(Guid TicketId, string ReferenceNumber, TicketPriority Priority, int ResolutionRemainingMinutes, bool IsBreached, string DeepLinkPath);
```

**Create file: `src/SupportCrm.Application/Tickets/ISlaAlertNotifier.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

/// <summary>
/// Delivers SLA warning/breach alerts and digests over email/push. No real email/push channel
/// exists in this codebase yet — register <see cref="NoOpSlaAlertNotifier"/> until one does,
/// following the same seam pattern as IAssignmentNotifier (Ticket Management Story 07) and
/// ICustomerStatusNotifier (Ticket Management Story 08). In-app delivery does not go through
/// this seam — it's real, via AgentNotificationService, called directly by SlaAlertService.
/// </summary>
public interface ISlaAlertNotifier
{
    Task NotifyWarningAsync(Guid agentId, Guid ticketId, string referenceNumber, CancellationToken ct);
    Task NotifyBreachAsync(Guid agentId, Guid ticketId, string referenceNumber, CancellationToken ct);
    Task SendDigestAsync(Guid agentId, IReadOnlyList<AtRiskTicketDto> atRiskTickets, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Tickets/NoOpSlaAlertNotifier.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

public class NoOpSlaAlertNotifier : ISlaAlertNotifier
{
    public Task NotifyWarningAsync(Guid agentId, Guid ticketId, string referenceNumber, CancellationToken ct) => Task.CompletedTask;
    public Task NotifyBreachAsync(Guid agentId, Guid ticketId, string referenceNumber, CancellationToken ct) => Task.CompletedTask;
    public Task SendDigestAsync(Guid agentId, IReadOnlyList<AtRiskTicketDto> atRiskTickets, CancellationToken ct) => Task.CompletedTask;
}
```

**Create file: `src/SupportCrm.Application/Tickets/SlaAlertService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Sla;

public class SlaAlertService(
    ITicketRepository ticketRepository,
    IAlertPreferenceRepository preferenceRepository,
    SlaCalculationService slaCalculationService,
    AgentNotificationService notificationService,
    ISlaAlertNotifier alertNotifier,
    TimeProvider timeProvider)
{
    // Fixed floor for the manager-facing "at risk" view, independent of any one agent's own
    // warning threshold — a ticket qualifies once it's 80%+ through its resolution window or
    // already breached, regardless of who (if anyone) it's assigned to.
    private const int AtRiskFloorPercentage = 80;

    public async Task<AlertPreferenceDto> SetPreferenceAsync(Guid agentId, SetAlertPreferenceRequest request, CancellationToken ct)
    {
        var preference = await preferenceRepository.GetByAgentIdAsync(agentId, ct) ?? new AlertPreference(agentId);
        preference.Update(request.EmailEnabled, request.PushEnabled, request.WarningThresholdPercentage, request.DigestFrequency);
        await preferenceRepository.UpsertAsync(preference, ct);
        await preferenceRepository.SaveChangesAsync(ct);
        return ToDto(preference);
    }

    public async Task<AlertPreferenceDto> GetPreferenceAsync(Guid agentId, CancellationToken ct)
    {
        var preference = await preferenceRepository.GetByAgentIdAsync(agentId, ct) ?? new AlertPreference(agentId); // unsaved defaults until the agent's first Set
        return ToDto(preference);
    }

    public async Task<IReadOnlyList<AtRiskTicketDto>> GetAtRiskTicketsAsync(CancellationToken ct)
    {
        var openTickets = await ticketRepository.GetOpenAsync(ct); // added by Story 23
        var result = new List<AtRiskTicketDto>();
        foreach (var ticket in openTickets)
        {
            var status = await slaCalculationService.GetStatusAsync(ticket.Id, ct);
            if (status is null) continue;
            var elapsedPercentage = 100 - status.ResolutionRemainingMinutes * 100 / Math.Max(1, status.ResolutionTargetMinutes);
            if (status.IsResolutionBreached || elapsedPercentage >= AtRiskFloorPercentage)
                result.Add(new AtRiskTicketDto(ticket.Id, ticket.ReferenceNumber, ticket.Priority, status.ResolutionRemainingMinutes, status.IsResolutionBreached, $"/tickets/{ticket.Id}"));
        }
        return result;
    }

    // Called every SlaEscalationHostedService tick (Story 23). Fires a ticket's Warning alert
    // once when it crosses its assigned agent's own threshold, and its Breach alert once when
    // it breaches (a breach always alerts, regardless of the configured warning threshold).
    public async Task EvaluateAndSendAlertsAsync(CancellationToken ct)
    {
        var assignedOpenTickets = (await ticketRepository.GetOpenAsync(ct)).Where(t => t.AssignedAgentId is not null).ToList();
        foreach (var ticket in assignedOpenTickets)
        {
            var status = await slaCalculationService.GetStatusAsync(ticket.Id, ct);
            if (status is null) continue;

            var preference = await preferenceRepository.GetByAgentIdAsync(ticket.AssignedAgentId!.Value, ct) ?? new AlertPreference(ticket.AssignedAgentId.Value);
            var elapsedPercentage = 100 - status.ResolutionRemainingMinutes * 100 / Math.Max(1, status.ResolutionTargetMinutes);

            if (status.IsResolutionBreached)
                await SendOnceAsync(ticket, "Breach", $"Ticket {ticket.ReferenceNumber} has breached its SLA.", preference, ct);
            else if (elapsedPercentage >= preference.WarningThresholdPercentage)
                await SendOnceAsync(ticket, "Warning", $"Ticket {ticket.ReferenceNumber} is at risk of breaching its SLA ({status.ResolutionRemainingMinutes} min remaining).", preference, ct);
        }
    }

    public async Task SendDailyWeeklyDigestsAsync(CancellationToken ct)
    {
        var subscribed = await preferenceRepository.GetWithDigestEnabledAsync(ct);
        if (subscribed.Count == 0) return;

        var atRisk = await GetAtRiskTicketsAsync(ct);
        var now = timeProvider.GetUtcNow();
        foreach (var preference in subscribed)
        {
            var lastSent = await preferenceRepository.GetLastDigestSentAsync(preference.AgentId, ct);
            var interval = preference.DigestFrequency == DigestFrequency.Daily ? TimeSpan.FromDays(1) : TimeSpan.FromDays(7);
            if (lastSent is not null && now - lastSent.Value < interval) continue;

            await alertNotifier.SendDigestAsync(preference.AgentId, atRisk, ct);
            await preferenceRepository.AddDigestLogAsync(new DigestLogEntry(preference.AgentId, now), ct);
        }
        await preferenceRepository.SaveChangesAsync(ct);
    }

    private async Task SendOnceAsync(Ticket ticket, string kind, string message, AlertPreference preference, CancellationToken ct)
    {
        if (await preferenceRepository.HasAlertBeenSentAsync(ticket.Id, kind, ct)) return;

        await notificationService.NotifyAsync(ticket.AssignedAgentId!.Value, $"Sla{kind}", message, ticket.Id, ct);

        if (kind == "Breach")
            await alertNotifier.NotifyBreachAsync(ticket.AssignedAgentId.Value, ticket.Id, ticket.ReferenceNumber, ct);
        else if (preference.EmailEnabled || preference.PushEnabled)
            await alertNotifier.NotifyWarningAsync(ticket.AssignedAgentId.Value, ticket.Id, ticket.ReferenceNumber, ct);

        await preferenceRepository.AddAlertLogAsync(new SlaAlertLog(ticket.Id, kind, timeProvider.GetUtcNow()), ct);
        await preferenceRepository.SaveChangesAsync(ct);
    }

    private static AlertPreferenceDto ToDto(AlertPreference p) => new(p.AgentId, p.EmailEnabled, p.PushEnabled, p.WarningThresholdPercentage, p.DigestFrequency);
}
```

**File: `src/SupportCrm.Application/Tickets/SlaEscalationHostedService.cs`** (Story 23's file) — extend `ExecuteAsync`'s loop body to also run SLA alerting/digests in the same tick, and broaden the class doc comment:

```csharp
// This codebase's first recurring background job — a minimal stand-in for a real job
// scheduler (Hangfire/Quartz/etc.), per Story 23/24's explicit scope note. Runs escalation
// evaluation (Story 23) and SLA alerting/digests (Story 24) every EvaluationInterval, in one
// DI scope per tick (IServiceScopeFactory, since every dependency below it is Scoped).
public class SlaEscalationHostedService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    public static readonly TimeSpan EvaluationInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(EvaluationInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();

            var escalationEngine = scope.ServiceProvider.GetRequiredService<EscalationRuleEngine>();
            await escalationEngine.EvaluateAllAsync(stoppingToken);

            var alertService = scope.ServiceProvider.GetRequiredService<SlaAlertService>();
            await alertService.EvaluateAndSendAlertsAsync(stoppingToken);
            await alertService.SendDailyWeeklyDigestsAsync(stoppingToken);
        }
    }
}
```

(Only the loop body and the class doc comment change; the constructor, `EvaluationInterval`, and `using`s are unchanged from Story 23.)

### 3 — Infrastructure: EF config, repository, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet`s after Story 23's additions:

```csharp
    public DbSet<AlertPreference> AlertPreferences => Set<AlertPreference>();
    public DbSet<SlaAlertLog> SlaAlertLogs => Set<SlaAlertLog>();
    public DbSet<DigestLogEntry> DigestLogEntries => Set<DigestLogEntry>();
```

Add new `OnModelCreating` blocks after Story 23's additions:

```csharp

        modelBuilder.Entity<AlertPreference>(entity =>
        {
            entity.ToTable("AlertPreferences");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.DigestFrequency).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.HasIndex(p => p.AgentId).IsUnique();
        });

        modelBuilder.Entity<SlaAlertLog>(entity =>
        {
            entity.ToTable("SlaAlertLog");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Kind).IsRequired().HasMaxLength(16);
            // A ticket's Warning/Breach alert fires at most once each — enforced here too, not
            // just in SlaAlertService.SendOnceAsync, so a racing tick can't double-send.
            entity.HasIndex(e => new { e.TicketId, e.Kind }).IsUnique();
        });

        modelBuilder.Entity<DigestLogEntry>(entity =>
        {
            entity.ToTable("DigestLog");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.AgentId);
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/AlertPreferenceRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AlertPreferenceRepository(SupportCrmDbContext dbContext) : IAlertPreferenceRepository
{
    public Task<AlertPreference?> GetByAgentIdAsync(Guid agentId, CancellationToken ct) =>
        dbContext.AlertPreferences.FirstOrDefaultAsync(p => p.AgentId == agentId, ct);

    public async Task<IReadOnlyList<AlertPreference>> GetWithDigestEnabledAsync(CancellationToken ct) =>
        await dbContext.AlertPreferences.Where(p => p.DigestFrequency != DigestFrequency.None).ToListAsync(ct);

    public Task UpsertAsync(AlertPreference preference, CancellationToken ct)
    {
        if (dbContext.Entry(preference).State == EntityState.Detached)
            dbContext.AlertPreferences.Add(preference);
        return Task.CompletedTask;
    }

    public Task<bool> HasAlertBeenSentAsync(Guid ticketId, string kind, CancellationToken ct) =>
        dbContext.SlaAlertLogs.AnyAsync(e => e.TicketId == ticketId && e.Kind == kind, ct);

    public Task AddAlertLogAsync(SlaAlertLog entry, CancellationToken ct)
    {
        dbContext.SlaAlertLogs.Add(entry);
        return Task.CompletedTask;
    }

    public async Task<DateTimeOffset?> GetLastDigestSentAsync(Guid agentId, CancellationToken ct) =>
        await dbContext.DigestLogEntries.Where(e => e.AgentId == agentId).OrderByDescending(e => e.SentAtUtc).Select(e => (DateTimeOffset?)e.SentAtUtc).FirstOrDefaultAsync(ct);

    public Task AddDigestLogAsync(DigestLogEntry entry, CancellationToken ct)
    {
        dbContext.DigestLogEntries.Add(entry);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add after Story 23's registrations, before `return services;`:

```csharp
        services.AddScoped<IAlertPreferenceRepository, AlertPreferenceRepository>();
        services.AddScoped<ISlaAlertNotifier, NoOpSlaAlertNotifier>();
        services.AddScoped<SlaAlertService>();
```

(`SlaEscalationHostedService` is already registered by Story 23 — not re-registered here.)

- After creating these files, run `dotnet ef migrations add AddSlaAlerts --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: `SlaAlertsController`

**Create file: `src/SupportCrm.Api/Controllers/SlaAlertsController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/sla/alerts")]
public class SlaAlertsController(SlaAlertService alertService) : ControllerBase
{
    [HttpGet("preferences/{agentId:guid}")]
    public async Task<ActionResult<AlertPreferenceDto>> GetPreference(Guid agentId, CancellationToken ct) =>
        await alertService.GetPreferenceAsync(agentId, ct);

    [HttpPut("preferences/{agentId:guid}")]
    public async Task<ActionResult<AlertPreferenceDto>> SetPreference(Guid agentId, [FromBody] SetAlertPreferenceRequest request, CancellationToken ct)
    {
        try { return await alertService.SetPreferenceAsync(agentId, request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    // "Available to managers" per the AC is a product/UI convention, not a backend role check —
    // see this story's Story Goal assumption note (no RBAC exists yet).
    [HttpGet("at-risk")]
    public async Task<ActionResult<IReadOnlyList<AtRiskTicketDto>>> GetAtRisk(CancellationToken ct) =>
        Ok(await alertService.GetAtRiskTicketsAsync(ct));
}
```

---

## Edge Cases & Failure Modes

- **A ticket has no assigned agent** — excluded entirely from `EvaluateAndSendAlertsAsync`'s `Where(t => t.AssignedAgentId is not null)` filter; it can still appear in `GetAtRiskTicketsAsync` (the manager-facing view is ticket-centric, not agent-centric).
- **A ticket has already sent its Warning alert and later breaches** — `SendOnceAsync`'s dedupe key is `(TicketId, Kind)`, so `"Warning"` and `"Breach"` are independent — the ticket still gets its one Breach alert even though its Warning already fired.
- **A ticket's remaining time recovers above the threshold after a Warning alert already fired** (e.g. priority downgraded, or an `SlaTarget` reconfigured with a longer window) — no "recovered" notification is sent, and the Warning is not re-sent if it dips back down later, since `HasAlertBeenSentAsync` only ever checks "has one ever been sent," not "is the condition currently true." Flagged as a known simplification, not a bug — a ticket's alert state is monotonic per kind.
- **`WarningThresholdPercentage` set outside 1–100** — rejected by `AlertPreference.Update`'s guard (`ArgumentException` → `400` via `SlaAlertsController.SetPreference`'s catch).
- **An agent has never called `SetPreferenceAsync`** — `GetByAgentIdAsync` returns `null`; both `GetPreferenceAsync` and `EvaluateAndSendAlertsAsync` construct a fresh, unsaved `new AlertPreference(agentId)` (defaults: threshold 80%, no email/push, no digest) rather than throwing — an agent who never configured anything still gets in-app Warning/Breach alerts at the 80% default.
- **`SendDailyWeeklyDigestsAsync` runs before an agent's very first digest is due** — `GetLastDigestSentAsync` returns `null` for a never-sent agent; the `lastSent is not null && ...` guard short-circuits to "send now," so a newly-subscribed agent gets their first digest on the very next tick rather than waiting a full day/week.
- **Zero tickets are currently at-risk** — `GetAtRiskTicketsAsync` returns an empty list; `SendDailyWeeklyDigestsAsync` still calls `alertNotifier.SendDigestAsync` with an empty list for every due subscriber (an explicit "nothing at risk" digest, not a skipped one) — the no-op stub doesn't care either way; a real implementation would decide whether to suppress empty digests, out of scope here.
- **`GetOpenAsync` (Story 23) is unavailable or the ticket list is large** — no pagination/batching is added in this story; every open ticket is walked on every tick, same pattern as `EscalationRuleEngine.EvaluateAllAsync` — flagged as a scale limitation shared with Story 23, not newly introduced here.
- **The hosted service tick overlaps a slow `EvaluateAndSendAlertsAsync` run** — the unique index on `(TicketId, Kind)` (see EF config above) prevents a double-send even if two ticks somehow interleave, same defense-in-depth reasoning as Story 23's escalation-log unique index.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/AlertPreferenceTests.cs`**:
   - `Update_ThresholdOutOfRange_Throws`
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/SlaAlertServiceTests.cs`**:
   - `EvaluateAndSendAlertsAsync_UnassignedTicket_NoAlertSent`
   - `EvaluateAndSendAlertsAsync_CrossesThreshold_SendsWarningOnce`
   - `EvaluateAndSendAlertsAsync_AlreadySentWarning_DoesNotResend`
   - `EvaluateAndSendAlertsAsync_Breached_SendsBreachRegardlessOfThreshold`
   - `SendDailyWeeklyDigestsAsync_NeverSentBefore_SendsImmediately`
   - `SendDailyWeeklyDigestsAsync_WithinInterval_DoesNotResend`
   - `GetAtRiskTicketsAsync_IncludesBreachedAndNearingBreachOnly`
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/SlaAlertsControllerTests.cs`**:
   - `Put_PreferenceWithInvalidThreshold_Returns400`
   - `Get_AtRisk_ReturnsCurrentlyAtRiskTickets`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddSlaAlerts --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.
3. **Regression:** confirm `SlaEscalationHostedService` still starts and completes a tick cleanly with the added `SlaAlertService` calls, on an empty database (zero rules, zero preferences, zero tickets).

---

## Frontend Tasks

**Implemented** (`d:\Code\selfAssessment\frontend\src\app`):

- **File: `features/sla-automation/sla.model.ts`** — `AlertPreference`, `SetAlertPreferenceRequest`, `AtRiskTicket`, `DigestFrequency`.
- **File: `features/sla-automation/sla.service.ts`** — `getAlertPreference`/`setAlertPreference`, `getAtRiskTickets`.
- **Create file: `features/sla-automation/sla-alerts/sla-alerts.component.{ts,html,scss}`** — at-risk ticket digest table (breached/nearing badges) + an "acting as" agent picker (reusing `AgentContextService`) whose preferences (email/push toggles, warning threshold %, digest frequency) load and save inline. Route: `/admin/sla-alerts`.
- **File: `app.routes.ts`**, **`layout/app-shell/app-shell.component.ts`** — route + sidebar nav entry ("SLA alerts").

In-app alert delivery itself (the `AgentNotificationService` calls `SlaAlertService.SendOnceAsync` makes) surfaces through the existing notification-bell component (Agent Dashboard Story 20) with no changes needed there — it already renders any `AgentNotification` row regardless of `Kind`.

---

## Done Criteria

- [ ] In-app SLA warning/breach alerts are delivered via the existing `AgentNotificationService`, each at most once per ticket.
- [ ] Email/push are configurable per agent and routed through the stubbed `ISlaAlertNotifier` seam.
- [ ] Each alert identifies the ticket, remaining time, and a frontend deep-link path.
- [ ] Alert threshold and channels are configurable per agent (`GET`/`PUT /api/sla/alerts/preferences/{agentId}`).
- [ ] A daily/weekly digest of at-risk tickets is available on demand (`GET /api/sla/alerts/at-risk`) and sent on each subscriber's configured cadence via the shared `SlaEscalationHostedService` tick.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
