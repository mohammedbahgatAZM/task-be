# Story intake

- Folder: `.squad/stories/security-administration/SEC-2/intake.md`

---

## Feature

- **Feature name (display):** Security & Administration
- **Feature slug (folder under `plans/`):** `security-administration`

## Tracker (metadata only)

- **Tracker type:** `none`
- **Work item id:** `SEC-2`
- **Work item type:** `Story`

---

## Title

```
Permissions
```

---

## Description

```
Role: System Administrator
As a system administrator, I want to configure granular permissions per role, so that users only access what they're authorized to.
```

---

## Acceptance criteria

```
- Permissions can be set per module and action (view, create, edit, delete, export).
- Custom roles can be created beyond the default set, with a specific permission combination.
- A user attempting an unauthorized action is blocked with a clear message.
- Permission changes take effect without requiring the user to log out and back in, or clearly state if they do.
```

---

## Attachments

None.

---

## Dependencies

- **Blocked by / related ids:** Story 45 (SEC-1) — `User`, `Role`, JWT auth pipeline.
- **Depends on code areas or other stories:** none.

## Extra notes (optional)

- Permission catalog is seeded as a fixed (Module × Action) cross-product — 8 modules (`Tickets`, `Customers`, `KnowledgeBase`, `Sla`, `Ai`, `CustomerPortal`, `Reports`, `Administration`) × 5 actions (`View`, `Create`, `Edit`, `Delete`, `Export`) = 40 rows, seeded via migration data, matching the AC's own enumeration exactly.
- **Permission changes take effect immediately, without re-login** — the JWT deliberately carries only the user's role ids (roles themselves change rarely), never a baked-in permission list; the enforcement filter re-resolves role→permission grants from the database on every request. Changing what a role can do is therefore live on the user's very next request, not stale until their token expires. (Reassigning a *user's roles* does require a fresh login to pick up the new role claims — flagged as the one case where the AC's "or clearly state if they do" escape hatch applies.)
- Enforcement is a small `[RequirePermission(module, action)]` action filter, not full ASP.NET Core policy-provider machinery — matches this codebase's general preference for small, direct constructs over framework ceremony (e.g. controllers already inject services via `[FromServices]` rather than a DI-heavy abstraction layer).
- "Blocked with a clear message" — a `403` with a JSON body `{ "error": "You do not have permission to perform this action." }`, not a bare status code.
- Custom (non-system-defined) roles are full first-class roles — created, permission-assigned, and deleted through the same `RoleManagementService` as the four seeded defaults; only the four defaults are protected from deletion (`IsSystemDefined = true`), never from having their permissions edited.

## Technical hints (optional)

- Repos/roots: `.`. Primary language: `C#` / .NET 10.
- `src/SupportCrm.Application/Reports/ReportDtos.cs` (Reports & Management) — precedent for a shared DTO file per bounded concern, followed here as `SecurityDtos.cs`.

## Out of scope

- Applying `[RequirePermission]` to any pre-existing controller from an earlier module (see Story 45's own scope note — unchanged here).
