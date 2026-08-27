# Story 18 — Tasks and reminders (Story: AD-3)

---

## Prerequisites

- Story 16 completed (`16-story-AD-1.md`) — the "acting as" agent-identity parameter pattern.

---

## Story Goal

1. `TicketTask` entity: a note + due date + assignee, linked to a ticket.
2. A shared, polling-based `AgentNotification` mechanism — this story's own use is "task due", but the service is written generically so Story 20 (@-mentions) reuses it without modification.
3. Task reassignment (`AssignedAgentId` update only).

**Team decision, restated from the intake:** there is no background job scheduler in this app, and this story does not add one. Due-task notifications are computed **lazily** — materialized the moment an agent's notifications are polled, not on a timer. Flag this plainly; it is a deliberate scope decision, not a hidden shortcut.

---

## Context — Read These Files First

1. `src/SupportCrm.Domain/Entities/Ticket.cs`, `Agent.cs` — the entities `TicketTask`/`AgentNotification` reference by id.
2. `src/SupportCrm.Api/Controllers/TicketsController.cs` — this story's task endpoints are added here, following its `[FromServices]`-per-action pattern for newly introduced services.

---

## Backend Tasks

### 1 — Domain: two new entities, two new repositories

**Create file: `src/SupportCrm.Domain/Entities/TicketTask.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class TicketTask
{
    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public string Note { get; private set; } = default!;
    public DateTimeOffset DueAtUtc { get; private set; }
    public Guid AssignedAgentId { get; private set; }
    public bool IsCompleted { get; private set; }
    public DateTimeOffset? NotifiedAtUtc { get; private set; } // set once a "task due" notification has fired — prevents re-notifying on every poll
    public string CreatedBy { get; private set; } = default!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private TicketTask() { } // EF Core

    public TicketTask(Guid ticketId, string note, DateTimeOffset dueAtUtc, Guid assignedAgentId, string createdBy, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("Task note is required.", nameof(note));

        Id = Guid.NewGuid();
        TicketId = ticketId;
        Note = note;
        DueAtUtc = dueAtUtc;
        AssignedAgentId = assignedAgentId;
        CreatedBy = createdBy;
        CreatedAtUtc = createdAtUtc;
    }

    public void Complete() => IsCompleted = true;

    public void Reassign(Guid newAgentId) => AssignedAgentId = newAgentId;

    public void MarkNotified(DateTimeOffset atUtc) => NotifiedAtUtc = atUtc;
}
```

**Create file: `src/SupportCrm.Domain/Entities/AgentNotification.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class AgentNotification
{
    public Guid Id { get; private set; }
    public Guid AgentId { get; private set; }
    public string Kind { get; private set; } = default!; // "TaskDue" | "Mention" (Story 20)
    public string Message { get; private set; } = default!;
    public Guid? RelatedTicketId { get; private set; }
    public bool IsRead { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private AgentNotification() { } // EF Core

    public AgentNotification(Guid agentId, string kind, string message, Guid? relatedTicketId, DateTimeOffset createdAtUtc)
    {
        if (string.IsNullOrWhiteSpace(message))
            throw new ArgumentException("Notification message is required.", nameof(message));

        Id = Guid.NewGuid();
        AgentId = agentId;
        Kind = kind;
        Message = message;
        RelatedTicketId = relatedTicketId;
        CreatedAtUtc = createdAtUtc;
    }

    public void MarkRead() => IsRead = true;
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ITicketTaskRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ITicketTaskRepository
{
    Task<TicketTask?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<TicketTask>> GetByTicketAsync(Guid ticketId, CancellationToken ct);
    Task<IReadOnlyList<TicketTask>> GetByAgentAsync(Guid agentId, CancellationToken ct);
    Task AddAsync(TicketTask task, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IAgentNotificationRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IAgentNotificationRepository
{
    Task<AgentNotification?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<AgentNotification>> GetByAgentAsync(Guid agentId, CancellationToken ct);
    Task AddAsync(AgentNotification notification, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, services

**Create file: `src/SupportCrm.Application/Tickets/TicketTaskDtos.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

public record CreateTicketTaskRequest(string Note, DateTimeOffset DueAtUtc, Guid AssignedAgentId, string CreatedBy);
public record TicketTaskDto(Guid Id, Guid TicketId, string Note, DateTimeOffset DueAtUtc, Guid AssignedAgentId, bool IsCompleted, DateTimeOffset CreatedAtUtc);
public record ReassignTicketTaskRequest(Guid NewAgentId);
public record AgentNotificationDto(Guid Id, string Kind, string Message, Guid? RelatedTicketId, bool IsRead, DateTimeOffset CreatedAtUtc);
```

**Create file: `src/SupportCrm.Application/Tickets/TicketTaskService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class TicketTaskService(ITicketTaskRepository repository, TimeProvider timeProvider)
{
    public async Task<TicketTaskDto> CreateAsync(Guid ticketId, CreateTicketTaskRequest request, CancellationToken ct)
    {
        var task = new TicketTask(ticketId, request.Note, request.DueAtUtc, request.AssignedAgentId, request.CreatedBy, timeProvider.GetUtcNow());
        await repository.AddAsync(task, ct);
        await repository.SaveChangesAsync(ct);
        return ToDto(task);
    }

    public async Task<IReadOnlyList<TicketTaskDto>> GetForTicketAsync(Guid ticketId, CancellationToken ct) =>
        (await repository.GetByTicketAsync(ticketId, ct)).OrderBy(t => t.DueAtUtc).Select(ToDto).ToList();

    public async Task<IReadOnlyList<TicketTaskDto>> GetOverdueForAgentAsync(Guid agentId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        return (await repository.GetByAgentAsync(agentId, ct))
            .Where(t => !t.IsCompleted && t.DueAtUtc < now)
            .OrderBy(t => t.DueAtUtc)
            .Select(ToDto)
            .ToList();
    }

    public async Task CompleteAsync(Guid taskId, CancellationToken ct)
    {
        var task = await repository.GetByIdAsync(taskId, ct) ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");
        task.Complete();
        await repository.SaveChangesAsync(ct);
    }

    public async Task ReassignAsync(Guid taskId, Guid newAgentId, CancellationToken ct)
    {
        var task = await repository.GetByIdAsync(taskId, ct) ?? throw new KeyNotFoundException($"Task '{taskId}' was not found.");
        task.Reassign(newAgentId);
        await repository.SaveChangesAsync(ct);
    }

    private static TicketTaskDto ToDto(TicketTask t) => new(t.Id, t.TicketId, t.Note, t.DueAtUtc, t.AssignedAgentId, t.IsCompleted, t.CreatedAtUtc);
}
```

**Create file: `src/SupportCrm.Application/Tickets/AgentNotificationService.cs`**

```csharp
namespace SupportCrm.Application.Tickets;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AgentNotificationService(
    IAgentNotificationRepository notificationRepository,
    ITicketTaskRepository taskRepository,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<AgentNotificationDto>> GetForAgentAsync(Guid agentId, CancellationToken ct)
    {
        await MaterializeDueTaskNotificationsAsync(agentId, ct);
        return (await notificationRepository.GetByAgentAsync(agentId, ct))
            .OrderByDescending(n => n.CreatedAtUtc)
            .Select(ToDto)
            .ToList();
    }

    public async Task MarkReadAsync(Guid notificationId, CancellationToken ct)
    {
        var notification = await notificationRepository.GetByIdAsync(notificationId, ct)
            ?? throw new KeyNotFoundException($"Notification '{notificationId}' was not found.");
        notification.MarkRead();
        await notificationRepository.SaveChangesAsync(ct);
    }

    // Reused as-is by Story 20 (@-mentions) — the one shared way any part of this app
    // creates an agent notification. Do not add a second, parallel mechanism there.
    public async Task NotifyAsync(Guid agentId, string kind, string message, Guid? relatedTicketId, CancellationToken ct)
    {
        await notificationRepository.AddAsync(new AgentNotification(agentId, kind, message, relatedTicketId, timeProvider.GetUtcNow()), ct);
        await notificationRepository.SaveChangesAsync(ct);
    }

    private async Task MaterializeDueTaskNotificationsAsync(Guid agentId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        var dueTasks = (await taskRepository.GetByAgentAsync(agentId, ct))
            .Where(t => !t.IsCompleted && t.NotifiedAtUtc is null && t.DueAtUtc <= now)
            .ToList();

        if (dueTasks.Count == 0) return;

        foreach (var task in dueTasks)
        {
            await notificationRepository.AddAsync(new AgentNotification(agentId, "TaskDue", $"Task due: {task.Note}", task.TicketId, now), ct);
            task.MarkNotified(now);
        }
        await taskRepository.SaveChangesAsync(ct);
    }

    private static AgentNotificationDto ToDto(AgentNotification n) => new(n.Id, n.Kind, n.Message, n.RelatedTicketId, n.IsRead, n.CreatedAtUtc);
}
```

### 3 — Infrastructure: EF config, repositories, DI

**File: `SupportCrmDbContext.cs`** — add `DbSet<TicketTask> TicketTasks` and `DbSet<AgentNotification> AgentNotifications`, plus in `OnModelCreating`:

```csharp
        modelBuilder.Entity<TicketTask>(entity =>
        {
            entity.ToTable("TicketTasks");
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Note).IsRequired();
            entity.HasIndex(t => t.TicketId);
            entity.HasIndex(t => t.AssignedAgentId);
        });

        modelBuilder.Entity<AgentNotification>(entity =>
        {
            entity.ToTable("AgentNotifications");
            entity.HasKey(n => n.Id);
            entity.Property(n => n.Kind).IsRequired().HasMaxLength(32);
            entity.Property(n => n.Message).IsRequired();
            entity.HasIndex(n => n.AgentId);
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/TicketTaskRepository.cs`** and **`AgentNotificationRepository.cs`** — straightforward EF implementations mirroring `TicketMessageRepository`'s shape (`FirstOrDefaultAsync`/`Where(...).ToListAsync`/`Add`/`SaveChangesAsync`).

**File: `DependencyInjection.cs`** — add:

```csharp
        services.AddScoped<ITicketTaskRepository, TicketTaskRepository>();
        services.AddScoped<TicketTaskService>();
        services.AddScoped<IAgentNotificationRepository, AgentNotificationRepository>();
        services.AddScoped<AgentNotificationService>();
```

### 4 — Api: controllers

**File: `TicketsController.cs`** — add:

```csharp
    [HttpPost("{id:guid}/tasks")]
    public async Task<ActionResult<TicketTaskDto>> CreateTask(Guid id, [FromBody] CreateTicketTaskRequest request, [FromServices] TicketTaskService taskService, CancellationToken ct) =>
        await taskService.CreateAsync(id, request, ct);

    [HttpGet("{id:guid}/tasks")]
    public async Task<ActionResult<IReadOnlyList<TicketTaskDto>>> GetTasks(Guid id, [FromServices] TicketTaskService taskService, CancellationToken ct) =>
        Ok(await taskService.GetForTicketAsync(id, ct));

    [HttpGet("tasks/overdue")]
    public async Task<ActionResult<IReadOnlyList<TicketTaskDto>>> GetOverdueTasks([FromQuery] Guid agentId, [FromServices] TicketTaskService taskService, CancellationToken ct) =>
        Ok(await taskService.GetOverdueForAgentAsync(agentId, ct));

    [HttpPut("tasks/{taskId:guid}/complete")]
    public async Task<IActionResult> CompleteTask(Guid taskId, [FromServices] TicketTaskService taskService, CancellationToken ct)
    {
        try { await taskService.CompleteAsync(taskId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [HttpPut("tasks/{taskId:guid}/reassign")]
    public async Task<IActionResult> ReassignTask(Guid taskId, [FromBody] ReassignTicketTaskRequest request, [FromServices] TicketTaskService taskService, CancellationToken ct)
    {
        try { await taskService.ReassignAsync(taskId, request.NewAgentId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
```

**Create file: `src/SupportCrm.Api/Controllers/AgentNotificationsController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Tickets;

[ApiController]
[Route("api/agents/{agentId:guid}/notifications")]
public class AgentNotificationsController(AgentNotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentNotificationDto>>> GetAll(Guid agentId, CancellationToken ct) =>
        Ok(await notificationService.GetForAgentAsync(agentId, ct));

    [HttpPut("{notificationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid agentId, Guid notificationId, CancellationToken ct)
    {
        try { await notificationService.MarkReadAsync(notificationId, ct); return NoContent(); }
        catch (KeyNotFoundException) { return NotFound(); }
    }
}
```

---

## Edge Cases & Failure Modes

- **Route ordering:** `GET api/tickets/tasks/overdue` and `PUT api/tickets/tasks/{taskId:guid}/complete` do not collide with `GET/POST api/tickets/{id:guid}/tasks` — `{id:guid}` requires a real GUID segment, and `tasks` is not one, so ASP.NET Core's routing disambiguates correctly. No explicit route ordering is required, but the executor should still register the literal `tasks/...` routes and verify with a real request rather than assuming.
- **Polling an agent with no due tasks** — `MaterializeDueTaskNotificationsAsync` finds nothing, does nothing; `GetForAgentAsync` just returns existing notifications (possibly empty). No wasted writes.
- **Same task polled twice after becoming due** — `NotifiedAtUtc` is set the first time, so the second poll's `Where` filter excludes it; no duplicate notifications for the same task.
- **Reassigning a task to a new agent doesn't move its `NotifiedAtUtc`/history** — intentional; the AC only requires reassignment to update who owns it, not to reset notification state.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Tickets/TicketTaskServiceTests.cs`**:
   - `CreateAsync_CreatesTaskLinkedToTicket`
   - `GetOverdueForAgentAsync_ExcludesCompletedAndNotYetDue`
   - `ReassignAsync_UpdatesAssignedAgent`
2. **Unit — `tests/SupportCrm.Application.Tests/Tickets/AgentNotificationServiceTests.cs`**:
   - `GetForAgentAsync_MaterializesNotificationForDueTask`
   - `GetForAgentAsync_DoesNotDuplicateNotificationOnSecondPoll`
   - `NotifyAsync_CreatesNotificationDirectly` (the path Story 20 reuses)

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Manual smoke:** create a task due in the past, poll that agent's notifications, confirm a `"TaskDue"` notification appears exactly once across repeated polls.

---

## Done Criteria

- [ ] Tasks can be created (note + due date + assignee), listed per ticket, completed, and reassigned.
- [ ] Overdue tasks are queryable per agent (for the AD-1 dashboard to highlight).
- [ ] Polling an agent's notifications lazily materializes due-task notifications exactly once each.
- [ ] `dotnet build SupportCrm.slnx` succeeds. Migration needed: new `TicketTasks`, `AgentNotifications` tables.
