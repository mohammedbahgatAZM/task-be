# Story 48 — System configuration (Story: SEC-4)

---

## Prerequisites

- Story 46 completed: [`46-story-SEC-2.md`](46-story-SEC-2.md) — `RequirePermissionAttribute`, this story's own endpoints are admin-only.
- SLA & Automation Story 22 — `BusinessCalendarConfigService`/`BusinessHours`/`Holiday` — **reused entirely unmodified**, not rebuilt.

---

## Story Goal

1. `SystemSetting` — a small, generic key/value store for the settings the AC names that don't already exist: supported languages, notification defaults.
2. `SystemSettingCatalog` — a fixed, code-defined catalog (key, display name, type, validator) — not a fully generic schema.
3. A real two-step preview flow: `POST validate` (dry run, no persistence) then `POST apply` (persists, re-validates internally regardless).
4. Confirm (no new code) that business hours/holidays already satisfy "configured centrally" via SLA & Automation's existing endpoints.

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Sla/BusinessCalendarConfigService.cs` — read, not modified.
2. `src/SupportCrm.Api/Controllers/SlaController.cs`, `business-hours`/`holidays` actions — the existing endpoints this story's frontend page surfaces alongside its own new settings, unmodified.

---

## Backend Tasks

### 1 — Domain

**Create file: `src/SupportCrm.Domain/Entities/SystemSetting.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

public class SystemSetting
{
    public Guid Id { get; private set; }
    public string Key { get; private set; } = default!;
    public string Value { get; private set; } = default!;
    public string UpdatedBy { get; private set; } = default!;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private SystemSetting() { }

    public SystemSetting(string key, string value, string updatedBy, DateTimeOffset updatedAtUtc)
    {
        Id = Guid.NewGuid();
        Key = key;
        Value = value;
        UpdatedBy = updatedBy;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void SetValue(string value, string updatedBy, DateTimeOffset updatedAtUtc)
    {
        Value = value;
        UpdatedBy = updatedBy;
        UpdatedAtUtc = updatedAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/ISystemSettingRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface ISystemSettingRepository
{
    Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken ct);
    Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct);
    Task AddAsync(SystemSetting setting, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, catalog, `SystemSettingsService`

**File: `src/SupportCrm.Application/Security/SecurityDtos.cs`** — append:

```csharp
public record SystemSettingDto(string Key, string DisplayName, string ValueType, string? Value);
public record ValidateSettingsRequest(IReadOnlyDictionary<string, string> Changes);
public record ValidationResultDto(bool IsValid, IReadOnlyDictionary<string, string> Errors);
public record ApplySettingsRequest(IReadOnlyDictionary<string, string> Changes);
```

**Create file: `src/SupportCrm.Application/Security/SystemSettingCatalog.cs`**

```csharp
namespace SupportCrm.Application.Security;

using System.Text.Json;

public record SystemSettingDefinition(string Key, string DisplayName, string ValueType, string DefaultValue, Func<string, string?> Validate);

// A fixed, code-defined catalog — not a fully generic admin-authorable schema. Adding a fourth
// setting means adding one entry here, not a migration; this is a deliberate scope boundary
// (see this story's own intake note), not a limitation anyone hit by accident.
public static class SystemSettingCatalog
{
    public static readonly IReadOnlyList<SystemSettingDefinition> Definitions = new List<SystemSettingDefinition>
    {
        new("SupportedLanguages", "Supported languages", "Json", "[\"en\",\"ar\"]", ValidateLanguages),
        new("NotifyCustomerOnStatusChangeByDefault", "Notify customer on status change by default", "Bool", "true", ValidateBool),
        new("NotifyCustomerOnResolutionByDefault", "Notify customer on resolution by default", "Bool", "true", ValidateBool)
    };

    public static SystemSettingDefinition? Find(string key) => Definitions.FirstOrDefault(d => d.Key == key);

    private static string? ValidateBool(string value) => bool.TryParse(value, out _) ? null : "Must be 'true' or 'false'.";

    private static string? ValidateLanguages(string value)
    {
        List<string>? codes;
        try { codes = JsonSerializer.Deserialize<List<string>>(value); }
        catch { return "Must be a JSON array of language codes, e.g. [\"en\",\"ar\"]."; }
        if (codes is null || codes.Count == 0) return "At least one language is required.";
        if (codes.Any(c => c.Length != 2)) return "Each language code must be 2 letters (ISO 639-1), e.g. \"en\".";
        return null;
    }
}
```

**Create file: `src/SupportCrm.Application/Security/SystemSettingsService.cs`**

```csharp
namespace SupportCrm.Application.Security;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SystemSettingsService(ISystemSettingRepository repository, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken ct)
    {
        var stored = (await repository.GetAllAsync(ct)).ToDictionary(s => s.Key, s => s.Value);
        return SystemSettingCatalog.Definitions
            .Select(d => new SystemSettingDto(d.Key, d.DisplayName, d.ValueType, stored.TryGetValue(d.Key, out var v) ? v : d.DefaultValue))
            .ToList();
    }

    // True dry run — never touches the repository. The frontend always calls this before
    // offering "Apply"; Apply itself re-validates anyway (see below), so this is a genuine
    // preview, not the only thing standing between a bad value and the database.
    public Task<ValidationResultDto> ValidateAsync(ValidateSettingsRequest request, CancellationToken ct)
    {
        var errors = new Dictionary<string, string>();
        foreach (var (key, value) in request.Changes)
        {
            var definition = SystemSettingCatalog.Find(key);
            if (definition is null) { errors[key] = "Unknown setting key."; continue; }
            var error = definition.Validate(value);
            if (error is not null) errors[key] = error;
        }
        return Task.FromResult(new ValidationResultDto(errors.Count == 0, errors));
    }

    public async Task<ValidationResultDto> ApplyAsync(ApplySettingsRequest request, string changedBy, CancellationToken ct)
    {
        var validation = await ValidateAsync(new ValidateSettingsRequest(request.Changes), ct);
        if (!validation.IsValid) return validation; // nothing persisted

        var now = timeProvider.GetUtcNow();
        foreach (var (key, value) in request.Changes)
        {
            var existing = await repository.GetByKeyAsync(key, ct);
            if (existing is null) await repository.AddAsync(new SystemSetting(key, value, changedBy, now), ct);
            else existing.SetValue(value, changedBy, now);
        }
        await repository.SaveChangesAsync(ct);
        return validation;
    }
}
```

### 3 — Infrastructure: EF config, repository, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add a `DbSet<SystemSetting>` and, in `OnModelCreating`:

```csharp
        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.ToTable("SystemSettings");
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Key).IsRequired().HasMaxLength(128);
            entity.HasIndex(s => s.Key).IsUnique();
            entity.Property(s => s.Value).IsRequired();
            entity.Property(s => s.UpdatedBy).IsRequired().HasMaxLength(256);
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/SystemSettingRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class SystemSettingRepository(SupportCrmDbContext dbContext) : ISystemSettingRepository
{
    public async Task<IReadOnlyList<SystemSetting>> GetAllAsync(CancellationToken ct) => await dbContext.SystemSettings.ToListAsync(ct);
    public Task<SystemSetting?> GetByKeyAsync(string key, CancellationToken ct) => dbContext.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
    public Task AddAsync(SystemSetting setting, CancellationToken ct) { dbContext.SystemSettings.Add(setting); return Task.CompletedTask; }
    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<ISystemSettingRepository, SystemSettingRepository>();
        services.AddScoped<SystemSettingsService>();
```

### 4 — Api: `SystemSettingsController`

**Create file: `src/SupportCrm.Api/Controllers/SystemSettingsController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Security;
using SupportCrm.Application.Security;

[ApiController]
[Route("api/admin/system-settings")]
[Authorize]
public class SystemSettingsController(SystemSettingsService systemSettingsService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Administration", "View")]
    public async Task<ActionResult<IReadOnlyList<SystemSettingDto>>> GetAll(CancellationToken ct) => Ok(await systemSettingsService.GetAllAsync(ct));

    [HttpPost("validate")]
    [RequirePermission("Administration", "Edit")]
    public async Task<ActionResult<ValidationResultDto>> Validate([FromBody] ValidateSettingsRequest request, CancellationToken ct) =>
        Ok(await systemSettingsService.ValidateAsync(request, ct));

    [HttpPost("apply")]
    [RequirePermission("Administration", "Edit")]
    public async Task<ActionResult<ValidationResultDto>> Apply([FromBody] ApplySettingsRequest request, CancellationToken ct)
    {
        var changedBy = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email)?.Value ?? "unknown";
        var result = await systemSettingsService.ApplyAsync(request, changedBy, ct);
        return result.IsValid ? Ok(result) : BadRequest(result);
    }
}
```

---

## Edge Cases & Failure Modes

- **`apply` called with a value that fails validation** — `400` with the same per-key error shape `validate` returns; nothing is persisted, including any *other* keys in the same request that would have been valid — an apply is all-or-nothing, not partially applied.
- **`validate`/`apply` called with an unknown key** — `"Unknown setting key."` for that key, not a silent no-op and not a crash.
- **Calling `apply` without ever calling `validate` first** — still safe; `ApplyAsync` re-validates internally regardless of what the frontend did, so a skipped preview can never reach the database with an invalid value.
- **`SupportedLanguages` set to an empty array** — rejected ("At least one language is required.") — the CRM always needs at least one active language.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Security/SystemSettingsServiceTests.cs`**: `ApplyAsync_InvalidValue_PersistsNothing`; `ApplyAsync_NeverCalledValidateFirst_StillRejectsInvalidInput`.

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration:** covered by Story 45/46's single consolidated `AddSecurityAndAdministration` migration — this story's `SystemSetting` table is included in that same migration if authored before it's generated, or as a small follow-up `AddSystemSettings` migration otherwise (see Verification Steps in Story 45).

---

## Done Criteria

- [ ] Languages and notification defaults are centrally configurable; business hours/holidays confirmed already reachable from the same page (no backend change).
- [ ] `validate` never persists; `apply` re-validates internally and is all-or-nothing.
- [ ] Applying a change is automatically captured by SEC-3's audit filter (no bespoke logging code in this story).
- [ ] `dotnet build SupportCrm.slnx` succeeds.
