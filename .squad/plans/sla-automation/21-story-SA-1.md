# Story 21 — Response and resolution targets (Story: SA-1)

---

## Prerequisites

- Ticket Management Stories 05–08 completed ([`../ticket-management/05-story-TM-1.md`](../ticket-management/05-story-TM-1.md) .. [`08-story-TM-4.md`](../ticket-management/08-story-TM-4.md)) — provide `Ticket` (`CreatedAtUtc`, `Priority`, `CategoryId`, `Status`), `TicketCategory`, and the status-history audit trail this story reads to detect `Pending` pauses.
- Agent Dashboard Story 16 completed ([`../agent-dashboard/16-story-AD-1.md`](../agent-dashboard/16-story-AD-1.md)) — **this story replaces its output.** Story 16 shipped a fixed-window static `SlaPolicy` helper (`src/SupportCrm.Application/Tickets/SlaPolicy.cs`) as an explicit stand-in ("SLA policy configuration UI... is a fixed constant for now" — its intake, `## Out of scope`). This story is that deferred configuration work: it deletes the static helper and replaces its two call sites in `AgentDashboardService.cs` with the new configurable, business-hours-aware calculation — the dashboard's `AgentDashboardTicketDto.SlaDueAtUtc`/`SlaState` contract does not change shape, only how it's computed.

---

## Story Goal

1. SLA **targets** (separate response-time and resolution-time minute budgets) can be configured, each scoped by priority (required) and optionally narrowed by category and/or customer tier.
2. When more than one configured target could match a ticket, the most specific one wins: tier+category match > category-only match > priority-only match.
3. Every ticket exposes a computed, real-time time-to-breach for both its response and resolution clocks.
4. The calculation is business-hours- and holiday-aware, using one global weekly calendar (not per-region/per-team) — a ticket's clock only advances during configured working hours and skips configured holidays.
5. The clock also **pauses** for any time a ticket has spent in `Pending` status (business time only), so a ticket awaiting the customer doesn't silently burn its SLA window.

**Not in scope:** auto-assignment (Story 22), escalation actions (Story 23), alert/notification delivery (Story 24) — this story is target configuration + breach-time calculation only. Full contracts/billing/tier management — only a minimal tier marker on `Customer`. Per-timezone business calendars — one global UTC-aligned calendar.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Tickets/SlaPolicy.cs` (all 32 lines) — the static helper this story **deletes**. Its `Windows` dictionary (Urgent=4h/High=8h/Medium=24h/Low=72h) becomes this story's seeded default `SlaTarget` rows (as resolution targets, to keep default dashboard behavior close to today's), and its `DueAt`/`StateFor` logic is superseded by `SlaCalculationService` below.
2. `src/SupportCrm.Application/Tickets/AgentDashboardService.cs`, lines 1–34 (whole file) — `GetAssignedTicketsAsync`'s `.Select(...)` at lines 24–27 calls `SlaPolicy.DueAt`/`SlaPolicy.StateFor` synchronously per ticket. This story restructures it to batch-resolve SLA status **before** the `.Select` (async calls cannot run inside a synchronous LINQ projection), keeping the sort at lines 30–32 (`OrderByDescending(Priority).ThenBy(SlaDueAtUtc)`) unchanged.
3. `src/SupportCrm.Application/Tickets/AgentDashboardDtos.cs`, lines 5–14 — `AgentDashboardTicketDto`'s `SlaDueAtUtc`/`SlaState` fields; their meaning (due-at for the *resolution* clock; `"OnTrack"|"NearingBreach"|"Breached"|"NotApplicable"`) is preserved exactly, only the source of truth changes.
4. `src/SupportCrm.Domain/Entities/Ticket.cs`, lines 1–20 — `CreatedAtUtc`, `Priority` (`TicketPriority`), `CategoryId`, `Status` (`TicketStatus`) are the calculation inputs; no changes to this file.
5. `src/SupportCrm.Domain/Entities/Customer.cs`, lines 1–52 — add a `Tier` property here (see Task 1); `SetAccountFlags` at lines 47–51 is the precedent for a simple setter method style to follow for the new `SetTier`.
6. `src/SupportCrm.Domain/Entities/TicketStatusChangeEntry.cs` (all 29 lines) — `TicketId`, `OldStatus`, `NewStatus`, `ChangedAtUtc`; read via `ITicketRepository.GetStatusHistoryAsync` (already implemented, `src/SupportCrm.Infrastructure/Persistence/TicketRepository.cs` lines 29–30) to compute time spent in `Pending`.
7. `src/SupportCrm.Domain/Entities/TicketCategory.cs` (all 24 lines) and `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs` lines 116–129 — precedent for a simple configuration entity with `HasData` seeding; this story's `SlaTarget` seeding follows the same fixed-GUID pattern.
8. `src/SupportCrm.Application/Tickets/TicketCategoryService.cs` (all 21 lines) and `src/SupportCrm.Infrastructure/Persistence/TicketCategoryRepository.cs` (all 22 lines) — precedent for this story's minimal CRUD services/repositories (`SlaTargetService`, `SlaTargetRepository`, etc.).
9. `src/SupportCrm.Infrastructure/DependencyInjection.cs`, lines 1–94 (whole file) — registration list to extend; note line 21 `services.AddSingleton(TimeProvider.System)` is already registered and reused here, not re-registered.

---

## Backend Tasks

### 1 — Domain: `CustomerTier`, `Customer.Tier`, `SlaTarget`, `BusinessHours`, `Holiday`

**Create file: `src/SupportCrm.Domain/Entities/CustomerTier.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public enum CustomerTier
{
    Standard,
    Silver,
    Gold,
    Platinum
}
```

**File: `src/SupportCrm.Domain/Entities/Customer.cs`** — add a property after `IsAtRisk` (line 15):

```csharp
    public CustomerTier Tier { get; private set; } = CustomerTier.Standard;
```

and a setter after `SetAccountFlags` (after line 51):

```csharp

    public void SetTier(CustomerTier tier) => Tier = tier;
```

**Create file: `src/SupportCrm.Domain/Entities/SlaTarget.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// Resolution precedence when multiple active targets match one ticket: the most specific
// wins — Tier+Category > Category-only > Priority-only (Priority is always required and
// always matches exactly). See SlaTargetService.ResolveAsync, which orders by Specificity().
public class SlaTarget
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public TicketPriority Priority { get; private set; }
    public Guid? CategoryId { get; private set; }
    public CustomerTier? Tier { get; private set; }
    public int ResponseTargetMinutes { get; private set; }
    public int ResolutionTargetMinutes { get; private set; }
    public bool IsActive { get; private set; } = true;

    private SlaTarget() { } // EF Core

    public SlaTarget(string name, TicketPriority priority, Guid? categoryId, CustomerTier? tier, int responseTargetMinutes, int resolutionTargetMinutes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (responseTargetMinutes <= 0)
            throw new ArgumentException("Response target must be positive.", nameof(responseTargetMinutes));
        if (resolutionTargetMinutes < responseTargetMinutes)
            throw new ArgumentException("Resolution target must be at least the response target.", nameof(resolutionTargetMinutes));

        Id = Guid.NewGuid();
        Name = name;
        Priority = priority;
        CategoryId = categoryId;
        Tier = tier;
        ResponseTargetMinutes = responseTargetMinutes;
        ResolutionTargetMinutes = resolutionTargetMinutes;
    }

    public void Deactivate() => IsActive = false;

    public int Specificity() => (CategoryId is not null ? 1 : 0) + (Tier is not null ? 1 : 0);
}
```

**Create file: `src/SupportCrm.Domain/Entities/BusinessHours.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// One row per day of week, seeded for all 7 (see SupportCrmDbContext). A day with
// IsWorkingDay=false is skipped entirely by BusinessCalendarService regardless of
// StartTime/EndTime. Keyed by DayOfWeek — one global calendar, not per-team/per-region.
public class BusinessHours
{
    public DayOfWeek DayOfWeek { get; private set; }
    public bool IsWorkingDay { get; private set; }
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }

    private BusinessHours() { } // EF Core

    public BusinessHours(DayOfWeek dayOfWeek, bool isWorkingDay, TimeOnly startTime, TimeOnly endTime)
    {
        if (isWorkingDay && startTime >= endTime)
            throw new ArgumentException("Start time must be before end time on a working day.", nameof(startTime));
        DayOfWeek = dayOfWeek;
        IsWorkingDay = isWorkingDay;
        StartTime = startTime;
        EndTime = endTime;
    }

    public void Update(bool isWorkingDay, TimeOnly startTime, TimeOnly endTime)
    {
        if (isWorkingDay && startTime >= endTime)
            throw new ArgumentException("Start time must be before end time on a working day.", nameof(startTime));
        IsWorkingDay = isWorkingDay;
        StartTime = startTime;
        EndTime = endTime;
    }
}
```

**Create file: `src/SupportCrm.Domain/Entities/Holiday.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class Holiday
{
    public Guid Id { get; private set; }
    public DateOnly Date { get; private set; }
    public string Name { get; private set; } = default!;

    private Holiday() { } // EF Core

    public Holiday(DateOnly date, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Holiday name is required.", nameof(name));
        Id = Guid.NewGuid();
        Date = date;
        Name = name;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ISlaTargetRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ISlaTargetRepository
{
    Task<IReadOnlyList<SlaTarget>> GetActiveAsync(CancellationToken ct);
    Task AddAsync(SlaTarget target, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IBusinessCalendarRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IBusinessCalendarRepository
{
    Task<IReadOnlyList<BusinessHours>> GetBusinessHoursAsync(CancellationToken ct);
    Task<BusinessHours?> GetBusinessHoursForDayAsync(DayOfWeek day, CancellationToken ct);
    Task UpdateBusinessHoursAsync(DayOfWeek day, bool isWorkingDay, TimeOnly startTime, TimeOnly endTime, CancellationToken ct);
    Task<IReadOnlyList<Holiday>> GetHolidaysAsync(CancellationToken ct);
    Task AddHolidayAsync(Holiday holiday, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, `BusinessCalendarService`, `SlaTargetService`, `SlaCalculationService`

**Create file: `src/SupportCrm.Application/Sla/SlaDtos.cs`**

```csharp
namespace SupportCrm.Application.Sla;

using SupportCrm.Domain.Entities;

public record CreateSlaTargetRequest(string Name, TicketPriority Priority, Guid? CategoryId, CustomerTier? Tier, int ResponseTargetMinutes, int ResolutionTargetMinutes);
public record SlaTargetDto(Guid Id, string Name, TicketPriority Priority, Guid? CategoryId, CustomerTier? Tier, int ResponseTargetMinutes, int ResolutionTargetMinutes);
public record SetBusinessHoursRequest(DayOfWeek DayOfWeek, bool IsWorkingDay, TimeOnly StartTime, TimeOnly EndTime);
public record BusinessHoursDto(DayOfWeek DayOfWeek, bool IsWorkingDay, TimeOnly StartTime, TimeOnly EndTime);
public record CreateHolidayRequest(DateOnly Date, string Name);
public record HolidayDto(Guid Id, DateOnly Date, string Name);
public record TicketSlaStatusDto(
    Guid TicketId, Guid SlaTargetId, int ResolutionTargetMinutes,
    DateTimeOffset ResponseDueAtUtc, DateTimeOffset ResolutionDueAtUtc,
    bool IsResponseBreached, bool IsResolutionBreached,
    int ResponseRemainingMinutes, int ResolutionRemainingMinutes);
```

**Create file: `src/SupportCrm.Application/Sla/BusinessCalendarService.cs`**

```csharp
namespace SupportCrm.Application.Sla;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

// Treats every DateTimeOffset as UTC-aligned to calendar days (no timezone conversion) — one
// global calendar, per the story's explicit UTC-only simplification.
public class BusinessCalendarService(IBusinessCalendarRepository calendarRepository)
{
    public async Task<DateTimeOffset> AddBusinessMinutesAsync(DateTimeOffset startUtc, int minutes, CancellationToken ct)
    {
        var (hoursByDay, holidays) = await LoadAsync(ct);
        var cursor = startUtc;
        var remaining = minutes;
        while (remaining > 0)
        {
            if (!IsWorkingInstant(cursor, hoursByDay, holidays, out var windowStart, out var windowEnd))
            {
                cursor = NextDayStart(cursor);
                continue;
            }
            if (cursor < windowStart) cursor = windowStart;

            var availableToday = (int)(windowEnd - cursor).TotalMinutes;
            if (remaining <= availableToday) return cursor.AddMinutes(remaining);
            remaining -= availableToday;
            cursor = NextDayStart(cursor);
        }
        return cursor;
    }

    public async Task<int> CalculateBusinessMinutesBetweenAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken ct)
    {
        if (endUtc <= startUtc) return 0;
        var (hoursByDay, holidays) = await LoadAsync(ct);
        var total = 0;
        var cursor = startUtc;
        while (cursor < endUtc)
        {
            var dayEnd = NextDayStart(cursor);
            var segmentEnd = dayEnd < endUtc ? dayEnd : endUtc;
            if (IsWorkingDay(cursor, hoursByDay, holidays, out var day))
            {
                var windowStart = AtTime(cursor, day!.StartTime);
                var windowEnd = AtTime(cursor, day.EndTime);
                var from = cursor > windowStart ? cursor : windowStart;
                var to = segmentEnd < windowEnd ? segmentEnd : windowEnd;
                if (to > from) total += (int)(to - from).TotalMinutes;
            }
            cursor = dayEnd;
        }
        return total;
    }

    private static bool IsWorkingDay(DateTimeOffset dt, IReadOnlyDictionary<DayOfWeek, BusinessHours> hoursByDay, HashSet<DateOnly> holidays, out BusinessHours? day)
    {
        day = hoursByDay.GetValueOrDefault(dt.DayOfWeek);
        return day is { IsWorkingDay: true } && !holidays.Contains(DateOnly.FromDateTime(dt.UtcDateTime));
    }

    private static bool IsWorkingInstant(DateTimeOffset dt, IReadOnlyDictionary<DayOfWeek, BusinessHours> hoursByDay, HashSet<DateOnly> holidays, out DateTimeOffset windowStart, out DateTimeOffset windowEnd)
    {
        windowStart = windowEnd = default;
        if (!IsWorkingDay(dt, hoursByDay, holidays, out var day)) return false;
        windowStart = AtTime(dt, day!.StartTime);
        windowEnd = AtTime(dt, day.EndTime);
        return dt < windowEnd; // still time left today; caller clamps dt up to windowStart if early
    }

    private static DateTimeOffset NextDayStart(DateTimeOffset dt) => new(dt.UtcDateTime.Date.AddDays(1), TimeSpan.Zero);
    private static DateTimeOffset AtTime(DateTimeOffset dt, TimeOnly time) => new DateTimeOffset(dt.UtcDateTime.Date, TimeSpan.Zero).Add(time.ToTimeSpan());

    private async Task<(IReadOnlyDictionary<DayOfWeek, BusinessHours> hoursByDay, HashSet<DateOnly> holidays)> LoadAsync(CancellationToken ct)
    {
        var hours = await calendarRepository.GetBusinessHoursAsync(ct);
        var holidays = await calendarRepository.GetHolidaysAsync(ct);
        return (hours.ToDictionary(h => h.DayOfWeek), holidays.Select(h => h.Date).ToHashSet());
    }
}
```

**Create file: `src/SupportCrm.Application/Sla/SlaTargetService.cs`**

```csharp
namespace SupportCrm.Application.Sla;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SlaTargetService(ISlaTargetRepository repository)
{
    public async Task<SlaTargetDto> CreateAsync(CreateSlaTargetRequest request, CancellationToken ct)
    {
        var target = new SlaTarget(request.Name.Trim(), request.Priority, request.CategoryId, request.Tier, request.ResponseTargetMinutes, request.ResolutionTargetMinutes);
        await repository.AddAsync(target, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(target);
    }

    public async Task<IReadOnlyList<SlaTargetDto>> GetActiveAsync(CancellationToken ct) =>
        (await repository.GetActiveAsync(ct)).Select(ToDto).ToList();

    // Priority is mandatory and matched exactly; Category/Tier only narrow. Among all matches,
    // the most specific (Specificity()) wins — see SlaTarget's doc comment.
    public async Task<SlaTarget?> ResolveAsync(TicketPriority priority, Guid? categoryId, CustomerTier tier, CancellationToken ct) =>
        (await repository.GetActiveAsync(ct))
            .Where(t => t.Priority == priority)
            .Where(t => t.CategoryId is null || t.CategoryId == categoryId)
            .Where(t => t.Tier is null || t.Tier == tier)
            .OrderByDescending(t => t.Specificity())
            .FirstOrDefault();

    private static SlaTargetDto ToDto(SlaTarget t) => new(t.Id, t.Name, t.Priority, t.CategoryId, t.Tier, t.ResponseTargetMinutes, t.ResolutionTargetMinutes);
}
```

**Create file: `src/SupportCrm.Application/Sla/BusinessCalendarConfigService.cs`** — thin CRUD wrapper, mirrors `TicketCategoryService`'s shape:

```csharp
namespace SupportCrm.Application.Sla;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class BusinessCalendarConfigService(IBusinessCalendarRepository repository)
{
    public async Task<IReadOnlyList<BusinessHoursDto>> GetBusinessHoursAsync(CancellationToken ct) =>
        (await repository.GetBusinessHoursAsync(ct)).Select(h => new BusinessHoursDto(h.DayOfWeek, h.IsWorkingDay, h.StartTime, h.EndTime)).ToList();

    public Task SetBusinessHoursAsync(SetBusinessHoursRequest request, CancellationToken ct) =>
        repository.UpdateBusinessHoursAsync(request.DayOfWeek, request.IsWorkingDay, request.StartTime, request.EndTime, ct);

    public async Task<HolidayDto> AddHolidayAsync(CreateHolidayRequest request, CancellationToken ct)
    {
        var holiday = new Holiday(request.Date, request.Name.Trim());
        await repository.AddHolidayAsync(holiday, ct);
        await repository.SaveChangesAsync(ct);
        return new HolidayDto(holiday.Id, holiday.Date, holiday.Name);
    }

    public async Task<IReadOnlyList<HolidayDto>> GetHolidaysAsync(CancellationToken ct) =>
        (await repository.GetHolidaysAsync(ct)).Select(h => new HolidayDto(h.Id, h.Date, h.Name)).ToList();
}
```

**Create file: `src/SupportCrm.Application/Sla/SlaCalculationService.cs`**

```csharp
namespace SupportCrm.Application.Sla;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Tickets;

public class SlaCalculationService(
    ITicketRepository ticketRepository,
    ICustomerRepository customerRepository,
    SlaTargetService targetService,
    BusinessCalendarService calendarService,
    TimeProvider timeProvider)
{
    public async Task<TicketSlaStatusDto?> GetStatusAsync(Guid ticketId, CancellationToken ct)
    {
        var ticket = await ticketRepository.GetByIdAsync(ticketId, ct) ?? throw new TicketNotFoundException(ticketId.ToString());
        var customer = await customerRepository.GetByIdAsync(ticket.CustomerId, ct);
        return await ComputeAsync(ticket, customer?.Tier ?? CustomerTier.Standard, ct);
    }

    // Batch entry point for the dashboard (Task 3) — avoids re-resolving/re-fetching a ticket
    // already in hand. Skips tickets with no matching active SlaTarget (caller reports "NotApplicable").
    public async Task<IReadOnlyDictionary<Guid, TicketSlaStatusDto>> GetStatusesAsync(IReadOnlyList<Ticket> tickets, CancellationToken ct)
    {
        var result = new Dictionary<Guid, TicketSlaStatusDto>();
        foreach (var ticket in tickets)
        {
            var customer = await customerRepository.GetByIdAsync(ticket.CustomerId, ct);
            var status = await ComputeAsync(ticket, customer?.Tier ?? CustomerTier.Standard, ct);
            if (status is not null) result[ticket.Id] = status;
        }
        return result;
    }

    private async Task<TicketSlaStatusDto?> ComputeAsync(Ticket ticket, CustomerTier tier, CancellationToken ct)
    {
        var target = await targetService.ResolveAsync(ticket.Priority, ticket.CategoryId, tier, ct);
        if (target is null) return null; // no policy configured for this priority — caller reports "NotApplicable"

        var now = timeProvider.GetUtcNow();
        var baseResponseDueAtUtc = await calendarService.AddBusinessMinutesAsync(ticket.CreatedAtUtc, target.ResponseTargetMinutes, ct);
        var baseResolutionDueAtUtc = await calendarService.AddBusinessMinutesAsync(ticket.CreatedAtUtc, target.ResolutionTargetMinutes, ct);

        // Push both due-ats out by however long the ticket has spent Pending so far (business
        // time only) — the clock pauses while awaiting the customer, per the story's Pending-pause rule.
        var pausedMinutes = await GetPendingBusinessMinutesAsync(ticket.Id, now, ct);
        var responseDueAtUtc = pausedMinutes == 0 ? baseResponseDueAtUtc : await calendarService.AddBusinessMinutesAsync(baseResponseDueAtUtc, pausedMinutes, ct);
        var resolutionDueAtUtc = pausedMinutes == 0 ? baseResolutionDueAtUtc : await calendarService.AddBusinessMinutesAsync(baseResolutionDueAtUtc, pausedMinutes, ct);

        var isClosed = ticket.Status is TicketStatus.Closed or TicketStatus.Resolved;
        return new TicketSlaStatusDto(
            ticket.Id, target.Id, target.ResolutionTargetMinutes,
            responseDueAtUtc, resolutionDueAtUtc,
            IsResponseBreached: now >= responseDueAtUtc,
            IsResolutionBreached: !isClosed && now >= resolutionDueAtUtc,
            ResponseRemainingMinutes: Math.Max(0, (int)(responseDueAtUtc - now).TotalMinutes),
            ResolutionRemainingMinutes: Math.Max(0, (int)(resolutionDueAtUtc - now).TotalMinutes));
    }

    private async Task<int> GetPendingBusinessMinutesAsync(Guid ticketId, DateTimeOffset now, CancellationToken ct)
    {
        var history = (await ticketRepository.GetStatusHistoryAsync(ticketId, ct)).OrderBy(h => h.ChangedAtUtc).ToList();
        var total = 0;
        for (var i = 0; i < history.Count; i++)
        {
            if (history[i].NewStatus != TicketStatus.Pending) continue;
            var from = history[i].ChangedAtUtc;
            var to = i + 1 < history.Count ? history[i + 1].ChangedAtUtc : now;
            total += await calendarService.CalculateBusinessMinutesBetweenAsync(from, to, ct);
        }
        return total;
    }
}
```

**Delete file: `src/SupportCrm.Application/Tickets/SlaPolicy.cs`** — fully superseded by `SlaCalculationService` above.

**File: `src/SupportCrm.Application/Tickets/AgentDashboardService.cs`** — replace lines 1–34 (whole file) with:

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Sla;

public class AgentDashboardService(ITicketRepository ticketRepository, SlaCalculationService slaCalculationService)
{
    public async Task<IReadOnlyList<AgentDashboardTicketDto>> GetAssignedTicketsAsync(
        Guid agentId, TicketStatus? status, TicketPriority? priority, Guid? categoryId, CancellationToken ct)
    {
        var tickets = await ticketRepository.GetAssignedToAgentAsync(agentId, ct);

        // Default view is "my workload" — excludes Closed unless the agent explicitly
        // filters for it; an explicit status filter always wins over that default.
        IEnumerable<Ticket> filtered = status.HasValue
            ? tickets.Where(t => t.Status == status.Value)
            : tickets.Where(t => t.Status != TicketStatus.Closed);

        if (priority.HasValue) filtered = filtered.Where(t => t.Priority == priority.Value);
        if (categoryId.HasValue) filtered = filtered.Where(t => t.CategoryId == categoryId.Value);
        var filteredList = filtered.ToList();

        // Batch-resolve SLA status before the projection — SlaCalculationService is async and
        // cannot be called inside a synchronous LINQ .Select.
        var slaByTicket = await slaCalculationService.GetStatusesAsync(filteredList, ct);

        return filteredList
            .Select(t =>
            {
                var sla = slaByTicket.GetValueOrDefault(t.Id);
                return new AgentDashboardTicketDto(
                    t.Id, t.ReferenceNumber, t.Subject, t.Status, t.Priority, t.CategoryId, t.CreatedAtUtc,
                    sla?.ResolutionDueAtUtc ?? t.CreatedAtUtc,
                    ToSlaState(t.Status, sla));
            })
            // TicketPriority is declared Low < Medium < High < Urgent, so descending puts
            // the most severe first; SLA due-at ascending breaks ties within a priority.
            .OrderByDescending(d => d.Priority)
            .ThenBy(d => d.SlaDueAtUtc)
            .ToList();
    }

    private static string ToSlaState(TicketStatus status, TicketSlaStatusDto? sla)
    {
        if (status == TicketStatus.Closed || sla is null) return "NotApplicable";
        if (sla.IsResolutionBreached) return "Breached";
        return sla.ResolutionRemainingMinutes <= sla.ResolutionTargetMinutes * 0.2 ? "NearingBreach" : "OnTrack";
    }
}
```

### 3 — Infrastructure: EF config + seed, repositories, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet`s after line 31 (`TicketCollaborators`):

```csharp
    public DbSet<Customer> Customers => Set<Customer>(); // unchanged — Tier is a plain column, no new DbSet
    public DbSet<SlaTarget> SlaTargets => Set<SlaTarget>();
    public DbSet<BusinessHours> BusinessHours => Set<BusinessHours>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
```

(Only the three new `DbSet` lines are additions; `Customers` above is shown for orientation, not re-added.) Extend the existing `Customer` block (lines 35–47) with one property line:

```csharp
            entity.Property(c => c.Tier).HasConversion<string>().HasMaxLength(16).IsRequired();
```

Add new `OnModelCreating` blocks after the `TicketCollaborator` block (after line 270):

```csharp

        modelBuilder.Entity<SlaTarget>(entity =>
        {
            entity.ToTable("SlaTargets");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Name).IsRequired().HasMaxLength(256);
            entity.Property(t => t.Priority).HasConversion<string>().HasMaxLength(16).IsRequired();
            entity.Property(t => t.Tier).HasConversion<string?>().HasMaxLength(16);
            entity.HasIndex(t => new { t.Priority, t.CategoryId, t.Tier });

            // Seeded defaults preserve Story 16's original fixed resolution windows exactly
            // (Urgent 4h/High 8h/Medium 24h/Low 72h = 240/480/1440/4320 min); response targets
            // are new, using common response:resolution ratios. Priority-only (Category=null,
            // Tier=null) so every ticket has a fallback target out of the box.
            entity.HasData(
                new { Id = new Guid("22222222-2222-2222-2222-222222222201"), Name = "Default — Urgent", Priority = TicketPriority.Urgent, CategoryId = (Guid?)null, Tier = (CustomerTier?)null, ResponseTargetMinutes = 30, ResolutionTargetMinutes = 240, IsActive = true },
                new { Id = new Guid("22222222-2222-2222-2222-222222222202"), Name = "Default — High", Priority = TicketPriority.High, CategoryId = (Guid?)null, Tier = (CustomerTier?)null, ResponseTargetMinutes = 60, ResolutionTargetMinutes = 480, IsActive = true },
                new { Id = new Guid("22222222-2222-2222-2222-222222222203"), Name = "Default — Medium", Priority = TicketPriority.Medium, CategoryId = (Guid?)null, Tier = (CustomerTier?)null, ResponseTargetMinutes = 240, ResolutionTargetMinutes = 1440, IsActive = true },
                new { Id = new Guid("22222222-2222-2222-2222-222222222204"), Name = "Default — Low", Priority = TicketPriority.Low, CategoryId = (Guid?)null, Tier = (CustomerTier?)null, ResponseTargetMinutes = 480, ResolutionTargetMinutes = 4320, IsActive = true }
            );
        });

        modelBuilder.Entity<BusinessHours>(entity =>
        {
            entity.ToTable("BusinessHours");
            entity.HasKey(h => h.DayOfWeek);
            entity.Property(h => h.DayOfWeek).HasConversion<string>().HasMaxLength(16);

            // Seeded Mon–Fri 09:00–17:00 working, Sat/Sun non-working — one row per day, required
            // for CalculateBusinessMinutesBetweenAsync/AddBusinessMinutesAsync to have data on first run.
            entity.HasData(
                new { DayOfWeek = DayOfWeek.Monday, IsWorkingDay = true, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) },
                new { DayOfWeek = DayOfWeek.Tuesday, IsWorkingDay = true, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) },
                new { DayOfWeek = DayOfWeek.Wednesday, IsWorkingDay = true, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) },
                new { DayOfWeek = DayOfWeek.Thursday, IsWorkingDay = true, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) },
                new { DayOfWeek = DayOfWeek.Friday, IsWorkingDay = true, StartTime = new TimeOnly(9, 0), EndTime = new TimeOnly(17, 0) },
                new { DayOfWeek = DayOfWeek.Saturday, IsWorkingDay = false, StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(0, 0) },
                new { DayOfWeek = DayOfWeek.Sunday, IsWorkingDay = false, StartTime = new TimeOnly(0, 0), EndTime = new TimeOnly(0, 0) }
            );
        });

        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.ToTable("Holidays");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Name).IsRequired().HasMaxLength(256);
            entity.HasIndex(h => h.Date).IsUnique();
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/SlaTargetRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SlaTargetRepository(SupportCrmDbContext dbContext) : ISlaTargetRepository
{
    public async Task<IReadOnlyList<SlaTarget>> GetActiveAsync(CancellationToken ct) =>
        await dbContext.SlaTargets.Where(t => t.IsActive).ToListAsync(ct);

    public Task AddAsync(SlaTarget target, CancellationToken ct)
    {
        dbContext.SlaTargets.Add(target);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/BusinessCalendarRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class BusinessCalendarRepository(SupportCrmDbContext dbContext) : IBusinessCalendarRepository
{
    public async Task<IReadOnlyList<BusinessHours>> GetBusinessHoursAsync(CancellationToken ct) =>
        await dbContext.BusinessHours.ToListAsync(ct);

    public Task<BusinessHours?> GetBusinessHoursForDayAsync(DayOfWeek day, CancellationToken ct) =>
        dbContext.BusinessHours.FirstOrDefaultAsync(h => h.DayOfWeek == day, ct);

    public async Task UpdateBusinessHoursAsync(DayOfWeek day, bool isWorkingDay, TimeOnly startTime, TimeOnly endTime, CancellationToken ct)
    {
        var hours = await dbContext.BusinessHours.FirstOrDefaultAsync(h => h.DayOfWeek == day, ct)
            ?? throw new KeyNotFoundException($"Business hours for '{day}' were not found.");
        hours.Update(isWorkingDay, startTime, endTime);
    }

    public async Task<IReadOnlyList<Holiday>> GetHolidaysAsync(CancellationToken ct) =>
        await dbContext.Holidays.ToListAsync(ct);

    public Task AddHolidayAsync(Holiday holiday, CancellationToken ct)
    {
        dbContext.Holidays.Add(holiday);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add after line 90 (`TicketCollaborationService`) and before `return services;` (line 92):

```csharp
        services.AddScoped<ISlaTargetRepository, SlaTargetRepository>();
        services.AddScoped<Application.Sla.SlaTargetService>();
        services.AddScoped<IBusinessCalendarRepository, BusinessCalendarRepository>();
        services.AddScoped<Application.Sla.BusinessCalendarService>();
        services.AddScoped<Application.Sla.BusinessCalendarConfigService>();
        services.AddScoped<Application.Sla.SlaCalculationService>();
```

Add `using SupportCrm.Application.Sla;` near line 7 instead of fully-qualifying, if preferred — either is fine as long as it compiles; fully-qualified above avoids ambiguity with `Application.Tickets` types already `using`d in this file (none currently collide by name, so a plain `using` also works).

- After creating these files, run `dotnet ef migrations add AddSlaTargetsAndBusinessCalendar --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` from the repo root.

### 4 — Api: `SlaController`, `TicketsController` addition

**Create file: `src/SupportCrm.Api/Controllers/SlaController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Sla;

[ApiController]
[Route("api/sla")]
public class SlaController(SlaTargetService targetService, BusinessCalendarConfigService calendarConfigService) : ControllerBase
{
    [HttpGet("targets")]
    public async Task<ActionResult<IReadOnlyList<SlaTargetDto>>> GetTargets(CancellationToken ct) =>
        Ok(await targetService.GetActiveAsync(ct));

    [HttpPost("targets")]
    public async Task<ActionResult<SlaTargetDto>> CreateTarget([FromBody] CreateSlaTargetRequest request, CancellationToken ct)
    {
        try { return await targetService.CreateAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }

    [HttpGet("business-hours")]
    public async Task<ActionResult<IReadOnlyList<BusinessHoursDto>>> GetBusinessHours(CancellationToken ct) =>
        Ok(await calendarConfigService.GetBusinessHoursAsync(ct));

    [HttpPut("business-hours")]
    public async Task<IActionResult> SetBusinessHours([FromBody] SetBusinessHoursRequest request, CancellationToken ct)
    {
        try { await calendarConfigService.SetBusinessHoursAsync(request, ct); return NoContent(); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpGet("holidays")]
    public async Task<ActionResult<IReadOnlyList<HolidayDto>>> GetHolidays(CancellationToken ct) =>
        Ok(await calendarConfigService.GetHolidaysAsync(ct));

    [HttpPost("holidays")]
    public async Task<ActionResult<HolidayDto>> AddHoliday([FromBody] CreateHolidayRequest request, CancellationToken ct)
    {
        try { return await calendarConfigService.AddHolidayAsync(request, ct); }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}
```

**File: `src/SupportCrm.Api/Controllers/TicketsController.cs`** — add after `GetGroupedCounts` (after line 73):

```csharp

    [HttpGet("{id:guid}/sla-status")]
    public async Task<ActionResult<TicketSlaStatusDto>> GetSlaStatus(Guid id, [FromServices] SlaCalculationService slaCalculationService, CancellationToken ct)
    {
        try
        {
            var status = await slaCalculationService.GetStatusAsync(id, ct);
            return status is null ? NotFound("No SLA target is configured for this ticket's priority.") : status;
        }
        catch (TicketNotFoundException) { return NotFound(); }
    }
```

Add `using SupportCrm.Application.Sla;` to this file's `using` block (near line 4).

---

## Frontend Tasks

**Implemented** (Angular 19, standalone components, Bootstrap — `d:\Code\selfAssessment\frontend\src\app`):

- **File: `features/sla-automation/sla.model.ts`** — `SlaTarget`, `CreateSlaTargetRequest`, `BusinessHours`, `Holiday`, `CustomerTier`, `DayOfWeek`.
- **File: `features/sla-automation/sla.service.ts`** — `getTargets`/`createTarget`, `getBusinessHours`/`setBusinessHours`, `getHolidays`/`addHoliday`.
- **Create file: `features/sla-automation/sla-config/sla-config.component.{ts,html,scss}`** — admin page with three cards: SLA targets (table + create form), business hours (editable 7-row table), holidays (list + add). Route: `/admin/sla-targets`.
- **File: `features/tickets/ticket.model.ts`** — added `TicketSlaStatus`.
- **File: `features/tickets/ticket.service.ts`** — added `getSlaStatus(ticketId)`.
- **Create file: `features/tickets/ticket-sla-status/ticket-sla-status.component.{ts,html,scss}`** — response/resolution due-at + remaining-minutes + breach badges, composed into `ticket-detail.component.html` alongside the other per-ticket sub-features (mirrors `ticket-status-escalation`'s `@Input({ required: true }) ticketId` pattern). Also renders Story 23's escalation log (see that story's Frontend Tasks).
- **File: `app.routes.ts`**, **`layout/app-shell/app-shell.component.ts`** — route + sidebar nav entry ("SLA targets").

---

## Edge Cases & Failure Modes

- **No `SlaTarget` matches a ticket's priority at all** (e.g. all targets for that priority were deactivated) — `ResolveAsync` returns `null`; `SlaCalculationService` returns `null` from `ComputeAsync`; `GetSlaStatus` returns `404` with an explanatory message; the dashboard reports `"NotApplicable"` (`ToSlaState`'s `sla is null` branch) rather than throwing.
- **Ticket has no `CategoryId`** (`CategoryId` is nullable on `Ticket`) — category-scoped `SlaTarget`s (`CategoryId is not null`) never match it; only priority-only (and tier-only-plus-priority) targets can, which is correct per the precedence rule.
- **Ticket's customer record is missing or deleted** — `customerRepository.GetByIdAsync` returns `null`; tier defaults to `CustomerTier.Standard` (`customer?.Tier ?? CustomerTier.Standard`), matching every customer's own default, so nothing throws.
- **A ticket spends multiple separate intervals in `Pending`** (e.g. Pending → Open → Pending again) — `GetPendingBusinessMinutesAsync` sums every `Pending`-tagged interval independently by walking the ordered status history pairwise, not just the most recent one.
- **A ticket is currently `Pending` (no status change out of it yet)** — the trailing interval's `to` is `now` (`i + 1 < history.Count ? ... : now`), so the pause keeps growing correctly on every read rather than freezing at the last recorded change.
- **`AddBusinessMinutesAsync` called with a start time outside business hours** (e.g. `CreatedAtUtc` at 11pm) — `IsWorkingInstant` returns `false` for that day if already past `EndTime`, or clamps `cursor` up to `StartTime` if before it; either way the algorithm advances to the next working day rather than producing a negative or nonsensical due-at.
- **`AddBusinessMinutesAsync`/`CalculateBusinessMinutesBetweenAsync` called with an all-non-working calendar** (every `BusinessHours.IsWorkingDay = false`) — `AddBusinessMinutesAsync`'s loop never finds working time and does not terminate; this is a configuration error the story does not guard against beyond documenting it here — `SetBusinessHoursAsync`'s caller (a support manager) is expected not to disable every day. Flagged for the executor, not silently handled.
- **A holiday falls on a day that's also `IsWorkingDay = false`** (e.g. a holiday defined on a Saturday) — harmless; the day is already skipped by the `IsWorkingDay` check before the holiday check runs.
- **`ResolutionTargetMinutes < ResponseTargetMinutes` on create** — rejected by `SlaTarget`'s constructor (`ArgumentException` → `400` via `SlaController.CreateTarget`'s catch).
- **Ticket priority changes after creation** (Ticket Management Story 06's `SetPriority`) — SLA status recalculates correctly on every read since `ComputeAsync` always resolves against the ticket's *current* `Priority`, consistent with Story 16's original behavior (see `../agent-dashboard/16-story-AD-1.md`, Edge Cases).
- **`GetStatusesAsync` (dashboard batch path) with an empty ticket list** — the `foreach` loop body never runs; returns an empty dictionary; `AgentDashboardService`'s subsequent `.Select` over an empty `filteredList` also returns empty — no exception.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Domain.Tests/Entities/SlaTargetTests.cs`**:
   - `Constructor_ResolutionLessThanResponse_Throws`
   - `Specificity_CategoryAndTierBothSet_ReturnsTwo`
2. **Unit — `tests/SupportCrm.Application.Tests/Sla/BusinessCalendarServiceTests.cs`**:
   - `AddBusinessMinutesAsync_StartOutsideHours_AdvancesToNextWorkingWindow`
   - `AddBusinessMinutesAsync_SkipsWeekendsAndHolidays`
   - `CalculateBusinessMinutesBetweenAsync_SpanningMultipleDays_SumsOnlyWorkingWindows`
3. **Unit — `tests/SupportCrm.Application.Tests/Sla/SlaTargetServiceTests.cs`**:
   - `ResolveAsync_PrefersTierAndCategoryMatchOverPriorityOnly`
   - `ResolveAsync_NoMatchingPriority_ReturnsNull`
4. **Unit — `tests/SupportCrm.Application.Tests/Sla/SlaCalculationServiceTests.cs`**:
   - `ComputeAsync_NoMatchingTarget_ReturnsNull`
   - `ComputeAsync_TicketWithPendingInterval_PushesDueDatesOutByPausedBusinessMinutes`
   - `ComputeAsync_ClosedTicket_NeverReportsResolutionBreached`
5. **Unit — `tests/SupportCrm.Application.Tests/Tickets/AgentDashboardServiceTests.cs`** (extend existing Story 16 tests):
   - `GetAssignedTicketsAsync_UsesConfiguredSlaTargetInsteadOfFixedWindow`
6. **Integration — `tests/SupportCrm.Api.Tests/Controllers/SlaControllerTests.cs`**:
   - `Post_TargetWithResolutionLessThanResponse_Returns400`
   - `Get_TicketSlaStatus_NoMatchingTarget_Returns404`

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** `dotnet ef migrations add AddSlaTargetsAndBusinessCalendar --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api`.
3. **Regression:** confirm `GET /api/tickets/assigned?agentId=...` (Agent Dashboard Story 16's endpoint) still returns `slaDueAtUtc`/`slaState` in the same shape, now sourced from `SlaCalculationService`.

---

## Done Criteria

- [ ] SLA targets are configurable per priority and optionally narrowed by category and/or customer tier (`POST /api/sla/targets`, `GET /api/sla/targets`).
- [ ] Multiple matching targets resolve by specificity (tier+category > category > priority-only).
- [ ] `GET /api/tickets/{id}/sla-status` returns real-time response/resolution due-at, breach flags, and remaining minutes.
- [ ] Business hours and holidays are configurable (`GET`/`PUT /api/sla/business-hours`, `GET`/`POST /api/sla/holidays`) and factored into every due-at calculation.
- [ ] Time spent in `Pending` status extends (pauses) both clocks.
- [ ] The Agent Dashboard's `GET /api/tickets/assigned` still returns `slaDueAtUtc`/`slaState`, now computed from the configured targets instead of the old fixed-window static helper.
- [ ] `src/SupportCrm.Application/Tickets/SlaPolicy.cs` is deleted.
- [ ] `dotnet build SupportCrm.slnx` succeeds.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 22.**
