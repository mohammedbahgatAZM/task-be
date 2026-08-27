# Story 53 — Custom branding (Story: PL-5)

---

## Prerequisites

- Story 52 (`Branch`) — per-branch override scope.

---

## Story Goal

1. `BrandingSettings` (one row per scope: `BranchId = null` is global) — logo, two brand colors.
2. Real preview (`POST .../preview`, no persistence) then publish (`POST .../publish`), mirroring Security & Administration SEC-4's `validate`/`apply` split.
3. Logo upload/download mirroring the existing `Article`/`Guide`/`Ticket` attachment storage pattern exactly.
4. `GetEffectiveAsync` resolution: branch override → global → this app's own existing hardcoded brand colors.

---

## Context — Read These Files First

1. `src/SupportCrm.Infrastructure/Storage/LocalDiskArticleAttachmentStorage.cs` (all of it) — the exact shape `LocalDiskBrandingAssetStorage` copies.
2. `src/SupportCrm.Api/Controllers/ArticlesController.cs`, `UploadAttachment`/`DownloadAttachment` — the exact upload/download endpoint shape this story's logo endpoints copy.
3. `src/SupportCrm.Application/Security/SystemSettingsService.cs` (Security & Administration SEC-4) — the `validate`-never-persists / `apply`-re-validates-anyway pattern this story's `preview`/`publish` copies.
4. `frontend/src/styles.scss`, lines 6–7 — `$primary: #1565c0`, `$secondary: #5e35b1`, the literal fallback values used below.

---

## Backend Tasks

### 1 — Domain

**Create file: `src/SupportCrm.Domain/Entities/BrandingSettings.cs`**

```csharp
namespace SupportCrm.Domain.Entities;

// One row per scope — BranchId == null is the global default; a non-null BranchId overrides it
// for that branch only. Uniqueness per scope is enforced at the database level (a unique index
// on BranchId, treating null as its own single value via a filtered index — see the Infrastructure task).
public class BrandingSettings
{
    public Guid Id { get; private set; }
    public Guid? BranchId { get; private set; }
    public string? LogoStorageKey { get; private set; }
    public string? LogoContentType { get; private set; }
    public string PrimaryColorHex { get; private set; } = default!;
    public string SecondaryColorHex { get; private set; } = default!;
    public string UpdatedBy { get; private set; } = default!;
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private BrandingSettings() { }

    public BrandingSettings(Guid? branchId, string? logoStorageKey, string? logoContentType, string primaryColorHex, string secondaryColorHex, string updatedBy, DateTimeOffset updatedAtUtc)
    {
        Id = Guid.NewGuid();
        BranchId = branchId;
        LogoStorageKey = logoStorageKey;
        LogoContentType = logoContentType;
        PrimaryColorHex = primaryColorHex;
        SecondaryColorHex = secondaryColorHex;
        UpdatedBy = updatedBy;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void Update(string? logoStorageKey, string? logoContentType, string primaryColorHex, string secondaryColorHex, string updatedBy, DateTimeOffset updatedAtUtc)
    {
        LogoStorageKey = logoStorageKey;
        LogoContentType = logoContentType;
        PrimaryColorHex = primaryColorHex;
        SecondaryColorHex = secondaryColorHex;
        UpdatedBy = updatedBy;
        UpdatedAtUtc = updatedAtUtc;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IBrandingSettingsRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IBrandingSettingsRepository
{
    Task<BrandingSettings?> GetByBranchIdAsync(Guid? branchId, CancellationToken ct);
    Task AddAsync(BrandingSettings settings, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application

**File: `src/SupportCrm.Application/Platform/PlatformDtos.cs`** — append:

```csharp
public record BrandingPreviewRequest(Guid? BranchId, string? LogoStorageKey, string PrimaryColorHex, string SecondaryColorHex);
public record BrandingValidationDto(bool IsValid, IReadOnlyDictionary<string, string> Errors);
public record BrandingSettingsDto(Guid? BranchId, string? LogoUrl, string PrimaryColorHex, string SecondaryColorHex, bool IsDefault);
```

**Create file: `src/SupportCrm.Application/Platform/IBrandingAssetStorage.cs`**

```csharp
namespace SupportCrm.Application.Platform;

public interface IBrandingAssetStorage
{
    Task<string> SaveAsync(string fileName, Stream content, CancellationToken ct);
    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct);
}
```

**Create file: `src/SupportCrm.Application/Platform/BrandingService.cs`**

```csharp
namespace SupportCrm.Application.Platform;

using System.Text.RegularExpressions;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public partial class BrandingService(IBrandingSettingsRepository repository, TimeProvider timeProvider)
{
    // This app's own existing brand colors (frontend/src/styles.scss $primary/$secondary) — the
    // "nothing configured yet" fallback, so an unbranded system still looks like this app.
    private const string DefaultPrimary = "#1565c0";
    private const string DefaultSecondary = "#5e35b1";

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex HexColorPattern();

    public BrandingValidationDto Validate(BrandingPreviewRequest request)
    {
        var errors = new Dictionary<string, string>();
        if (!HexColorPattern().IsMatch(request.PrimaryColorHex)) errors["primaryColorHex"] = "Must be a hex color like #1565c0.";
        if (!HexColorPattern().IsMatch(request.SecondaryColorHex)) errors["secondaryColorHex"] = "Must be a hex color like #5e35b1.";
        return new BrandingValidationDto(errors.Count == 0, errors);
    }

    public async Task<BrandingSettingsDto> PublishAsync(BrandingPreviewRequest request, string publishedBy, CancellationToken ct)
    {
        var validation = Validate(request);
        if (!validation.IsValid) throw new ArgumentException(string.Join(" ", validation.Errors.Values));

        var now = timeProvider.GetUtcNow();
        var existing = await repository.GetByBranchIdAsync(request.BranchId, ct);
        if (existing is null)
        {
            var created = new BrandingSettings(request.BranchId, request.LogoStorageKey, null, request.PrimaryColorHex, request.SecondaryColorHex, publishedBy, now);
            await repository.AddAsync(created, ct);
        }
        else
        {
            existing.Update(request.LogoStorageKey, existing.LogoContentType, request.PrimaryColorHex, request.SecondaryColorHex, publishedBy, now);
        }
        await repository.SaveChangesAsync(ct);
        return await GetEffectiveAsync(request.BranchId, ct);
    }

    // Resolution order: this branch's own override -> the global (BranchId=null) row -> this
    // app's own hardcoded defaults. IsDefault=true means "nothing was ever configured," so the
    // frontend can distinguish a deliberately-unbranded system from one that just hasn't loaded yet.
    public async Task<BrandingSettingsDto> GetEffectiveAsync(Guid? branchId, CancellationToken ct)
    {
        var branchSpecific = branchId is not null ? await repository.GetByBranchIdAsync(branchId, ct) : null;
        var settings = branchSpecific ?? await repository.GetByBranchIdAsync(null, ct);
        if (settings is null) return new BrandingSettingsDto(branchId, null, DefaultPrimary, DefaultSecondary, true);
        return new BrandingSettingsDto(settings.BranchId, settings.LogoStorageKey, settings.PrimaryColorHex, settings.SecondaryColorHex, false);
    }
}
```

**Create file: `src/SupportCrm.Infrastructure/Storage/LocalDiskBrandingAssetStorage.cs`** — copies `LocalDiskArticleAttachmentStorage` exactly, minus the per-entity subfolder (branding has no owning entity id):

```csharp
namespace SupportCrm.Infrastructure.Storage;

using Microsoft.Extensions.Options;
using SupportCrm.Application.Platform;

public class LocalDiskBrandingAssetStorageOptions
{
    public const string SectionName = "BrandingAssets";
    public string RootPath { get; set; } = "App_Data/branding-assets";
}

public class LocalDiskBrandingAssetStorage(IOptions<LocalDiskBrandingAssetStorageOptions> options) : IBrandingAssetStorage
{
    public async Task<string> SaveAsync(string fileName, Stream content, CancellationToken ct)
    {
        Directory.CreateDirectory(options.Value.RootPath);
        var storageKey = $"{Guid.NewGuid()}_{Path.GetFileName(fileName)}";
        var fullPath = Path.Combine(options.Value.RootPath, storageKey);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, ct);

        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct) =>
        Task.FromResult<Stream>(File.OpenRead(Path.Combine(options.Value.RootPath, storageKey)));
}
```

### 3 — Infrastructure: EF config, repository, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add `DbSet<BrandingSettings>` and:

```csharp
        modelBuilder.Entity<BrandingSettings>(entity =>
        {
            entity.ToTable("BrandingSettings");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.PrimaryColorHex).IsRequired().HasMaxLength(7);
            entity.Property(b => b.SecondaryColorHex).IsRequired().HasMaxLength(7);
            entity.Property(b => b.LogoStorageKey).HasMaxLength(512);
            entity.Property(b => b.UpdatedBy).IsRequired().HasMaxLength(256);
            entity.HasIndex(b => b.BranchId).IsUnique();
        });
```

**Create file: `src/SupportCrm.Infrastructure/Persistence/BrandingSettingsRepository.cs`** — standard EF repo (`GetByBranchIdAsync` via `FirstOrDefaultAsync(b => b.BranchId == branchId, ct)`).

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — `services.AddScoped<IBrandingSettingsRepository, BrandingSettingsRepository>();`, `services.AddScoped<IBrandingAssetStorage, LocalDiskBrandingAssetStorage>();`, `services.AddScoped<BrandingService>();`.

**File: `src/SupportCrm.Api/Program.cs`** — add `builder.Services.Configure<LocalDiskBrandingAssetStorageOptions>(builder.Configuration.GetSection(LocalDiskBrandingAssetStorageOptions.SectionName));`, matching the existing `LocalDisk*AttachmentStorageOptions` registrations already there.

### 4 — Api

**Create file: `src/SupportCrm.Api/Controllers/BrandingController.cs`** (`api/branding`)

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Mvc;
using SupportCrm.Application.Platform;

[ApiController]
[Route("api/branding")]
public class BrandingController(BrandingService brandingService, IBrandingAssetStorage assetStorage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<BrandingSettingsDto>> GetEffective([FromQuery] Guid? branchId, CancellationToken ct) =>
        await brandingService.GetEffectiveAsync(branchId, ct);

    [HttpPost("preview")]
    public ActionResult<BrandingValidationDto> Preview([FromBody] BrandingPreviewRequest request) => brandingService.Validate(request);

    [HttpPost("publish")]
    public async Task<ActionResult<BrandingSettingsDto>> Publish([FromBody] BrandingPreviewRequest request, CancellationToken ct)
    {
        try { return await brandingService.PublishAsync(request, "admin", ct); }
        catch (ArgumentException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("logo")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<string>> UploadLogo(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("A file is required.");
        await using var stream = file.OpenReadStream();
        return await assetStorage.SaveAsync(file.FileName, stream, ct);
    }

    [HttpGet("logo/{storageKey}")]
    public async Task<IActionResult> DownloadLogo(string storageKey, CancellationToken ct)
    {
        try { return File(await assetStorage.OpenReadAsync(storageKey, ct), "image/*"); }
        catch (FileNotFoundException) { return NotFound(); }
    }
}
```

**File: `src/SupportCrm.Api/appsettings.json`** — add:

```json
  "BrandingAssets": { "RootPath": "App_Data/branding-assets" }
```

---

## Edge Cases & Failure Modes

- **`preview` with an invalid hex color** — returns `IsValid: false` with a per-field error, nothing persisted (`Validate` never calls the repository at all).
- **`publish` called directly without a prior `preview`** — still safe; `PublishAsync` calls `Validate` internally first and throws before touching the database on invalid input, same defense-in-depth as SEC-4's `SystemSettingsService.ApplyAsync`.
- **`GetEffective` for a branch that has no override and no global row exists either** — returns the hardcoded app defaults with `IsDefault: true`, never a `404` — there's always *something* to render.
- **Publishing global branding (`BranchId: null`) after a branch already has its own override** — the branch's override is untouched; `GetByBranchIdAsync(branchId)` still finds the branch-specific row first, so publishing the global default never silently overrides an already-configured branch.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Platform/BrandingServiceTests.cs`**: `GetEffectiveAsync_NoRowsExist_ReturnsHardcodedDefaults`; `GetEffectiveAsync_BranchOverrideExists_WinsOverGlobal`; `Validate_InvalidHex_ReturnsFieldError`.

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx`.
2. **Migration:** part of this feature's single `AddPlatform` migration (see Story 52's Verification Steps).

---

## Done Criteria

- [ ] Logo upload + color publish, with a real no-persistence preview step.
- [ ] Per-branch override falls back to global, falls back to this app's own existing brand colors.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
