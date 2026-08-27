# Story 47 — Audit logs (Story: SEC-3)

---

## Prerequisites

- Story 45 completed: [`45-story-SEC-1.md`](45-story-SEC-1.md) — JWT claims (`sub`, `email`) the filter reads to identify the actor.
- Story 46 completed: [`46-story-SEC-2.md`](46-story-SEC-2.md) — `RequirePermissionAttribute`, reused for this story's own read/export endpoints.
- Reports & Management Story 40 completed — `IReportExporter`, reused directly for this story's export action.

---

## Story Goal

1. `AuditLogEntry` — append-only, no update/delete path anywhere.
2. `AuditLoggingActionFilter` — registered **globally**, logs every mutating request (`POST`/`PUT`/`DELETE`/`PATCH`) across the **entire API**, every prior module included, without touching any of those controllers.
3. `GET /api/admin/audit-logs` — filterable by user, date range, action type (HTTP method).
4. `GET /api/admin/audit-logs/export` — reuses `IReportExporter`.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Reports/ReportDtos.cs`/`IReportExporter.cs` (Reports & Management) — reused verbatim, not reimplemented.
2. `src/SupportCrm.Api/Program.cs` — this story adds the global filter registration here.

---

## Backend Tasks

### 1 — Domain

**Create file: `src/SupportCrm.Domain/Entities/AuditLogEntry.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// Append-only by construction — no setters beyond the constructor, and no endpoint anywhere
// (including this feature's own) accepts an update or delete for this entity.
public class AuditLogEntry
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string UserEmail { get; private set; } = default!; // denormalized snapshot — survives the user later being deleted
    public string HttpMethod { get; private set; } = default!;
    public string Path { get; private set; } = default!;
    public string ActionSummary { get; private set; } = default!;
    public string? IpAddress { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }

    private AuditLogEntry() { }

    public AuditLogEntry(Guid? userId, string userEmail, string httpMethod, string path, string actionSummary, string? ipAddress, DateTimeOffset occurredAtUtc)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        UserEmail = string.IsNullOrWhiteSpace(userEmail) ? "anonymous" : userEmail;
        HttpMethod = httpMethod;
        Path = path;
        ActionSummary = actionSummary;
        IpAddress = ipAddress;
        OccurredAtUtc = occurredAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IAuditLogRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken ct);
    Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, `AuditLogService`

**File: `src/SupportCrm.Application/Security/SecurityDtos.cs`** — append:

```csharp
public record AuditLogQuery(Guid? UserId, DateTimeOffset? From, DateTimeOffset? To, string? ActionType);
public record AuditLogEntryDto(Guid Id, Guid? UserId, string UserEmail, string HttpMethod, string Path, string ActionSummary, string? IpAddress, DateTimeOffset OccurredAtUtc);
```

**Create file: `src/SupportCrm.Application/Security/AuditLogService.cs`**

```csharp
namespace SupportCrm.Application.Security;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;
using SupportCrm.Application.Reports;

public class AuditLogService(IAuditLogRepository repository, IReportExporter exporter, TimeProvider timeProvider)
{
    public async Task LogAsync(Guid? userId, string userEmail, string httpMethod, string path, string actionSummary, string? ipAddress, CancellationToken ct)
    {
        var entry = new AuditLogEntry(userId, userEmail, httpMethod, path, actionSummary, ipAddress, timeProvider.GetUtcNow());
        await repository.AddAsync(entry, ct);
        await repository.SaveChangesAsync(ct);
    }

    // "Action type" is the HTTP method (POST/PUT/DELETE/PATCH) — this codebase has no richer
    // action taxonomy, and the method itself already distinguishes creates/edits/deletes cleanly.
    public async Task<IReadOnlyList<AuditLogEntryDto>> GetLogsAsync(AuditLogQuery query, CancellationToken ct)
    {
        var all = await repository.GetAllAsync(ct);
        IEnumerable<AuditLogEntry> filtered = all;
        if (query.UserId is not null) filtered = filtered.Where(e => e.UserId == query.UserId);
        if (query.From is not null) filtered = filtered.Where(e => e.OccurredAtUtc >= query.From);
        if (query.To is not null) filtered = filtered.Where(e => e.OccurredAtUtc <= query.To);
        if (!string.IsNullOrWhiteSpace(query.ActionType)) filtered = filtered.Where(e => e.HttpMethod.Equals(query.ActionType, StringComparison.OrdinalIgnoreCase));
        return filtered.OrderByDescending(e => e.OccurredAtUtc).Select(ToDto).ToList();
    }

    public async Task<byte[]> ExportAsync(AuditLogQuery query, ReportExportFormat format, CancellationToken ct)
    {
        var logs = await GetLogsAsync(query, ct);
        var columns = new[] { "Timestamp (UTC)", "User", "Method", "Path", "Summary", "IP" };
        var rows = logs.Select(l => (IReadOnlyList<string>)new[] { l.OccurredAtUtc.ToString("u"), l.UserEmail, l.HttpMethod, l.Path, l.ActionSummary, l.IpAddress ?? "" }).ToList();
        var data = new ReportExportData("Audit log", columns, rows);
        return format == ReportExportFormat.Xlsx ? exporter.ExportToExcel(data) : exporter.ExportToPdf(data);
    }

    private static AuditLogEntryDto ToDto(AuditLogEntry e) => new(e.Id, e.UserId, e.UserEmail, e.HttpMethod, e.Path, e.ActionSummary, e.IpAddress, e.OccurredAtUtc);
}
```

### 3 — Infrastructure: EF config, repository, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add a `DbSet<AuditLogEntry>` and, in `OnModelCreating`:

```csharp
        modelBuilder.Entity<AuditLogEntry>(entity =>
        {
            entity.ToTable("AuditLogEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UserEmail).IsRequired().HasMaxLength(256);
            entity.Property(e => e.HttpMethod).IsRequired().HasMaxLength(16);
            entity.Property(e => e.Path).IsRequired().HasMaxLength(512);
            entity.Property(e => e.ActionSummary).IsRequired().HasMaxLength(256);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.HasIndex(e => e.OccurredAtUtc);
            entity.HasIndex(e => e.UserId);
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/AuditLogRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class AuditLogRepository(SupportCrmDbContext dbContext) : IAuditLogRepository
{
    public Task AddAsync(AuditLogEntry entry, CancellationToken ct) { dbContext.AuditLogEntries.Add(entry); return Task.CompletedTask; }
    public async Task<IReadOnlyList<AuditLogEntry>> GetAllAsync(CancellationToken ct) => await dbContext.AuditLogEntries.ToListAsync(ct);
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add `using SupportCrm.Application.Reports;` if not already present, and before `return services;`:

```csharp
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<AuditLogService>();
```

### 4 — Api: global `AuditLoggingActionFilter`, `AuditLogsController`

**Create file: `src/SupportCrm.Api/Security/AuditLoggingActionFilter.cs`**

```csharp
namespace SupportCrm.Api.Security;

using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using SupportCrm.Application.Security;

// Registered globally (Program.cs) — every mutating request across the WHOLE API is logged here,
// every prior module's controllers included, without any of them being touched. This is the
// ONLY code path anywhere that writes an AuditLogEntry — nothing else can create, edit, or
// delete one, which is what makes the entries read-only in practice, not just by convention.
public class AuditLoggingActionFilter(AuditLogService auditLogService) : IAsyncActionFilter
{
    private static readonly HashSet<string> MutatingMethods = new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "DELETE", "PATCH" };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        await next(); // log regardless of outcome — a failed/denied mutating attempt is itself worth recording

        var request = context.HttpContext.Request;
        if (!MutatingMethods.Contains(request.Method)) return;

        var userIdClaim = context.HttpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var emailClaim = context.HttpContext.User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var userId = Guid.TryParse(userIdClaim, out var id) ? id : (Guid?)null;

        var actionDescriptor = context.ActionDescriptor as ControllerActionDescriptor;
        var summary = actionDescriptor is not null ? $"{actionDescriptor.ControllerName}.{actionDescriptor.ActionName}" : request.Path.ToString();
        var ip = context.HttpContext.Connection.RemoteIpAddress?.ToString();

        await auditLogService.LogAsync(userId, emailClaim ?? "anonymous", request.Method, request.Path, summary, ip, context.HttpContext.RequestAborted);
    }
}
```

**File: `src/SupportCrm.Api/Program.cs`** — register the filter as a DI-resolved global filter (add near the other `builder.Services` calls, before `AddControllers`):

```csharp
builder.Services.AddScoped<SupportCrm.Api.Security.AuditLoggingActionFilter>();
```

Change the existing `AddControllers().AddJsonOptions(...)` call to add the filter:

```csharp
builder.Services.AddControllers(options =>
{
    options.Filters.AddService<SupportCrm.Api.Security.AuditLoggingActionFilter>();
}).AddJsonOptions(options =>
{
    ...
});
```

**Create file: `src/SupportCrm.Api/Controllers/AuditLogsController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Security;
using SupportCrm.Application.Security;
using SupportCrm.Application.Reports;

[ApiController]
[Route("api/admin/audit-logs")]
[Authorize]
public class AuditLogsController(AuditLogService auditLogService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Administration", "View")]
    public async Task<ActionResult<IReadOnlyList<AuditLogEntryDto>>> GetLogs(
        [FromQuery] Guid? userId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? actionType, CancellationToken ct) =>
        Ok(await auditLogService.GetLogsAsync(new AuditLogQuery(userId, from, to, actionType), ct));

    [HttpGet("export")]
    [RequirePermission("Administration", "Export")]
    public async Task<IActionResult> Export(
        [FromQuery] Guid? userId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, [FromQuery] string? actionType,
        [FromQuery] ReportExportFormat format, CancellationToken ct)
    {
        var bytes = await auditLogService.ExportAsync(new AuditLogQuery(userId, from, to, actionType), format, ct);
        var (contentType, fileName) = format == ReportExportFormat.Xlsx
            ? ("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "audit-log.xlsx")
            : ("application/pdf", "audit-log.pdf");
        return File(bytes, contentType, fileName);
    }
}
```

---

## Edge Cases & Failure Modes

- **An anonymous (unauthenticated) mutating request** — `POST /api/auth/login` itself, for example — logs with `UserEmail = "anonymous"`, `UserId = null` — never dropped just because there's no authenticated caller.
- **A request that throws an unhandled exception inside the action** — the filter's `await next()` still returns normally (ASP.NET Core's filter pipeline captures the exception on the context rather than propagating it synchronously through `next()`), so the log write still happens — an errored mutation is still worth a record.
- **A denied `[RequirePermission]` attempt** — still logged (it's still a mutating-verb request that reached the pipeline), giving a compliance officer visibility into attempted-but-blocked actions, not just successful ones.
- **Filtering by an `actionType` that doesn't match any logged method exactly** (case differences aside) — returns an empty list, not an error.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Security/AuditLogServiceTests.cs`**: `GetLogsAsync_FiltersByUserDateRangeAndActionType`.
2. **Integration — `tests/SupportCrm.Api.Tests/Controllers/AuditLogsControllerTests.cs`**: `Post_ToAnyMutatingEndpoint_CreatesAnAuditLogEntry` (calls an existing, unrelated Ticket Management endpoint and asserts a log entry appears — proving the global filter reaches prior modules).

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Manual smoke:** create a customer via the pre-existing `POST /api/customers` (Customer Management, untouched by this feature), then confirm `GET /api/admin/audit-logs` shows that exact request — proof the global filter reaches modules it never touched directly.

---

## Done Criteria

- [ ] Every mutating request across the whole API is logged with who/when/what, including prior modules' endpoints.
- [ ] Logs are filterable by user/date range/action type and exportable.
- [ ] No endpoint anywhere can edit or delete an audit log entry.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
