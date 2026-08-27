# Story 46 — Permissions (Story: SEC-2)

---

## Prerequisites

- Story 45 completed: [`45-story-SEC-1.md`](45-story-SEC-1.md) — `User`, `Role`, `IUserRepository`/`IRoleRepository`, JWT auth pipeline, `SecurityDtos.cs`.

---

## Story Goal

1. `Permission`/`RolePermission` entities, seeded as a fixed (Module × Action) catalog — 8 modules × 5 actions = 40 rows.
2. `IPermissionChecker` — resolves a role-id set (from the JWT) to granted permissions **live from the database**, so permission changes take effect without a fresh login.
3. `[RequirePermission(module, action)]` — a small action filter, applied to every SEC-1/SEC-2 endpoint; blocks unauthorized calls with a clear `403` message.
4. Role CRUD (custom roles beyond the four seeded defaults) + permission assignment.
5. `GET /api/auth/me` — the frontend's own source of truth for "what can I do."

---

## Context — Read These Files First

1. `src/SupportCrm.Application/Security/JwtTokenService.cs` (Story 45) — `RoleIdClaimType`, the claim `RequirePermissionAttribute` reads.
2. `src/SupportCrm.Api/Controllers/UsersController.cs` (Story 45) — the controller this story adds `[RequirePermission]` to, action by action.

---

## Backend Tasks

### 1 — Domain

**Create files:**

`src/SupportCrm.Domain/Entities/Permission.cs`:
```csharp
namespace SupportCrm.Domain.Entities;

public class Permission
{
    public Guid Id { get; private set; }
    public string Module { get; private set; } = default!;
    public string Action { get; private set; } = default!; // "View" | "Create" | "Edit" | "Delete" | "Export"

    private Permission() { }

    public Permission(string module, string action)
    {
        Id = Guid.NewGuid();
        Module = module;
        Action = action;
    }
}
```

`src/SupportCrm.Domain/Entities/RolePermission.cs`:
```csharp
namespace SupportCrm.Domain.Entities;

public class RolePermission
{
    public Guid Id { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    private RolePermission() { }

    public RolePermission(Guid roleId, Guid permissionId)
    {
        Id = Guid.NewGuid();
        RoleId = roleId;
        PermissionId = permissionId;
    }
}
```

**Create file: `src/SupportCrm.Domain/Repositories/IPermissionRepository.cs`**

```csharp
namespace SupportCrm.Domain.Repositories;

using SupportCrm.Domain.Entities;

public interface IPermissionRepository
{
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<Guid>> GetPermissionIdsForRolesAsync(IReadOnlyList<Guid> roleIds, CancellationToken ct);
    Task SetRolePermissionsAsync(Guid roleId, IReadOnlyList<Guid> permissionIds, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}
```

### 2 — Application: DTOs, `PermissionChecker`, `RoleManagementService`

**File: `src/SupportCrm.Application/Security/SecurityDtos.cs`** — append:

```csharp
public record PermissionDto(Guid Id, string Module, string Action);
public record CreateRoleRequest(string Name);
public record RoleDto(Guid Id, string Name, bool IsSystemDefined, IReadOnlyList<Guid> PermissionIds);
public record SetRolePermissionsRequest(IReadOnlyList<Guid> PermissionIds);
public class SystemRoleDeletionException(string name) : Exception($"Role '{name}' is system-defined and cannot be deleted.");
public class RoleNotFoundException(Guid id) : Exception($"Role '{id}' was not found.");
```

**Create file: `src/SupportCrm.Application/Security/PermissionChecker.cs`**

```csharp
namespace SupportCrm.Application.Security;

using SupportCrm.Domain.Repositories;

public interface IPermissionChecker
{
    Task<bool> HasPermissionAsync(IReadOnlyList<Guid> roleIds, string module, string action, CancellationToken ct);
    Task<IReadOnlyList<string>> GetPermissionsAsync(IReadOnlyList<Guid> roleIds, CancellationToken ct);
}

// Deliberately re-queries the database on every call rather than trusting anything baked into the
// JWT — this is what makes "permission changes take effect without re-login" literally true
// (see this story's intake for the tradeoff against baking permissions into the token instead).
public class PermissionChecker(IPermissionRepository permissionRepository) : IPermissionChecker
{
    public async Task<bool> HasPermissionAsync(IReadOnlyList<Guid> roleIds, string module, string action, CancellationToken ct)
    {
        if (roleIds.Count == 0) return false;
        var granted = await permissionRepository.GetPermissionIdsForRolesAsync(roleIds, ct);
        if (granted.Count == 0) return false;
        var all = await permissionRepository.GetAllAsync(ct);
        return all.Any(p => granted.Contains(p.Id) && p.Module == module && p.Action == action);
    }

    public async Task<IReadOnlyList<string>> GetPermissionsAsync(IReadOnlyList<Guid> roleIds, CancellationToken ct)
    {
        var granted = await permissionRepository.GetPermissionIdsForRolesAsync(roleIds, ct);
        var all = await permissionRepository.GetAllAsync(ct);
        return all.Where(p => granted.Contains(p.Id)).Select(p => $"{p.Module}:{p.Action}").ToList();
    }
}
```

**Create file: `src/SupportCrm.Application/Security/RoleManagementService.cs`**

```csharp
namespace SupportCrm.Application.Security;

using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class RoleManagementService(IRoleRepository roleRepository, IPermissionRepository permissionRepository)
{
    public async Task<IReadOnlyList<PermissionDto>> GetPermissionCatalogAsync(CancellationToken ct) =>
        (await permissionRepository.GetAllAsync(ct)).Select(p => new PermissionDto(p.Id, p.Module, p.Action)).OrderBy(p => p.Module).ThenBy(p => p.Action).ToList();

    public async Task<IReadOnlyList<RoleDto>> GetAllAsync(CancellationToken ct)
    {
        var result = new List<RoleDto>();
        foreach (var role in await roleRepository.GetAllAsync(ct))
        {
            var permissionIds = await permissionRepository.GetPermissionIdsForRolesAsync(new[] { role.Id }, ct);
            result.Add(new RoleDto(role.Id, role.Name, role.IsSystemDefined, permissionIds));
        }
        return result;
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken ct)
    {
        var role = new Role(request.Name, isSystemDefined: false);
        await roleRepository.AddAsync(role, ct);
        await roleRepository.SaveChangesAsync(ct);
        return new RoleDto(role.Id, role.Name, role.IsSystemDefined, Array.Empty<Guid>());
    }

    public async Task DeleteAsync(Guid roleId, CancellationToken ct)
    {
        var role = await roleRepository.GetByIdAsync(roleId, ct) ?? throw new RoleNotFoundException(roleId);
        if (role.IsSystemDefined) throw new SystemRoleDeletionException(role.Name);
        await roleRepository.DeleteAsync(role, ct);
        await roleRepository.SaveChangesAsync(ct);
    }

    public async Task SetPermissionsAsync(Guid roleId, SetRolePermissionsRequest request, CancellationToken ct)
    {
        _ = await roleRepository.GetByIdAsync(roleId, ct) ?? throw new RoleNotFoundException(roleId);
        await permissionRepository.SetRolePermissionsAsync(roleId, request.PermissionIds, ct);
        await permissionRepository.SaveChangesAsync(ct);
    }
}
```

### 3 — Infrastructure: EF config + seed data, repository, DI

**File: `src/SupportCrm.Infrastructure/Persistence/SupportCrmDbContext.cs`** — add DbSets (`Permissions`, `RolePermissions`) and, in `OnModelCreating` right after Story 45's `UserRole` block:

```csharp
        var permissionModules = new[] { "Tickets", "Customers", "KnowledgeBase", "Sla", "Ai", "CustomerPortal", "Reports", "Administration" };
        var permissionActions = new[] { "View", "Create", "Edit", "Delete", "Export" };
        var permissionSeed = new List<(Guid Id, string Module, string Action)>();
        var seedIndex = 0;
        foreach (var module in permissionModules)
            foreach (var action in permissionActions)
                permissionSeed.Add((Guid.Parse($"66666666-6666-6666-6666-{seedIndex++:D12}"), module, action));

        modelBuilder.Entity<Permission>(entity =>
        {
            entity.ToTable("Permissions");
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Module).IsRequired().HasMaxLength(64);
            entity.Property(p => p.Action).IsRequired().HasMaxLength(32);
            entity.HasIndex(p => new { p.Module, p.Action }).IsUnique();

            entity.HasData(permissionSeed.Select(p => new { p.Id, p.Module, p.Action }));
        });

        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.ToTable("RolePermissions");
            entity.HasKey(rp => rp.Id);
            entity.HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();

            // The seeded Admin role starts with every permission — every other seeded role
            // (Agent/Team Lead/Manager) starts with none, assigned deliberately by an admin.
            var grantIndex = 0;
            entity.HasData(permissionSeed.Select(p => new { Id = Guid.Parse($"77777777-7777-7777-7777-{grantIndex++:D12}"), RoleId = adminRoleId, PermissionId = p.Id }));
        });
```

(`adminRoleId` is the same local variable Story 45 already declared earlier in this method — reused, not redeclared.)

**Create file: `src/SupportCrm.Infrastructure/Persistence/PermissionRepository.cs`**

```csharp
namespace SupportCrm.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using SupportCrm.Domain.Entities;
using SupportCrm.Domain.Repositories;

public class PermissionRepository(SupportCrmDbContext dbContext) : IPermissionRepository
{
    public async Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken ct) => await dbContext.Permissions.ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> GetPermissionIdsForRolesAsync(IReadOnlyList<Guid> roleIds, CancellationToken ct) =>
        await dbContext.RolePermissions.Where(rp => roleIds.Contains(rp.RoleId)).Select(rp => rp.PermissionId).Distinct().ToListAsync(ct);

    public Task SetRolePermissionsAsync(Guid roleId, IReadOnlyList<Guid> permissionIds, CancellationToken ct)
    {
        dbContext.RolePermissions.RemoveRange(dbContext.RolePermissions.Where(rp => rp.RoleId == roleId));
        foreach (var permissionId in permissionIds) dbContext.RolePermissions.Add(new RolePermission(roleId, permissionId));
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => dbContext.SaveChangesAsync(ct);
}
```

**File: `src/SupportCrm.Infrastructure/DependencyInjection.cs`** — add before `return services;`:

```csharp
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IPermissionChecker, PermissionChecker>();
        services.AddScoped<RoleManagementService>();
```

### 4 — Api: `RequirePermissionAttribute`, `RolesController`, wire it onto `UsersController`, `GET /api/auth/me`

**Create file: `src/SupportCrm.Api/Security/RequirePermissionAttribute.cs`**

```csharp
namespace SupportCrm.Api.Security;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SupportCrm.Application.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute(string module, string action) : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            context.Result = new UnauthorizedObjectResult(new { error = "Authentication required." });
            return;
        }

        var roleIds = context.HttpContext.User.FindAll(JwtTokenService.RoleIdClaimType)
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : (Guid?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        var permissionChecker = context.HttpContext.RequestServices.GetRequiredService<IPermissionChecker>();
        var allowed = await permissionChecker.HasPermissionAsync(roleIds, module, action, context.HttpContext.RequestAborted);
        if (!allowed)
        {
            context.Result = new ObjectResult(new { error = "You do not have permission to perform this action." }) { StatusCode = StatusCodes.Status403Forbidden };
            return;
        }

        await next();
    }
}
```

**File: `src/SupportCrm.Api/Controllers/UsersController.cs`** — add `using SupportCrm.Api.Security;` and one `[RequirePermission("Administration", ...)]` per action: `View` on `GetAll`, `Create` on `Create`, `Edit` on `Deactivate`/`Activate`/`SetRoles`, `Delete` on `Delete`.

**File: `src/SupportCrm.Api/Controllers/AuthController.cs`** — add:

```csharp

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<CurrentUserDto>> Me(
        [FromServices] SupportCrm.Domain.Repositories.IUserRepository userRepository,
        [FromServices] SupportCrm.Domain.Repositories.IRoleRepository roleRepository,
        [FromServices] IPermissionChecker permissionChecker, CancellationToken ct)
    {
        var userId = CurrentUserId();
        var user = await userRepository.GetByIdAsync(userId, ct);
        if (user is null) return NotFound();
        var roleIds = await userRepository.GetRoleIdsForUserAsync(userId, ct);
        var roles = await roleRepository.GetByIdsAsync(roleIds, ct);
        var permissions = await permissionChecker.GetPermissionsAsync(roleIds, ct);
        return new CurrentUserDto(user.Id, user.Email, roles.Select(r => r.Name).ToList(), permissions);
    }
```

**Create file: `src/SupportCrm.Api/Controllers/RolesController.cs`**

```csharp
namespace SupportCrm.Api.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SupportCrm.Api.Security;
using SupportCrm.Application.Security;

[ApiController]
[Route("api/admin/roles")]
[Authorize]
public class RolesController(RoleManagementService roleManagementService) : ControllerBase
{
    [HttpGet]
    [RequirePermission("Administration", "View")]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> GetAll(CancellationToken ct) => Ok(await roleManagementService.GetAllAsync(ct));

    [HttpGet("permissions")]
    [RequirePermission("Administration", "View")]
    public async Task<ActionResult<IReadOnlyList<PermissionDto>>> GetPermissionCatalog(CancellationToken ct) => Ok(await roleManagementService.GetPermissionCatalogAsync(ct));

    [HttpPost]
    [RequirePermission("Administration", "Create")]
    public async Task<ActionResult<RoleDto>> Create([FromBody] CreateRoleRequest request, CancellationToken ct) => await roleManagementService.CreateAsync(request, ct);

    [HttpDelete("{id:guid}")]
    [RequirePermission("Administration", "Delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try { await roleManagementService.DeleteAsync(id, ct); return NoContent(); }
        catch (RoleNotFoundException) { return NotFound(); }
        catch (SystemRoleDeletionException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("{id:guid}/permissions")]
    [RequirePermission("Administration", "Edit")]
    public async Task<IActionResult> SetPermissions(Guid id, [FromBody] SetRolePermissionsRequest request, CancellationToken ct)
    {
        try { await roleManagementService.SetPermissionsAsync(id, request, ct); return NoContent(); }
        catch (RoleNotFoundException) { return NotFound(); }
    }
}
```

---

## Edge Cases & Failure Modes

- **A role's permissions are edited while a user with that role has an active session** — their very next `[RequirePermission]`-guarded call re-resolves from the database and reflects the change immediately (see `PermissionChecker`'s own doc comment) — no re-login needed, satisfying the AC's primary clause directly.
- **A user's roles themselves are reassigned** — the JWT's `role_id` claims were baked in at login and don't change until the token is refreshed — flagged as the one case where the AC's "or clearly state if they do" fallback applies; surfaced to the frontend so it can say so.
- **Deleting a system-defined role** (`Agent`/`Team Lead`/`Manager`/`Admin`) — `400` with a clear message, never silently ignored or allowed.
- **A user with zero roles calling any `[RequirePermission]` endpoint** — `roleIds` is empty, `HasPermissionAsync` short-circuits to `false` before any query — a `403`, not a crash.
- **Deleting the last permission that grants a role any access at all** — allowed; an admin locking a role out of everything is a valid (if unusual) administrative action, not something this story second-guesses.

---

## Test Plan

1. **Unit — `tests/SupportCrm.Application.Tests/Security/PermissionCheckerTests.cs`**: `HasPermissionAsync_RoleWithoutGrant_ReturnsFalse`; `HasPermissionAsync_ReflectsAChangeMadeAfterTheRoleIdsWereResolved` (proves the "no re-login needed" claim).
2. **Unit — `tests/SupportCrm.Application.Tests/Security/RoleManagementServiceTests.cs`**: `DeleteAsync_SystemDefinedRole_Throws`.
3. **Integration — `tests/SupportCrm.Api.Tests/Controllers/UsersControllerTests.cs`**: `Get_All_WithoutPermission_Returns403WithMessage`.

---

## Verification Steps

1. **Backend builds:** `dotnet build SupportCrm.slnx` from `d:\Code\selfAssessment\backend`.
2. **Migration generation:** now that Story 45's and this story's model changes are both in place, run `dotnet ef migrations add AddSecurityAndAdministration --project src/SupportCrm.Infrastructure --startup-project src/SupportCrm.Api` once, from the repo root.
3. **Manual smoke:** log in as the seeded admin, call `GET /api/admin/users` (should succeed, Admin has every permission); create a custom role with zero permissions, confirm a user assigned only that role gets `403` on the same call.

---

## Done Criteria

- [ ] 40-row permission catalog seeded; custom roles can be created/deleted (except the 4 defaults) and have permissions assigned.
- [ ] `[RequirePermission]` blocks unauthorized calls with a clear `403` message and protects every SEC-1/SEC-2 endpoint.
- [ ] Permission changes take effect on the very next request, no re-login required.
- [ ] `dotnet build SupportCrm.slnx` succeeds.
